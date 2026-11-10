# Quality gate: when the agent tries to finish a turn with changes under src/, run the tests (and lint + build the UI if src/web changed).
# Red => block and send the failures back as the next prompt. No changes => allow, no cost.
[Console]::In.ReadToEnd() | Out-Null
if (-not (git status --porcelain -- src)) { exit 0 }
$out = dotnet test src/Harness.slnx --nologo 2>&1
if ($LASTEXITCODE -eq 0 -and (git status --porcelain -- src/web)) {
    Push-Location src/web
    $out = npm run lint 2>&1
    if ($LASTEXITCODE -eq 0) { $out = npm run build 2>&1 }
    Pop-Location
}
if ($LASTEXITCODE -eq 0) { exit 0 }
$tail = ($out | Select-Object -Last 30) -join "`n"
@{ decision = 'block'; reason = "Quality gate failed. Fix the production code (do not weaken, skip or delete tests), re-run the check, then finish.`n$tail" } | ConvertTo-Json -Compress
