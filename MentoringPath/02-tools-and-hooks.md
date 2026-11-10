# 02 — Tools and hooks

Goal: control what the agent may do, at the moment it tries to do it.

## Instructions are wishes, hooks are gates

A system prompt saying "never delete files" is a wish. The model can ignore it. A hook runs **before** the tool executes, in your code.

The Copilot SDK gives you two places to say no:

| Mechanism | When | Used here |
|---|---|---|
| `OnPermissionRequest` | The CLI asks "may I write/run/fetch?" for sensitive tools | Set to `ApproveAll` **because** the hook below decides everything |
| `Hooks.OnPreToolUse` | Before **every** tool call, with tool name and JSON args | The harness policy gate |
| `Hooks.OnPostToolUse` | After the tool, with its result | The flight recorder |

```csharp
OnPreToolUse = async (input, _) =>
{
    var ok = await approvals.CheckAsync(policy, current.Run, input.ToolName, input.ToolArgs?.GetRawText(), CancellationToken.None);
    return new PreToolUseHookOutput { PermissionDecision = ok ? "allow" : "deny", PermissionDecisionReason = ok ? null : "Denied by harness policy" };
},
```

A denied call is not an error: the model receives the reason and usually explains or tries something else. That is what you want.

## Built-in tools

The Copilot CLI brings its own tools (`view`, `glob`, `grep`, `create`, `edit`, shell…) scoped to `WorkingDirectory`.
The harness points them at [`src/Harness.Api/workspace`](../src/Harness.Api/workspace). Your own tools: `SessionConfig.Tools = [AIFunctionFactory.Create(MyMethod)]`, or MCP servers (chapter 09). The same hooks gate them.

## Exercise

1. Use the `copilot-readonly` profile and ask it to create `hello.txt`. Find the `deny` event in the run log.
2. Add a policy rule that sends any `create` call to `ask`. Create a file again: approve it from the UI card. Then deny one.
3. Read [`Policy.cs`](../src/Harness.Api/Policy.cs): what happens to an `ask` if nobody answers? Which test proves that evals never wait for a human?
