# Pitfalls

Things that cost time while building this repo, with the fix.

| Symptom | Cause | Fix |
|---|---|---|
| Session creation fails for a model you saw in a blog post | Model ids depend on your plan and change often | `GET /api/models` (needs `client.StartAsync()` before `ListModelsAsync`) |
| Build error `MAAI001` | MAF Harness APIs are marked experimental | `<NoWarn>$(NoWarn);MAAI001</NoWarn>` with a comment, not a blanket suppression |
| AG-UI package not found | `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` is prerelease only | Pin the exact `-preview` version |
| Tool calls refused or session errors about permissions | No `OnPermissionRequest` handler | Always set one; let `OnPreToolUse` be the real gate |
| Policy never triggers on the Copilot engine | Expecting MAF function middleware to see Copilot tools | Copilot tools run in the CLI: gate them in `OnPreToolUse` |
| MAF engine returns garbage or "OK" from GitHub Models | A corporate proxy intercepts `models.github.ai` | Use another OpenAI-compatible endpoint, or test with a fake `IChatClient` |
| BYOK with Entra ID fails | Copilot SDK BYOK takes API keys only | Use a key from Key Vault, or use the `maf` engine with Azure credentials |
| `dotnet build` fails with a locked DLL | The API is still running | Stop `Harness.Api` first |
| Edits to `harness.yaml` seem ignored | The edit is invalid; last valid config is kept | Read the red banner in the UI, or `GET /api/profiles` → `configError` |
| A typo like `modle:` does nothing | — | It fails loudly: unknown keys are rejected on purpose |
| Hosted web search error on the `maf` profile | `HarnessAgent` web search needs the OpenAI Responses API | `disable: [webSearch]` for chat-completions endpoints |
