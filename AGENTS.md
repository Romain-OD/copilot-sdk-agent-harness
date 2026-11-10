# AGENTS.md

A learning repo: a C# + React agent harness in `src/`, plus Markdown chapters in `MentoringPath/` and `Guides/`.

## Verify your work
- Backend changes: `dotnet test src/Harness.slnx` must pass. Never report done on a red build.
- UI changes: `cd src/web && npm run lint && npm run build` must pass.
- `harness.yaml` / config changes: keep `harness.schema.json` and `HarnessConfig.Validate()` in sync.
- Doc changes: relative links must resolve; external links point to official docs where possible.

## Rules
- No custom agent loop. Both engines are MAF `AIAgent`s; new behavior goes in the control plane (config, policy, recorder, evals).
- Engine selection stays a `switch` in `AgentFactory`. No plugin system, no `IEngine` interface.
- Tool permissions are decided only by `Policy`/`Approvals`. Never set `PermissionHandler.ApproveAll` without the `OnPreToolUse` gate.
- Secrets never go in `harness.yaml`: providers use `apiKeyEnv`.
- Tests must not call a real model. Use the `AgentFactory.ChatClientFactory` seam with a fake `IChatClient`.
- Every MentoringPath page ends with an `## Exercise` section that uses `src/`.
- Keep pages short and opinionated. Link to official docs instead of copying them.
