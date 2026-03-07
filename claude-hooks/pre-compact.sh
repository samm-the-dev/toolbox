#!/bin/bash
# PreCompact: Output session state so the compaction summary has better material.
# Stdout is added to context before compaction runs.
#
# Register in ~/.claude/settings.json:
#   "PreCompact": [{
#     "matcher": "",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/pre-compact.sh", "timeout": 10 }]
#   }]

INPUT=$(cat)

CWD=$(echo "$INPUT" | python -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('cwd', ''))
" 2>/dev/null)

TRIGGER=$(echo "$INPUT" | python -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('trigger', 'unknown'))
" 2>/dev/null)

echo "## Pre-Compaction State Snapshot"
echo "Trigger: $TRIGGER"
echo ""

# If we're in a git repo, capture working state
if [ -n "$CWD" ] && [ -d "$CWD/.git" ]; then
  BRANCH=$(git -C "$CWD" branch --show-current 2>/dev/null)
  echo "**Working directory:** $CWD"
  echo "**Branch:** $BRANCH"
  echo ""

  STATUS=$(git -C "$CWD" status --short 2>/dev/null)
  if [ -n "$STATUS" ]; then
    echo "**Uncommitted changes:**"
    echo "$STATUS"
  else
    echo "**Working tree:** clean"
  fi
  echo ""

  echo "**Recent commits:**"
  git -C "$CWD" log --oneline -5 2>/dev/null
fi

exit 0
