# 01 — Copilot SDK hello

Goal: understand the three objects of the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) before the harness hides them.

| Object | Role |
|---|---|
| `CopilotClient` | Starts and owns the Copilot CLI process. One per app (a singleton in [`Program.cs`](../src/Harness.Api/Program.cs)). |
| `SessionConfig` | Everything about one conversation: `Model`, `SystemMessage`, `WorkingDirectory`, `Provider` (BYOK), `McpServers`, `Hooks`. |
| Session | The conversation. Sends prompts, emits events (deltas, tool start/complete, usage, idle). |

The agent loop (plan → call tool → read result → repeat) runs **inside the CLI**. You configure it; you don't write it.

## Smallest program

```csharp
#:package GitHub.Copilot.SDK@1.0.29
using GitHub.Copilot;

await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-haiku-4.5",
    OnPermissionRequest = PermissionHandler.ApproveAll, // fine for a hello world, never for real tools
});
var reply = await session.SendAndWaitAsync(new MessageOptions { Prompt = "What is a C# record? One sentence." });
Console.WriteLine(reply?.Data.Content);
```

Save as `hello.cs` and run `dotnet run hello.cs` (.NET 10 file-based apps). Method names move between SDK versions:
if one doesn't compile, hover the type in your IDE or read the XML docs shipped in the package.

## What the harness adds

[`AgentFactory.CreateCopilot`](../src/Harness.Api/AgentFactory.cs) builds the same `SessionConfig` from a YAML profile,
then wraps it as a MAF `AIAgent` (chapter 04). `SystemMessageMode.Append` keeps Copilot's own system prompt and adds yours.

## Exercise

1. Run the hello world above with your own model id.
2. Change it to stream: subscribe to session events and print only text deltas. Which event type carries usage?
3. In `harness.yaml`, add a profile `copilot-pirate` with a `systemPrompt` that answers like a pirate. Compare it with `copilot` in the Compare tab.
