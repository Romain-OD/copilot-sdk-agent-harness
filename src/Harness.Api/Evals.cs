using System.IO.Enumeration;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace Harness.Api;

/// <summary>Deterministic assertions over a recorded run. LLM-as-judge is deliberately not here (yet).</summary>
public static class Evals
{
    public static List<string> Check(EvalCase c, string text, IReadOnlyList<JsonElement> events)
    {
        var failures = new List<string>();
        foreach (var s in c.Contains.Where(s => !text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            failures.Add($"expected text to contain '{s}'");
        foreach (var s in c.NotContains.Where(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            failures.Add($"expected text NOT to contain '{s}'");

        var tools = events.Where(e => e.GetProperty("kind").GetString() == "tool").Select(e => e.GetProperty("data")).ToList();
        foreach (var t in c.ToolsCalled.Where(t => !tools.Any(x => x.GetProperty("decision").GetString() == "allow"
                     && FileSystemName.MatchesSimpleExpression(t, x.GetProperty("name").GetString()!, ignoreCase: true))))
            failures.Add($"expected tool '{t}' to be called");
        if (c.NoDeniedTools)
            failures.AddRange(tools.Where(x => x.GetProperty("decision").GetString() == "deny")
                .Select(x => $"tool '{x.GetProperty("name").GetString()}' was denied ({x.GetProperty("reason").GetString()})"));
        return failures;
    }

    /// <summary>`dotnet run -- eval [profile]`: runs every case, prints a table, returns the process exit code.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string? onlyProfile, TextWriter output)
    {
        var cfg = services.GetRequiredService<ConfigStore>().Current;
        var factory = services.GetRequiredService<AgentFactory>();
        services.GetRequiredService<Approvals>().Unattended = true;
        var failed = 0;
        foreach (var c in cfg.Evals)
            foreach (var profile in (c.Profiles.Count > 0 ? c.Profiles : [.. cfg.Profiles.Keys]).Where(p => onlyProfile is null || p == onlyProfile))
            {
                List<string> failures;
                try
                {
                    var response = await factory.Create(profile).RunAsync(c.Prompt);
                    failures = Check(c, response.Text, services.GetRequiredService<RunStore>().Get(AgentFactory.RunId(response))!);
                }
                catch (Exception ex) { failures = [$"run failed: {ex.Message}"]; }
                failed += failures.Count > 0 ? 1 : 0;
                output.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}  {c.Name} @ {profile}");
                foreach (var f in failures) output.WriteLine($"      - {f}");
            }
        output.WriteLine(failed == 0 ? "All evals passed." : $"{failed} eval(s) failed.");
        return failed == 0 ? 0 : 1;
    }
}
