# 03 — MAF agents (and where Semantic Kernel went)

Goal: build agents with [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/), the successor of Semantic Kernel's agent stack.

## The abstraction ladder

```text
IChatClient          Microsoft.Extensions.AI: one model call (OpenAI, Azure, Ollama, GitHub Models…)
  └─ ChatClientAgent   MAF: instructions + tools + session = an agent      chatClient.AsAIAgent(...)
       └─ HarnessAgent   MAF Harness: + file memory, todos, agent modes, skills, compaction, approvals
                         chatClient.AsHarnessAgent(new HarnessAgentOptions { ... })
```

Every rung is an `AIAgent`: `RunAsync`, `RunStreamingAsync`, `CreateSessionAsync`. Hosting, middleware and the recorder only see `AIAgent`.

## The maf profile

[`AgentFactory.CreateMaf`](../src/Harness.Api/AgentFactory.cs) maps YAML to `HarnessAgentOptions`:

- `model`, `systemPrompt` → `ChatOptions { ModelId, Instructions }`
- `disable: [webSearch, todos, …]` → `Disable*` flags
- MAF's own `ToolApprovalAgent` auto-approves, because the harness policy runs as **function middleware** (`.AsBuilder().Use(...)`) right before each tool invocation

## Coming from Semantic Kernel

| Semantic Kernel | MAF |
|---|---|
| `Kernel` + `ChatCompletionAgent` | `chatClient.AsAIAgent(instructions, tools)` |
| `[KernelFunction]` + plugins | `AIFunctionFactory.Create(method)` |
| `AgentThread` | `AgentSession` (`agent.CreateSessionAsync()`) |
| `InvokeAsync` / `InvokeStreamingAsync` | `RunAsync` / `RunStreamingAsync` |
| Filters | Agent / function middleware |

Details: [Guides/sk-to-maf-migration.md](../Guides/sk-to-maf-migration.md) and the [official guide](https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel/).

## Exercise

1. Read `MafEngineTests` in [`HarnessTests.cs`](../src/Harness.Tests/HarnessTests.cs). It runs a `HarnessAgent` against a fake `IChatClient`. Why can this test run in CI without a model?
2. Add a test: with a policy that denies `*`, the fake tool is never invoked and the run log has a `deny` event.
3. Point the `maf` profile at an endpoint you have (OpenAI, Azure OpenAI's `/openai/v1`, Ollama's `/v1`) and run the same prompt in Compare against `copilot`.
