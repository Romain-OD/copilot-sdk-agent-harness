#!/usr/bin/env bash
# Quality gate: when the agent tries to finish a turn with changes under src/, run the tests (and lint + build the UI if src/web changed).
# Red => block and send the failures back as the next prompt. No changes => allow, no cost.
cat > /dev/null
[ -z "$(git status --porcelain -- src)" ] && exit 0
out=$(dotnet test src/Harness.slnx --nologo 2>&1); code=$?
if [ $code -eq 0 ] && [ -n "$(git status --porcelain -- src/web)" ]; then
  out=$(cd src/web && npm run lint 2>&1 && npm run build 2>&1); code=$?
fi
[ $code -eq 0 ] && exit 0
jq -cn --arg t "$(printf '%s' "$out" | tail -n 30)" \
  '{decision:"block", reason:("Quality gate failed. Fix the production code (do not weaken, skip or delete tests), re-run the check, then finish.\n" + $t)}'
