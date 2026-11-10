# 09 — Production

Goal: know exactly what stands between this learning harness and something you'd let a team use.

## Sandbox the tools

The Copilot CLI's shell and file tools are real. `workingDirectory` scopes them, a policy filters them, but neither is a sandbox.
Before running untrusted prompts:

- run the API in a container with only the workspace mounted, no host credentials, and a non-root user
- keep `policy.default: deny` and allow-list tools
- deny network-capable tools unless the task needs them

## Secrets

- Keys only via `apiKeyEnv`. Copilot SDK BYOK accepts keys, not Entra ID tokens.
- In the cloud, load them from Key Vault into environment variables at startup.

## MCP servers

```yaml
profiles:
  copilot-docs:
    engine: copilot
    mcpServers:
      learn: { url: "https://learn.microsoft.com/api/mcp", tools: ["*"] }
    policy:
      default: deny
      rules: [{ tool: "microsoft_docs_*", decision: allow }]
```

Each MCP server is code you run or a service you trust. Allow-list tool names; `tools: ["*"]` only for servers you control.

## Before hosting it

| Missing on purpose | Add |
|---|---|
| Auth | ASP.NET Core authentication + an authorization policy on `/api` and `/agui` |
| Approvals across instances | `Approvals` is in-memory: move it to a shared store |
| Run storage | JSONL on disk: move to blob storage or SQLite when you need queries |
| Rate limits | `AddRateLimiter` per user |

## Exercise

1. Write a `Dockerfile` for `Harness.Api` that mounts only `workspace/` and runs as non-root. Verify a denied `view` of `/etc/passwd`.
2. Add the `copilot-docs` profile above and ask it a .NET question. Which MCP tools were called?
3. Add an API key check on `/api` and `/agui` with a minimal endpoint filter.
