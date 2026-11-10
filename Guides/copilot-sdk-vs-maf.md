# Copilot SDK vs Microsoft Agent Framework

They are not competitors. The Copilot SDK is an **agent runtime** (Copilot's loop, tools and model access).
MAF is an **agent framework** (abstractions, middleware, hosting, workflows) that can host the Copilot SDK as one of its agents.

| | Copilot SDK | MAF (`HarnessAgent` / `ChatClientAgent`) |
|---|---|---|
| Agent loop | Inside the Copilot CLI process | In-process, .NET |
| Built-in tools | File, shell, search, web… (the Copilot CLI toolset) | None by default; the Harness adds memory, todos, modes, skills |
| Models | Copilot subscription models, or BYOK (OpenAI, Azure, Anthropic) | Any `IChatClient` (OpenAI, Azure, Ollama, Foundry, GitHub Models…) |
| Billing | Copilot premium requests (or your BYOK bill) | Your provider's bill |
| Tool gating | `OnPreToolUse` hook, `OnPermissionRequest` | Function middleware, `ToolApprovalAgent` |
| Telemetry | `CopilotClientOptions.Telemetry` (OTLP) | `UseOpenTelemetry()` / ActivitySources |
| Best for | Coding agents that act on a repo, with Copilot's proven loop | Business agents with your own tools, data and workflows |

## Rule of thumb

- The agent edits code, runs builds, explores a repo → **Copilot SDK**.
- The agent calls your APIs, follows a business process, must run on your model contract → **MAF**.
- You want both behind one UI, one policy and one recorder → wrap Copilot with `AsAIAgent()` and treat both as MAF agents. That's this repo.

## Links

- [Copilot SDK](https://github.com/github/copilot-sdk)
- [Microsoft Agent Framework docs](https://learn.microsoft.com/agent-framework/)
- [MAF source](https://github.com/microsoft/agent-framework)
