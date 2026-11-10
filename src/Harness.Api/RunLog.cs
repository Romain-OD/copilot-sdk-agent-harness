using System.Text.Json;
using System.Text.RegularExpressions;

namespace Harness.Api;

/// <summary>One agent run = one append-only JSONL file. The flight recorder behind replay, compare and evals.</summary>
public sealed class RunLog
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly Lock _gate = new();
    readonly List<JsonElement> _events = [];
    readonly string _file;

    internal RunLog(string dir, string profile, string engine, string prompt)
    {
        Id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("n")[..6]}";
        Profile = profile;
        _file = Path.Combine(dir, Id + ".jsonl");
        Add("start", new { profile, engine, prompt });
    }

    public string Id { get; }
    public string Profile { get; }

    public IReadOnlyList<JsonElement> Events { get { lock (_gate) return [.. _events]; } }

    public void Add(string kind, object data)
    {
        var line = JsonSerializer.SerializeToElement(new { t = DateTimeOffset.UtcNow, kind, data }, Json);
        lock (_gate)
        {
            _events.Add(line);
            File.AppendAllText(_file, line.GetRawText() + "\n");
        }
    }
}

public sealed partial class RunStore(string dir)
{
    public string Dir { get; } = Directory.CreateDirectory(dir).FullName;

    public RunLog Start(string profile, string engine, string prompt) => new(Dir, profile, engine, prompt);

    public IEnumerable<JsonElement> List(int take = 50) =>
        new DirectoryInfo(Dir).EnumerateFiles("*.jsonl")
            .OrderByDescending(f => f.Name).Take(take)
            .Select(f => Summarize(f.Name[..^6], Read(f.FullName)));

    public IReadOnlyList<JsonElement>? Get(string id) =>
        ValidId().IsMatch(id) && File.Exists(Path.Combine(Dir, id + ".jsonl")) ? Read(Path.Combine(Dir, id + ".jsonl")) : null;

    static List<JsonElement> Read(string file) =>
        [.. File.ReadLines(file).Where(l => l.Length > 0).Select(l => JsonDocument.Parse(l).RootElement)];

    static JsonElement Summarize(string id, List<JsonElement> events)
    {
        var start = events[0].GetProperty("data");
        var end = events.LastOrDefault(e => e.GetProperty("kind").GetString() == "end");
        return JsonSerializer.SerializeToElement(new
        {
            id,
            profile = start.GetProperty("profile").GetString(),
            engine = start.GetProperty("engine").GetString(),
            prompt = start.GetProperty("prompt").GetString(),
            tools = events.Count(e => e.GetProperty("kind").GetString() == "tool"),
            end = end.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : end.GetProperty("data"),
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [GeneratedRegex("^[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$")]
    private static partial Regex ValidId();
}
