#!/bin/bash
# PostToolUse: After git push, remind Claude to check Copilot review status.
# Output goes to stdout and is added to Claude's conversation context.
#
# Register in ~/.claude/settings.json:
#   "PostToolUse": [{
#     "matcher": "Bash",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/copilot-reminder.sh", "timeout": 5 }]
#   }]

INPUT=$(cat)

COMMAND=$(echo "$INPUT" | python -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('command', ''))
" 2>/dev/null)

[ -z "$COMMAND" ] && exit 0

# Only trigger on git push commands
if echo "$COMMAND" | grep -qE 'git\s+push'; then
  cat << 'EOF'
POST-PUSH REMINDER: If this repo has Copilot auto-review enabled, wait for
Copilot to review the new commit before merging. Check with:
  gh api repos/OWNER/REPO/pulls/PR/reviews --jq 'map(select(.user.login | contains("copilot"))) | last | .commit_id'
Compare to HEAD. See AGENTS.md "Checking if Copilot Review is Complete."
EOF
fi

exit 0
