# Copilot SDK Agent Harness

Learn the **[GitHub Copilot SDK](https://github.com/github/copilot-sdk)** and **[Microsoft Agent Framework](https://github.com/microsoft/agent-framework)**
(MAF, the successor of Semantic Kernel) by building a **configurable agent harness** in C# + React.

> The model is a commodity. The harness is the product. Gates, recordings and evals are what make an agent safe to ship.

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![stack](https://img.shields.io/badge/React-19-61DAFB) ![license](https://img.shields.io/badge/license-MIT-green)

## What makes this harness different

Most harnesses ship their own agent loop. This one ships **no agent loop**: both engines already are a MAF `AIAgent`.
The value is the **control plane** around them, driven by one YAML file.

| Feature | How |
|---|---|
| **One file drives everything** | [`harness.yaml`](src/Harness.Api/harness.yaml) + [JSON Schema](src/Harness.Api/harness.schema.json): profiles, models, BYOK, MCP servers, policy, evals. Hot reload; a broken edit keeps the last valid config. |
| **Two engines, one abstraction** | `engine: copilot` → Copilot SDK `AsAIAgent()`. `engine: maf` → MAF `HarnessAgent`. Same hosting, same recorder, same gates. |
| **Policy-as-code gates** | `allow / deny / ask` rules on tool name globs and argument regexes. Copilot: `OnPreToolUse` hook. MAF: function middleware. |
| **Human-in-the-loop** | `ask` parks the tool call; the UI shows an approval card. Unattended (evals) → deny. |
| **Flight recorder + replay** | Every run is a JSONL file: prompt, every tool decision with its reason, results, answer, tokens, cost, latency. Replay it step by step, or re-run it on another profile. |
| **Side-by-side compare** | Same prompt, two profiles: Copilot vs MAF, Claude vs GPT, strict vs loose policy. |
| **Evals in CI** | Assertions in YAML (`contains`, `notContains`, `toolsCalled`, `noDeniedTools`). `dotnet run -- eval` exits non-zero on failure. |
| **OpenTelemetry** | Copilot SDK + MAF + ASP.NET traces to any OTLP endpoint (Aspire dashboard in one `docker run`). |
| **AG-UI streaming** | `MapAGUIServer("/agui/{profile}")` + `@ag-ui/client` in React. Any AG-UI client can talk to any profile. |

## Architecture

```text
React (Vite) ── Chat · Compare · Runs/Replay · Config · Approval cards
   │  AG-UI over SSE  /agui/{profile}        REST /api/*
ASP.NET Core (net10.0)
   ├─ ConfigStore   harness.yaml → validated, hot-reloaded
   ├─ AgentFactory  profile → AIAgent    (a switch, not a plugin system)
   │     ├─ copilot: CopilotClient.AsAIAgent(SessionConfig{Hooks, Provider, McpServers})
   │     └─ maf:     IChatClient.AsHarnessAgent(HarnessAgentOptions) + function middleware
   ├─ Policy + Approvals   first matching rule wins; ask → human
   ├─ RunStore      runs/*.jsonl  (replay, compare, evals)
   └─ OpenTelemetry → OTLP
```

## Quickstart

Prerequisites: .NET SDK 10, Node 22+, a GitHub account with a Copilot plan, signed in once with `gh auth login` or the Copilot CLI `/login`.
The Copilot SDK package downloads the matching Copilot CLI binary at build time; nothing else to install.

```shell
cd src/web && npm ci && npm run build && cd ../..   # UI → src/Harness.Api/wwwroot
dotnet run --project src/Harness.Api                # http://localhost:5180
```

Dev loop with hot reload: `dotnet run --project src/Harness.Api` in one terminal, `cd src/web && npm run dev` in another (Vite proxies `/api` and `/agui`).

```shell
dotnet test src/Harness.slnx                        # unit tests, no network
dotnet run --project src/Harness.Api -- eval copilot # live evals, exit code 1 on failure
```

## Learn

| Section | What's inside |
|---|---|
| [Mentoring Path](MentoringPath/README.md) | 10 chapters from "hello Copilot SDK" to production, each with an exercise on `src/` |
| [Guides](Guides/README.md) | Copilot SDK vs MAF, Semantic Kernel → MAF migration, `harness.yaml` reference, pitfalls |

## Why C# + React (the stack challenge)

| Option | Verdict | Why |
|---|---|---|
| **C#** | ✅ | First-class Copilot SDK, MAF, `Microsoft.Agents.AI.Harness`, `Microsoft.Agents.AI.GitHub.Copilot` and AG-UI hosting. |
| Python | Kill switch | MAF + Copilot SDK exist too. Switch only if .NET blocks on a missing feature. |
| Rust | ❌ | No MAF. Agent loops wait on the model; Rust's speed buys nothing here. |
| **React** | ✅ | AG-UI's reference clients (`@ag-ui/client`, CopilotKit) and the richest component ecosystem. |
| Blazor | ❌ | No AG-UI client; rich agent UIs (diffs, terminals) would be JS interop anyway. |
| Vue | ❌ | Fine framework, no first-party AG-UI client. |
| Semantic Kernel | Migration chapter | SK's own README points new work to MAF. See [the migration guide](Guides/sk-to-maf-migration.md). |

## Versions tested

| Package | Version |
|---|---|
| GitHub.Copilot.SDK | 1.0.29 |
| Microsoft.Agents.AI.Harness / .GitHub.Copilot / .OpenAI | 1.28.0 |
| Microsoft.Agents.AI.Hosting.AGUI.AspNetCore | 1.28.0-preview.261106.1 (prerelease) |
| @ag-ui/client | 1.0.4 |
| .NET SDK / Node | 10.0.302 / 24 |

Both SDKs ship monthly. Expect renames; the [pitfalls guide](Guides/pitfalls.md) lists the ones already hit.

## Not included (on purpose)

No database (JSONL files), no auth (localhost tool: add auth before hosting it), no custom agent loop, no plugin system.
Shell and file tools are real: keep `workingDirectory` pointed at `src/Harness.Api/workspace` or run in a container ([chapter 09](MentoringPath/09-production.md)).

## License

[MIT](LICENSE)
