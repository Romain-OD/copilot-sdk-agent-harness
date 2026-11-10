using System.Runtime.CompilerServices;
using System.Text.Json;
using Harness.Api;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Harness.Tests;

public class PolicyTests
{
    static readonly PolicyConfig Sample = HarnessConfig.Parse(File.ReadAllText("harness.yaml")).Policy;

    [Theory]
    [InlineData("view", "{\"path\":\"a.txt\"}", "allow")]
    [InlineData("powershell", "{\"command\":\"Remove-Item -Recurse .\"}", "deny")]
    [InlineData("view", "{\"path\":\"rm -rf\"}", "allow")] // first matching rule wins: order matters
    [InlineData("create", "{\"path\":\"a.txt\"}", "ask")]
    public void Evaluate_sample_policy_returns_expected_decision(string tool, string args, string expected) =>
        Assert.Equal(expected, Policy.Evaluate(Sample, tool, args).Decision);

    [Fact]
    public async Task Ask_is_denied_when_unattended()
    {
        var approvals = new Approvals { Unattended = true };
        Assert.False(await approvals.CheckAsync(new PolicyConfig { Default = "ask" }, null, "create", null, default));
    }

    [Fact]
    public async Task Ask_waits_for_a_human_and_allows_when_approved()
    {
        var approvals = new Approvals();
        var check = approvals.CheckAsync(new PolicyConfig { Default = "ask" }, null, "create", null, default);
        var pending = await WaitFor(() => approvals.List().FirstOrDefault());
        Assert.True(approvals.Resolve(pending.Id, approve: true));
        Assert.True(await check);
    }

    static async Task<T> WaitFor<T>(Func<T?> probe)
    {
        for (var i = 0; i < 100; i++, await Task.Delay(10))
            if (probe() is { } value) return value;
        throw new TimeoutException();
    }
}

public class ConfigTests
{
    [Fact]
    public void Sample_harness_yaml_is_valid() =>
        Assert.Empty(HarnessConfig.Parse(File.ReadAllText("harness.yaml")).Validate());

    [Fact]
    public void Validate_reports_semantic_errors()
    {
        var errors = HarnessConfig.Parse("""
            defaultProfile: missing
            policy: { default: maybe }
            profiles:
              a: { engine: rust }
              b: { engine: maf }
            """).Validate();
        Assert.Contains(errors, e => e.StartsWith("defaultProfile"));
        Assert.Contains(errors, e => e.StartsWith("policy.default"));
        Assert.Contains(errors, e => e.StartsWith("profiles.a.engine"));
        Assert.Contains(errors, e => e.Contains("maf engine needs"));
    }

    [Fact]
    public void ConfigStore_rejects_unknown_keys_and_keeps_the_file()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "defaultProfile: a\nprofiles: { a: { engine: copilot } }\n");
        var errors = new ConfigStore(path).Save("defaultProfile: a\nprofiles: { a: { engin: copilot } }\n");
        Assert.Single(errors);
        Assert.Contains("engine: copilot", File.ReadAllText(path));
    }
}

public class EvalTests
{
    [Fact]
    public void Check_reports_missing_text_missing_tool_and_denied_tool()
    {
        var events = new[]
        {
            Event("tool", new { name = "view", decision = "allow", reason = "rule" }),
            Event("tool", new { name = "powershell", decision = "deny", reason = "rule '*'" }),
        };
        var c = new EvalCase { Contains = ["records"], ToolsCalled = ["view", "grep"], NoDeniedTools = true };
        var failures = Evals.Check(c, "a class", events);
        Assert.Equal(3, failures.Count);
        Assert.Empty(Evals.Check(new EvalCase { Contains = ["CLASS"], ToolsCalled = ["vi*"] }, "a class", events));
    }

    static JsonElement Event(string kind, object data) => JsonSerializer.SerializeToElement(new { kind, data });
}

public class MafEngineTests
{
    [Fact]
    public async Task Maf_engine_routes_tool_calls_through_the_policy_and_records_the_run()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "harness.yaml"), """
            defaultProfile: maf
            policy: { default: deny }
            profiles:
              maf:
                engine: maf
                model: fake
                provider: { type: openai, baseUrl: "http://localhost/v1" }
                disable: [webSearch, fileMemory, skills]
            """);
        var runs = new RunStore(Path.Combine(dir, "runs"));
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var chat = new ToolCallingChatClient();
        var factory = new AgentFactory(new ConfigStore(Path.Combine(dir, "harness.yaml")), runs, new Approvals(), services)
        {
            ChatClientFactory = _ => chat,
        };

        var response = await factory.Create("maf").RunAsync("go");

        Assert.Equal("done", response.Text);
        Assert.Contains("Denied by harness policy", chat.ToolResult);
        var events = runs.Get(AgentFactory.RunId(response))!;
        var tool = Assert.Single(events, e => e.GetProperty("kind").GetString() == "tool").GetProperty("data");
        Assert.Equal(chat.CalledTool, tool.GetProperty("name").GetString());
        Assert.Equal("deny", tool.GetProperty("decision").GetString());
        Assert.Equal("end", events[^1].GetProperty("kind").GetString());
    }

    /// <summary>First turn calls the first tool the harness offers, second turn answers "done".</summary>
    sealed class ToolCallingChatClient : IChatClient
    {
        public string? CalledTool;
        public string? ToolResult;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var result = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
            if (result is not null)
            {
                ToolResult = result.Result?.ToString();
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
            }
            CalledTool = options!.Tools!.OfType<AIFunction>().First().Name;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call1", CalledTool)])));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var u in (await GetResponseAsync(messages, options, ct)).ToChatResponseUpdates()) yield return u;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
