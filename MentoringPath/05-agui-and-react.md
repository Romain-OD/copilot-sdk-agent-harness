# 05 — AG-UI and React

Goal: stream any `AIAgent` to a browser with an open protocol instead of a custom WebSocket.

[AG-UI](https://docs.ag-ui.com) is an event protocol between agent backends and UIs: `RUN_STARTED`, `TEXT_MESSAGE_CONTENT` (deltas),
`TOOL_CALL_*`, `RUN_FINISHED`, `RUN_ERROR`… over Server-Sent Events.

## Server: two lines

```csharp
builder.Services.AddAGUIServer();
app.MapAGUIServer("/agui/{profile}", ctx =>
    ctx.RequestServices.GetRequiredService<AgentFactory>().Create((string)ctx.Request.RouteValues["profile"]!));
```

A new agent per request keeps runs isolated; the client sends the full message history each time.
`Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` is **prerelease**: pin the version.

## Client: no framework needed

[`Chat.tsx`](../src/web/src/Chat.tsx) uses `@ag-ui/client` directly:

```ts
const agent = new HttpAgent({ url: `/agui/${profile}` })
agent.subscribe({ onMessagesChanged: ({ messages }) => setMessages([...messages]) })
agent.addMessage({ id: crypto.randomUUID(), role: 'user', content: input })
await agent.runAgent()
```

CopilotKit builds on the same protocol and adds ready-made chat components, but wants its own runtime. For a harness, the raw client is enough.

## Why React, not Blazor

The AG-UI clients are TypeScript. In Blazor you would wrap them in JS interop or re-implement the protocol. React gets them for free.
For a .NET team that is the honest trade-off: a little TypeScript, a lot less plumbing.

## Exercise

1. `curl -N -X POST http://localhost:5180/agui/copilot -H "Content-Type: application/json" -d '{"threadId":"t1","runId":"r1","messages":[{"id":"1","role":"user","content":"hi"}],"tools":[],"context":[],"state":{},"forwardedProps":{}}'` and read the raw events.
2. Show tool calls in the chat: subscribe to `onToolCallStartEvent` and render a chip per tool.
3. Display the token usage that arrives with `RUN_FINISHED`.
