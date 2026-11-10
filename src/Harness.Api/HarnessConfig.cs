using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Harness.Api;

public sealed class HarnessConfig
{
    public string DefaultProfile { get; set; } = "";
    public PolicyConfig Policy { get; set; } = new();
    public Dictionary<string, Profile> Profiles { get; set; } = [];
    public List<EvalCase> Evals { get; set; } = [];

    static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build(); // unknown keys throw on purpose: a typo in harness.yaml must not be silently ignored

    public static HarnessConfig Parse(string yaml) => Yaml.Deserialize<HarnessConfig>(yaml) ?? new();

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Profiles.Count == 0) errors.Add("profiles: at least one profile is required");
        if (!Profiles.ContainsKey(DefaultProfile)) errors.Add($"defaultProfile: '{DefaultProfile}' is not a defined profile");
        ValidatePolicy(Policy, "policy", errors);
        foreach (var (name, p) in Profiles)
        {
            if (p.Engine is not ("copilot" or "maf")) errors.Add($"profiles.{name}.engine: must be 'copilot' or 'maf'");
            if (p.Engine == "maf" && (p.Provider is null || p.Model is null))
                errors.Add($"profiles.{name}: the maf engine needs 'model' and 'provider'");
            if (p.Engine == "maf" && p.Provider is { Type: not "openai" })
                errors.Add($"profiles.{name}.provider.type: the maf engine only supports 'openai' (any OpenAI-compatible endpoint)");
            if (p.Provider is { BaseUrl: "" }) errors.Add($"profiles.{name}.provider.baseUrl: required");
            if (p.Policy is not null) ValidatePolicy(p.Policy, $"profiles.{name}.policy", errors);
            foreach (var (mcp, s) in p.McpServers)
                if ((s.Command is null) == (s.Url is null)) errors.Add($"profiles.{name}.mcpServers.{mcp}: set exactly one of 'command' or 'url'");
        }
        foreach (var e in Evals)
            foreach (var p in e.Profiles.Where(p => !Profiles.ContainsKey(p)))
                errors.Add($"evals.{e.Name}.profiles: unknown profile '{p}'");
        return errors;
    }

    static void ValidatePolicy(PolicyConfig policy, string path, List<string> errors)
    {
        if (!PolicyConfig.Decisions.Contains(policy.Default)) errors.Add($"{path}.default: must be allow, deny or ask");
        foreach (var r in policy.Rules)
        {
            if (!PolicyConfig.Decisions.Contains(r.Decision)) errors.Add($"{path}.rules[{r.Tool}].decision: must be allow, deny or ask");
            if (r.Args is not null)
                try { _ = new Regex(r.Args); }
                catch (ArgumentException ex) { errors.Add($"{path}.rules[{r.Tool}].args: invalid regex ({ex.Message})"); }
        }
    }
}

public sealed class Profile
{
    public string Engine { get; set; } = "copilot";
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    public string? WorkingDirectory { get; set; }
    public ProviderSettings? Provider { get; set; }
    public Dictionary<string, McpServer> McpServers { get; set; } = [];
    public PolicyConfig? Policy { get; set; }
    /// <summary>MAF HarnessAgent features to turn off: webSearch, fileMemory, todos, agentMode, skills, compaction.</summary>
    public List<string> Disable { get; set; } = [];
    public Pricing? Pricing { get; set; }
}

public sealed class ProviderSettings
{
    /// <summary>openai (any OpenAI-compatible endpoint), azure or anthropic (copilot engine only).</summary>
    public string Type { get; set; } = "openai";
    /// <summary>Required, e.g. https://api.openai.com/v1 or http://localhost:11434/v1.</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Name of the environment variable holding the key. Keys never live in harness.yaml.</summary>
    public string? ApiKeyEnv { get; set; }
}

public sealed class McpServer
{
    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];
    public string? Url { get; set; }
    public List<string> Tools { get; set; } = ["*"];
}

public sealed class PolicyConfig
{
    public static readonly string[] Decisions = ["allow", "deny", "ask"];
    public string Default { get; set; } = "ask";
    public List<PolicyRule> Rules { get; set; } = [];
}

public sealed class PolicyRule
{
    /// <summary>Tool name glob, e.g. "view", "mcp_*", "*".</summary>
    public string Tool { get; set; } = "*";
    /// <summary>Optional regex matched against the JSON arguments.</summary>
    public string? Args { get; set; }
    public string Decision { get; set; } = "ask";
}

public sealed class Pricing
{
    public decimal InputPer1M { get; set; }
    public decimal OutputPer1M { get; set; }
}

public sealed class EvalCase
{
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    /// <summary>Profiles to run this case on. Empty = all profiles.</summary>
    public List<string> Profiles { get; set; } = [];
    public List<string> Contains { get; set; } = [];
    public List<string> NotContains { get; set; } = [];
    public List<string> ToolsCalled { get; set; } = [];
    public bool NoDeniedTools { get; set; }
}

/// <summary>Reads harness.yaml, reloads it when the file changes, keeps the last valid version if an edit is broken.</summary>
public sealed class ConfigStore(string path)
{
    readonly Lock _gate = new();
    HarnessConfig? _current;
    DateTime _stamp;

    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string? LastError { get; private set; }

    public HarnessConfig Current
    {
        get
        {
            lock (_gate)
            {
                var stamp = File.GetLastWriteTimeUtc(Path);
                if (_current is not null && stamp == _stamp) return _current;
                _stamp = stamp;
                var (cfg, errors) = TryLoad(File.ReadAllText(Path));
                LastError = errors.Count == 0 ? null : string.Join("\n", errors);
                if (cfg is not null && errors.Count == 0) _current = cfg;
                return _current ?? throw new InvalidOperationException($"{Path} is invalid:\n{LastError}");
            }
        }
    }

    public List<string> Save(string yaml)
    {
        var (_, errors) = TryLoad(yaml);
        if (errors.Count == 0) File.WriteAllText(Path, yaml);
        return errors;
    }

    static (HarnessConfig?, List<string>) TryLoad(string yaml)
    {
        try
        {
            var cfg = HarnessConfig.Parse(yaml);
            return (cfg, cfg.Validate());
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            return (null, [$"yaml: {ex.Message}{(ex.InnerException is null ? "" : " " + ex.InnerException.Message)}"]);
        }
    }
}
