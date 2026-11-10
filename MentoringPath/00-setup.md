# 00 — Setup

Goal: the harness runs on your machine and you can read what an agent did.

## Install

- .NET SDK 10, Node 22+.
- A GitHub account with a Copilot plan. Sign in once: `gh auth login` (or `/login` in the Copilot CLI).
- The `GitHub.Copilot.SDK` NuGet package downloads the matching Copilot CLI binary at build time. The SDK talks to it over JSON-RPC; you never call it yourself.

```shell
cd src/web && npm ci && npm run build && cd ../..
dotnet test src/Harness.slnx
dotnet run --project src/Harness.Api      # http://localhost:5180
```

## Find your models

Model ids differ per account and change often. Ask the SDK instead of guessing:

```shell
curl http://localhost:5180/api/models
```

The Config tab lists them too. A wrong id fails when the first session is created, not at startup.

## BYOK (optional)

The Copilot SDK can call your own endpoint instead of the Copilot service: see the `copilot-ollama` profile in
[`harness.yaml`](../src/Harness.Api/harness.yaml). Keys come from environment variables named by `apiKeyEnv`, never from the file.

## Exercise

1. Start the harness, open the UI, ask the `copilot` profile: *"List the files in your working directory."*
2. Open `src/Harness.Api/runs/` and read the newest `.jsonl`. Find the `tool` event: which tool ran, what did the policy decide, and why?
3. Change `model:` of the `copilot` profile to another id from `/api/models`. Don't restart. Ask again and check the `start` event of the new run.
