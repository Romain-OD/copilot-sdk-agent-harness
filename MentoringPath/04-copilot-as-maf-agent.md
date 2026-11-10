# 04 — Copilot as a MAF agent

Goal: one abstraction, two engines. This is the core design choice of the harness.

`Microsoft.Agents.AI.GitHub.Copilot` turns a Copilot SDK session config into a MAF `AIAgent`:

```csharp
AIAgent agent = copilotClient.AsAIAgent(sessionConfig, ownsClient: false, id, name, description);
```

Now both engines go through the same pipeline:

```csharp
var engine = profile.Engine == "maf" ? CreateMaf(...) : CreateCopilot(...);
return engine.AsBuilder().Use(RecordAsync, RecordStreamingAsync).Build();   // agent middleware: the recorder
```

No `IEngine` interface, no plugin registry. A third engine is one more `case`.

## Two kinds of middleware

| Level | Sees | Harness uses it for |
|---|---|---|
| Agent run (`Use(runFunc, runStreamingFunc)`) | Messages in, response/updates out | Recorder: start, message, usage, cost, latency |
| Function (`Use((agent, ctx, next, ct) => …)`) | Each tool call | MAF policy gate |

The Copilot engine runs its tools inside the CLI, so MAF function middleware never sees them: the gate lives in `OnPreToolUse` instead.
Same policy object, two attachment points. This is the one place where the engines differ.

## Exercise

1. Set a breakpoint in `RecordStreamingAsync` and chat with each engine. Compare the `AgentResponseUpdate.Contents` types you get.
2. Add a `maxTurnMs` field to profiles: the recorder cancels runs that take longer. Test it with the fake `IChatClient`.
3. Explain in two sentences why the recorder is agent middleware and not an AG-UI concern.
