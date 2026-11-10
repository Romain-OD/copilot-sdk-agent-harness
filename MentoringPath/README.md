# Mentoring Path

Ten chapters, each one small step on the real harness in [`src/`](../src). Read the chapter, then do its **Exercise** on a branch.

| # | Chapter | You learn |
|---|---|---|
| 00 | [Setup](00-setup.md) | Run the harness, sign in, read your first run log |
| 01 | [Copilot SDK hello](01-copilot-sdk-hello.md) | `CopilotClient`, sessions, streaming events |
| 02 | [Tools and hooks](02-tools-and-hooks.md) | Built-in tools, `OnPreToolUse`, permissions |
| 03 | [MAF agents](03-maf-agents.md) | `ChatClientAgent` → `HarnessAgent`; where Semantic Kernel went |
| 04 | [Copilot as a MAF agent](04-copilot-as-maf-agent.md) | One `AIAgent`, two engines, middleware on both |
| 05 | [AG-UI and React](05-agui-and-react.md) | `MapAGUIServer`, SSE events, `@ag-ui/client` |
| 06 | [Config and policy](06-config-and-policy.md) | `harness.yaml`, JSON Schema, allow/deny/ask gates |
| 07 | [Compare, replay, evals](07-compare-replay-evals.md) | Flight recorder, side-by-side, evals in CI |
| 08 | [Observability](08-observability.md) | OpenTelemetry, Aspire dashboard, cost per run |
| 09 | [Production](09-production.md) | Sandboxing, secrets, MCP servers, what to add before hosting |

Before every exercise: `dotnet test src/Harness.slnx` must be green.
