---
applyTo: "src/**/*.cs"
---

- Target .NET 10, C# latest. Nullable enabled; no `!` without a reason. Warnings are errors.
- Both engines are MAF `AIAgent`s. Add behavior as agent/function middleware or in the control plane, never as a custom agent loop.
- Every tool call goes through `Approvals.CheckAsync` and is recorded in the `RunLog`.
- A new `harness.yaml` field means: property in `HarnessConfig.cs`, rule in `Validate()`, entry in `harness.schema.json`, row in `Guides/harness-yaml-reference.md`, a test in `ConfigTests`.
- Minimal API endpoints return `Results.*` for expected failures; validate ids and paths at the endpoint.
- Tests use a fake `IChatClient` via `AgentFactory.ChatClientFactory`. No network in tests.
