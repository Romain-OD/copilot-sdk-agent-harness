# Semantic Kernel → Microsoft Agent Framework

Semantic Kernel's agent stack evolved into MAF; new agent work should start on MAF. SK keeps getting fixes, so migrate when you touch the code, not in a big bang.
The [official migration guide](https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel/) is the source of truth; this page is the cheat sheet.

## Mapping

| Semantic Kernel | MAF |
|---|---|
| `using Microsoft.SemanticKernel(.Agents)` | `using Microsoft.Agents.AI; using Microsoft.Extensions.AI;` |
| `Kernel` required by every agent | No kernel. Agents wrap an `IChatClient` |
| `new ChatCompletionAgent { Kernel, Instructions }` | `chatClient.AsAIAgent(instructions: ..., tools: [...])` |
| `[KernelFunction]` + `KernelPlugin` + `kernel.Plugins.Add` | `AIFunctionFactory.Create(method)`; `[Description]` is optional |
| `AgentThread` types per provider | `await agent.CreateSessionAsync()` |
| `agent.InvokeAsync(...)` (streams items) | `agent.RunAsync(...)` → one `AgentResponse` |
| `agent.InvokeStreamingAsync(...)` | `agent.RunStreamingAsync(...)` → `AgentResponseUpdate` |
| `KernelArguments(PromptExecutionSettings)` | `ChatClientAgentRunOptions(new ChatOptions { ... })` |
| Function invocation filters | Function middleware: `agent.AsBuilder().Use((agent, ctx, next, ct) => ...)` |
| `services.AddKernel()` | `services.AddKeyedSingleton<AIAgent>(...)` |
| `ChatCompletionAgent` / `OpenAIAssistantAgent` / `AzureAIAgent` | One `ChatClientAgent` for all; hosted agents via provider `AsAIAgent` |

## Before / after

```csharp
// Semantic Kernel
var kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion(model, key).Build();
kernel.Plugins.AddFromType<WeatherPlugin>();
var agent = new ChatCompletionAgent { Kernel = kernel, Instructions = "Be brief." };
await foreach (var item in agent.InvokeAsync("Weather in Paris?")) Console.WriteLine(item.Message);

// MAF
AIAgent agent = new OpenAIClient(key).GetChatClient(model).AsIChatClient()
    .AsAIAgent(instructions: "Be brief.", tools: [AIFunctionFactory.Create(Weather.Get)]);
Console.WriteLine(await agent.RunAsync("Weather in Paris?"));
```

## Migration order that works

1. Move tools first: plain static methods + `AIFunctionFactory.Create`. They work with both stacks via `Microsoft.Extensions.AI`.
2. Replace the agent, keep prompts identical, and compare outputs (this harness's Compare tab or an eval).
3. Convert filters to middleware last, with a test per filter.
