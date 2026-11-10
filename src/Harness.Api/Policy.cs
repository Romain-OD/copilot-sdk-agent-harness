using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Harness.Api;

public static class Policy
{
    /// <summary>First matching rule wins; otherwise the policy default.</summary>
    public static (string Decision, string Reason) Evaluate(PolicyConfig policy, string tool, string? args)
    {
        foreach (var r in policy.Rules)
            if (FileSystemName.MatchesSimpleExpression(r.Tool, tool, ignoreCase: true)
                && (r.Args is null || Regex.IsMatch(args ?? "", r.Args, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))))
                return (r.Decision, r.Args is null ? $"rule '{r.Tool}'" : $"rule '{r.Tool}' /{r.Args}/");
        return (policy.Default, "policy default");
    }
}

/// <summary>Turns policy decisions into allow/deny, parking "ask" until a human answers in the UI.</summary>
public sealed class Approvals
{
    readonly ConcurrentDictionary<string, Pending> _pending = new();

    /// <summary>True in eval/CI mode: nobody is there to answer, so "ask" means deny.</summary>
    public bool Unattended { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    public IEnumerable<Pending> List() => _pending.Values.OrderBy(p => p.At);

    public bool Resolve(string id, bool approve) => _pending.TryGetValue(id, out var p) && p.Answer.TrySetResult(approve);

    public async Task<bool> CheckAsync(PolicyConfig policy, RunLog? run, string tool, string? args, CancellationToken ct)
    {
        var (decision, reason) = Policy.Evaluate(policy, tool, args);
        if (decision == "ask")
        {
            var approved = !Unattended && await AskAsync(run, tool, args, ct);
            (decision, reason) = approved ? ("allow", "approved by human")
                : ("deny", Unattended ? "ask in unattended mode" : "rejected by human or timed out");
        }
        run?.Add("tool", new { name = tool, args, decision, reason });
        return decision == "allow";
    }

    async Task<bool> AskAsync(RunLog? run, string tool, string? args, CancellationToken ct)
    {
        var p = new Pending(Guid.NewGuid().ToString("n"), run?.Profile ?? "", run?.Id ?? "", tool, args, DateTimeOffset.UtcNow);
        _pending[p.Id] = p;
        try { return await p.Answer.Task.WaitAsync(Timeout, ct); }
        catch (TimeoutException) { return false; }
        finally { _pending.TryRemove(p.Id, out _); }
    }

    public sealed record Pending(string Id, string Profile, string RunId, string Tool, string? Args, DateTimeOffset At)
    {
        [JsonIgnore] public TaskCompletionSource<bool> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
