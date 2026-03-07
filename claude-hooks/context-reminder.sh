#!/bin/bash
# SessionStart (compact): Re-inject key context after conversation compaction.
# This helps Claude remember important conventions that may be lost in summarization.
#
# Register in ~/.claude/settings.json:
#   "SessionStart": [{
#     "matcher": "compact",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/context-reminder.sh" }]
#   }]

cat << 'EOF'
## Post-Compaction Reminders

Context was just compacted. Key conventions to remember:

1. **Git**: Use merge commits (not squash). Include Co-Authored-By trailer.
2. **PRs**: Run `gh pr checks --watch` after creating PRs.
3. **Copilot Review**: Run dismissals by user before applying.
4. **Project Context**: Re-read CLAUDE.md if unsure about project conventions.

If working on a specific project, read its CLAUDE.md now to restore full context.
EOF

exit 0
