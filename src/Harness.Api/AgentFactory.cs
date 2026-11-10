using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GitHub.Copilot;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Harness.Api;

/// <summary>
/// harness.yaml profile -> AIAgent. Both engines end up as the same MAF <see cref="AIAgent"/>,
/// so hosting (AG-UI), recording, policy and evals don't care which one runs.
/// </summary>
public sealed class AgentFactory(ConfigStore config, RunStore runs, Approvals approvals, IServiceProvider services)
{
    /// <summary>Test seam: swap the model client of the maf engine.</summary>
    public Func<Profile, IChatClient> ChatClientFactory { get; set; } = CreateChatClient;

    public AIAgent Create(string profileName)
    {
        var cfg = config.Current;
        if (!cfg.Profiles.TryGetValue(profileName, out var profile))
            throw new KeyNotFoundException($"Unknown profile '{profileName}'");

        var policy = profile.Policy ?? cfg.Policy;
        var current = new RunHolder();
        var engine = profile.Engine == "maf" ? CreateMaf(profileName, profile, policy, current) : CreateCopilot(profileName, profile, policy, current);
        return engine.AsBuilder().Use(
            (msgs, session, opts, inner, ct) => RecordAsync(msgs, session, opts, inner, profileName, profile, current, ct),
            (msgs, session, opts, inner, ct) => RecordStreamingAsync(msgs, session, opts, inner, profileName, profile, current, ct)).Build();
    }

    AIAgent CreateCopilot(string name, Profile p, PolicyConfig policy, RunHolder current)
    {
        var session = new SessionConfig
        {
            Model = p.Model,
            Streaming = true,
            WorkingDirectory = p.WorkingDirectory is null ? null : Path.GetFullPath(p.WorkingDirectory),
            SystemMessage = p.SystemPrompt is null ? null : new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = p.SystemPrompt },
            Provider = p.Provider is null ? null : new ProviderConfig
            {
                Type = p.Provider.Type,
                BaseUrl = p.Provider.BaseUrl,
                ApiKey = Environment.GetEnvironmentVariable(p.Provider.ApiKeyEnv ?? "") ?? "",
            },
            McpServers = p.McpServers.ToDictionary(kv => kv.Key, kv => kv.Value.Url is null
                ? (McpServerConfig)new McpStdioServerConfig { Command = kv.Value.Command!, Args = kv.Value.Args, Tools = kv.Value.Tools }
                : new McpHttpServerConfig { Url = kv.Value.Url, Tools = kv.Value.Tools }),
            // The policy gate below decides every tool call, so the CLI's own prompt is pre-approved.
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Hooks = new SessionHooks
            {
                OnPreToolUse = async (input, _) =>
                {
                    var ok = await approvals.CheckAsync(policy, current.Run, input.ToolName, input.ToolArgs?.GetRawText(), CancellationToken.None);
                    return new PreToolUseHookOutput { PermissionDecision = ok ? "allow" : "deny", PermissionDecisionReason = ok ? null : "Denied by harness policy" };
                },
                OnPostToolUse = (input, _) =>
                {
                    var r = input.ToolResult;
                    var text = r is { ValueKind: JsonValueKind.Object } && r.Value.TryGetProperty("textResultForLlm", out var t) ? t.GetString() : r?.GetRawText();
                    current.Run?.Add("tool_result", new { name = input.ToolName, result = Truncate(text) });
                    return Task.FromResult<PostToolUseHookOutput?>(null)!;
                },
            },
        };
        return services.GetRequiredService<CopilotClient>().AsAIAgent(session, false, name, name, $"Copilot SDK profile '{name}'");
    }

    AIAgent CreateMaf(string name, Profile p, PolicyConfig policy, RunHolder current)
    {
        bool Off(string feature) => p.Disable.Contains(feature, StringComparer.OrdinalIgnoreCase);
        var options = new HarnessAgentOptions
        {
            Id = name,
            Name = name,
            Description = $"MAF HarnessAgent profile '{name}'",
            ChatOptions = new ChatOptions { ModelId = p.Model, Instructions = p.SystemPrompt },
            DisableWebSearch = Off("webSearch"),
            DisableFileMemory = Off("fileMemory"),
            DisableTodoProvider = Off("todos"),
            DisableAgentModeProvider = Off("agentMode"),
            DisableAgentSkillsProvider = Off("skills"),
            DisableCompaction = Off("compaction"),
            // Our policy gate is the single source of truth, so MAF's own approval step auto-approves.
            ToolApprovalAgentOptions = new ToolApprovalAgentOptions { AutoApprovalRules = [_ => ValueTask.FromResult(true)] },
        };
        return ChatClientFactory(p).AsHarnessAgent(options, services.GetService<ILoggerFactory>(), services)
            .AsBuilder()
            .Use(async (agent, ctx, next, ct) =>
            {
                var args = JsonSerializer.Serialize(ctx.Arguments);
                if (!await approvals.CheckAsync(policy, current.Run, ctx.Function.Name, args, ct))
                    return "Denied by harness policy.";
                var result = await next(ctx, ct);
                current.Run?.Add("tool_result", new { name = ctx.Function.Name, result = Truncate(JsonSerializer.Serialize(result)) });
                return result;
            })
            .Build();
    }

    static IChatClient CreateChatClient(Profile p)
    {
        var env = p.Provider!.ApiKeyEnv ?? "OPENAI_API_KEY";
        var key = Environment.GetEnvironmentVariable(env)
            ?? (p.Provider.BaseUrl.Contains("localhost") ? "local" : throw new InvalidOperationException($"Set the {env} environment variable."));
        var options = new OpenAIClientOptions();
        options.Endpoint = new Uri(p.Provider.BaseUrl);
        return new OpenAIClient(new ApiKeyCredential(key), options).GetChatClient(p.Model).AsIChatClient();
    }

    async Task<AgentResponse> RecordAsync(IEnumerable<ChatMessage> msgs, AgentSession? session, AgentRunOptions? opts, AIAgent inner,
        string name, Profile p, RunHolder current, CancellationToken ct)
    {
        var run = current.Run = runs.Start(name, p.Engine, LastUserText(msgs));
        var started = DateTimeOffset.UtcNow;
        try
        {
            var response = await inner.RunAsync(msgs, session, opts, ct);
            End(run, p, started, response.Text, response.Usage);
            (response.AdditionalProperties ??= [])["runId"] = run.Id;
            return response;
        }
        catch (Exception ex)
        {
            run.Add("error", new { message = ex.Message });
            End(run, p, started, null, null);
            throw;
        }
    }

    async IAsyncEnumerable<AgentResponseUpdate> RecordStreamingAsync(IEnumerable<ChatMessage> msgs, AgentSession? session, AgentRunOptions? opts,
        AIAgent inner, string name, Profile p, RunHolder current, [EnumeratorCancellation] CancellationToken ct)
    {
        var run = current.Run = runs.Start(name, p.Engine, LastUserText(msgs));
        var started = DateTimeOffset.UtcNow;
        var text = new System.Text.StringBuilder();
        var usage = new UsageDetails();
        await using var e = inner.RunStreamingAsync(msgs, session, opts, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            AgentResponseUpdate update;
            try
            {
                if (!await e.MoveNextAsync()) break;
                update = e.Current;
            }
            catch (Exception ex)
            {
                run.Add("error", new { message = ex.Message });
                End(run, p, started, text.ToString(), usage);
                throw;
            }
            text.Append(update.Text);
            foreach (var u in update.Contents.OfType<UsageContent>()) usage.Add(u.Details);
            yield return update;
        }
        End(run, p, started, text.ToString(), usage);
    }

    static void End(RunLog run, Profile p, DateTimeOffset started, string? text, UsageDetails? usage)
    {
        if (text is not null) run.Add("message", new { text });
        decimal? cost = p.Pricing is null || usage is null ? null
            : ((usage.InputTokenCount ?? 0) * p.Pricing.InputPer1M + (usage.OutputTokenCount ?? 0) * p.Pricing.OutputPer1M) / 1_000_000m;
        run.Add("end", new
        {
            ms = (int)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            inputTokens = usage?.InputTokenCount,
            outputTokens = usage?.OutputTokenCount,
            cost,
        });
    }

    public static string RunId(AgentResponse response) => (string)response.AdditionalProperties!["runId"]!;

    static string LastUserText(IEnumerable<ChatMessage> msgs) => msgs.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";

    static string? Truncate(string? s) => s is { Length: > 2000 } ? s[..2000] + "…" : s;

    sealed class RunHolder { public RunLog? Run; }
}
