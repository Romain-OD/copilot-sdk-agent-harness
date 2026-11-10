# 08 — Observability

Goal: see every model call, tool call and token, across both engines, in one trace.

## Wiring

[`Program.cs`](../src/Harness.Api/Program.cs) does two things when `OTEL_EXPORTER_OTLP_ENDPOINT` is set:

1. `CopilotClientOptions.Telemetry.OtlpEndpoint`: the Copilot CLI exports its own spans (model calls, tools).
2. `AddOpenTelemetry().WithTracing(t => t.AddSource("*")).UseOtlpExporter()`: MAF, Microsoft.Extensions.AI and ASP.NET Core spans.

`AddSource("*")` is a learning setting: you see everything. In production, list the sources you need.

## Local dashboard in one command

```shell
docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project src/Harness.Api
```

Open http://localhost:18888 (the login token is printed in the container logs). No AppHost needed.

## Cost per run

Each profile may declare `pricing: { inputPer1M, outputPer1M }`. The recorder multiplies it with the usage reported by the engine
and writes `cost` into the `end` event. Copilot subscription usage is billed in premium requests, not tokens:
treat the number as a comparable estimate, not an invoice.

## Exercise

1. Start the dashboard, run one Copilot and one MAF prompt. Find the tool span of each run.
2. Add the harness run id as a span tag (`Activity.Current?.SetTag("harness.run", run.Id)`) in the recorder, so a trace links to its JSONL.
3. Add a `/api/stats` endpoint: total cost and average latency per profile over the stored runs.
