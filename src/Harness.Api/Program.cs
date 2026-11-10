using GitHub.Copilot;
using Harness.Api;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
string FromRoot(string key, string fallback) => Path.Combine(builder.Environment.ContentRootPath, builder.Configuration[key] ?? fallback);

var otlp = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddSingleton(new ConfigStore(FromRoot("Harness:Config", "harness.yaml")));
builder.Services.AddSingleton(new RunStore(FromRoot("Harness:Runs", "runs")));
builder.Services.AddSingleton<Approvals>();
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton(_ => new CopilotClient(new CopilotClientOptions
{
    Telemetry = otlp is null ? null : new TelemetryConfig { OtlpEndpoint = otlp },
}));
builder.Services.AddAGUIServer();
if (otlp is not null)
    builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("*")).UseOtlpExporter();

var app = builder.Build();

if (args is ["eval", ..])
    return await Evals.RunAsync(app.Services, args.Skip(1).FirstOrDefault(a => !a.StartsWith('-')), Console.Out);

app.MapGet("/api/profiles", (ConfigStore c) => new
{
    c.Current.DefaultProfile,
    Profiles = c.Current.Profiles.Select(kv => new { Name = kv.Key, kv.Value.Engine, kv.Value.Model }),
    ConfigError = c.LastError,
});
app.MapGet("/api/config", (ConfigStore c) => Results.Text(File.ReadAllText(c.Path), "text/yaml"));
app.MapPut("/api/config", async (HttpRequest req, ConfigStore c) =>
{
    var errors = c.Save(await new StreamReader(req.Body).ReadToEndAsync());
    return errors.Count == 0 ? Results.NoContent() : Results.BadRequest(new { errors });
});

app.MapGet("/api/models", async (CopilotClient client, CancellationToken ct) =>
{
    await client.StartAsync(ct);
    return await client.ListModelsAsync(ct);
});

app.MapGet("/api/runs", (RunStore r) => r.List());
app.MapGet("/api/runs/{id}", (string id, RunStore r) => r.Get(id) is { } events ? Results.Ok(events) : Results.NotFound());
app.MapPost("/api/runs", async (RunRequest req, ConfigStore c, AgentFactory f, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt)) return Results.BadRequest(new { error = "prompt is required" });
    if (!c.Current.Profiles.ContainsKey(req.Profile)) return Results.NotFound(new { error = $"unknown profile '{req.Profile}'" });
    var response = await f.Create(req.Profile).RunAsync(req.Prompt, cancellationToken: ct);
    return Results.Ok(new { RunId = AgentFactory.RunId(response), response.Text });
});

app.MapGet("/api/approvals", (Approvals a) => a.List());
app.MapPost("/api/approvals/{id}", (string id, ApprovalAnswer answer, Approvals a) =>
    a.Resolve(id, answer.Approve) ? Results.NoContent() : Results.NotFound());

app.MapAGUIServer("/agui/{profile}", ctx =>
    ctx.RequestServices.GetRequiredService<AgentFactory>().Create((string)ctx.Request.RouteValues["profile"]!));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

record RunRequest(string Profile, string Prompt);
record ApprovalAnswer(bool Approve);

public partial class Program;
