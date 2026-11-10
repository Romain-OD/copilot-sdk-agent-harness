# harness.yaml reference

Schema: [`harness.schema.json`](../src/Harness.Api/harness.schema.json). Server-side checks: `HarnessConfig.Validate()` in [`HarnessConfig.cs`](../src/Harness.Api/HarnessConfig.cs).
Add `# yaml-language-server: $schema=harness.schema.json` at the top for completion in VS Code.

## Top level

| Key | Type | Notes |
|---|---|---|
| `defaultProfile` | string | Must name a profile |
| `policy` | [policy](#policy) | Global; a profile `policy` replaces it |
| `profiles` | map name → [profile](#profile) | At least one |
| `evals` | list of [eval](#eval) | Run with `dotnet run --project src/Harness.Api -- eval [profile]` |

## profile

| Key | Default | Notes |
|---|---|---|
| `engine` | `copilot` | `copilot` (Copilot SDK) or `maf` (MAF HarnessAgent) |
| `model` | Copilot default | Required for `maf`. Copilot ids: `GET /api/models` |
| `systemPrompt` | — | Copilot: appended to Copilot's prompt. MAF: `Instructions` |
| `workingDirectory` | process cwd | Copilot tools are scoped here. Relative to `src/Harness.Api` |
| `provider` | Copilot subscription | BYOK: `{ type, baseUrl, apiKeyEnv }`. `type`: `openai` \| `azure` \| `anthropic`; `maf` supports `openai` (any OpenAI-compatible endpoint) |
| `mcpServers` | — | map name → `{ command, args }` (stdio) or `{ url }` (HTTP), plus `tools` allow-list |
| `policy` | global policy | See below |
| `disable` | — | MAF only: `webSearch`, `fileMemory`, `todos`, `agentMode`, `skills`, `compaction` |
| `pricing` | — | `{ inputPer1M, outputPer1M }` in USD, for the cost shown per run |

## policy

| Key | Default | Notes |
|---|---|---|
| `default` | `ask` | `allow` \| `deny` \| `ask` when no rule matches |
| `rules[].tool` | `*` | Glob on tool name (`view`, `mcp_*`) |
| `rules[].args` | — | Regex on the JSON arguments, case-insensitive, 1 s timeout |
| `rules[].decision` | — | `allow` \| `deny` \| `ask`. First match wins |

`ask` waits for the UI (5 min, then deny). Evals run unattended: `ask` = deny.

## eval

| Key | Notes |
|---|---|
| `name`, `prompt` | Required |
| `profiles` | Empty = every profile |
| `contains` / `notContains` | Case-insensitive substrings of the final answer |
| `toolsCalled` | Each tool (glob) must have been called and allowed |
| `noDeniedTools` | Fail if the policy denied any call |
