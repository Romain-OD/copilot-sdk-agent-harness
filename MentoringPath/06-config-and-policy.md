# 06 — Config and policy

Goal: change agent behavior without recompiling, and make dangerous behavior impossible rather than discouraged.

## One file

[`harness.yaml`](../src/Harness.Api/harness.yaml) holds profiles, policy and evals. [`harness.schema.json`](../src/Harness.Api/harness.schema.json)
gives completion and red squiggles in VS Code (YAML extension) and Rider. The server validates again on load:

- unknown keys are errors (a typo must not silently disable a rule)
- regexes are compiled at load time
- a broken edit keeps the last valid config, and the UI shows the error

Edits hot-reload: the next run uses the new file. No restart.

## Policy

```yaml
policy:
  default: ask
  rules:
    - tool: "view"
      decision: allow
    - tool: "*"
      args: "rm -rf|Remove-Item|git push|DROP TABLE"
      decision: deny
```

- `tool` is a glob on the tool name, `args` a regex on the JSON arguments (1 s timeout).
- **First matching rule wins**, so put specific rules first.
- A profile's `policy:` replaces the global one entirely (no merging: explicit beats clever).
- `ask` parks the call and shows an approval card. No answer in 5 minutes = deny. In evals, `ask` = deny.

## Exercise

1. Write a failing test in `PolicyTests` for a rule you want: e.g. `edit` on `*.csproj` must be denied. Then add the rule to `harness.yaml`.
2. Break `harness.yaml` on purpose (bad regex) from the Config tab. What does the UI show? Which config do runs use meanwhile?
3. Add a `maxToolCalls` profile setting: the gate denies after N calls in one run. Update the schema, `Validate()` and a test.
