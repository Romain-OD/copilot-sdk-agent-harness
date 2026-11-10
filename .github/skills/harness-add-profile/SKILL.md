---
name: harness-add-profile
description: Adds a new agent profile to harness.yaml (engine, model, provider/BYOK, MCP servers, policy) and proves it works with an eval. Use when asked to add, create or configure a profile, a model, a BYOK provider or an MCP server for the harness.
---

Follow these steps in order.

1. Read `src/Harness.Api/harness.yaml` and `Guides/harness-yaml-reference.md` to learn the existing profiles and allowed keys.
2. Restate the profile in one line: `name: engine / model / provider / tools allowed / tools denied`. If the model id is unknown, run the API and `GET /api/models` (copilot engine) instead of guessing.
3. Add the profile. Rules:
   - secrets only via `apiKeyEnv`, never inline;
   - start from `policy: { default: deny }` and allow-list tools when the profile has MCP servers or BYOK;
   - `maf` needs `model` and `provider.type: openai` with a `baseUrl`.
4. Add one eval in `evals:` with `profiles: [<new profile>]` that proves the profile's purpose (`contains`, `toolsCalled` or `noDeniedTools`).
5. Verify:
   - `dotnet test src/Harness.slnx --filter "FullyQualifiedName~ConfigTests"` (the sample config must stay valid);
   - `dotnet run --project src/Harness.Api -- eval <new profile>` if credentials are available; otherwise say so explicitly.
6. Report the profile, the eval, and both command results.
