#!/bin/bash
# PreToolUse guardrail for Bash commands.
# Blocks dangerous patterns documented in AGENTS.md.
# Exit 2 = block the tool call (stderr message shown to Claude).
#
# Register in ~/.claude/settings.json:
#   "PreToolUse": [{
#     "matcher": "Bash",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/guardrail.sh", "timeout": 5 }]
#   }]

INPUT=$(cat)

# Extract command using Python (jq not available on Windows Git Bash)
COMMAND=$(echo "$INPUT" | python -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('command', ''))
" 2>/dev/null)

[ -z "$COMMAND" ] && exit 0

# --- Configuration ---
# Customize PROJECT_ROOT to match your workspace layout.
# Used by the rm -rf guard to prevent deleting entire projects.
PROJECT_ROOT="/c/Dev"           # Unix-style (Git Bash, WSL)
PROJECT_ROOT_WIN='C:\\Dev'      # Windows-style (PowerShell)

# --- 1. Force push guard ---
# AGENTS.md: "Never force-push to shared branches"
# Two-step: confirm it's git push, then check for force flags
if echo "$COMMAND" | grep -qE 'git\s+push\b'; then
  if echo "$COMMAND" | grep -qE '\s--force|\s-f(\s|$)|--force-with-lease'; then
    echo "BLOCKED: Force push." >&2
    echo "AGENTS.md: 'Never force-push to shared branches.'" >&2
    echo "If the user explicitly requested this, they can run it manually." >&2
    exit 2
  fi
fi

# --- 2. PR merge strategy guard ---
# AGENTS.md: "Always use merge commits... Never use --squash or --rebase"
if echo "$COMMAND" | grep -qE 'gh\s+pr\s+merge\s+.*(--squash|--rebase)'; then
  echo "BLOCKED: Non-merge PR strategy." >&2
  echo "AGENTS.md: 'Always use merge commits (gh pr merge --merge).'" >&2
  exit 2
fi

# --- 3. Secrets in staging guard ---
# AGENTS.md Security Checklist: "No secrets in code or logs"
# Block git add on known sensitive file patterns
if echo "$COMMAND" | grep -qE 'git\s+add\s+.*\.(env|pem|key|p12|pfx|keystore)(\s|$|\.local|\.prod|\.dev)'; then
  echo "BLOCKED: Sensitive file detected in git add." >&2
  echo "AGENTS.md Security Checklist: 'No secrets in code or logs.'" >&2
  echo "Review the file before staging manually." >&2
  exit 2
fi
if echo "$COMMAND" | grep -qiE 'git\s+add\s+.*(credentials|secret[s]?\.json|token[s]?\.json)'; then
  echo "BLOCKED: Possible secrets file in git add." >&2
  echo "Review the file before staging manually." >&2
  exit 2
fi

# --- 4. Destructive git guard ---
# Block git reset --hard (discards uncommitted work)
if echo "$COMMAND" | grep -qE 'git\s+reset\b'; then
  if echo "$COMMAND" | grep -qE '\s--hard(\s|$)'; then
    echo "BLOCKED: git reset --hard discards uncommitted changes." >&2
    echo "Use --soft or --mixed to preserve work. Run manually if intended." >&2
    exit 2
  fi
fi
# Block git clean -f (permanently deletes untracked files)
if echo "$COMMAND" | grep -qE 'git\s+clean\b'; then
  if echo "$COMMAND" | grep -qE '\s-[a-zA-Z]*f'; then
    echo "BLOCKED: git clean -f permanently deletes untracked files." >&2
    echo "Run manually if intended." >&2
    exit 2
  fi
fi

# --- 5. Destructive rm guard ---
# Block rm -rf on catastrophic targets (root, home, user dirs)
if echo "$COMMAND" | grep -qE 'rm\s+-(rf|fr|r\s+-f)\s+(/|/c/?|~|/c/Users)\s*$'; then
  echo "BLOCKED: Catastrophic rm -rf target." >&2
  exit 2
fi
# Block rm -rf on project root directories (top-level project, no deeper path)
if echo "$COMMAND" | grep -qiE "rm\\s+-(rf|fr|r\\s+-f)\\s+($PROJECT_ROOT/[^/\\s]+|${PROJECT_ROOT_WIN}\\\\\\\\?[^/\\\\\\s]+)\\s*\$"; then
  echo "BLOCKED: rm -rf on project root directory." >&2
  echo "This would delete an entire project. Run manually if intended." >&2
  exit 2
fi
# Block PowerShell Remove-Item -Recurse on project roots
if echo "$COMMAND" | grep -qiE "Remove-Item.*-Recurse.*(${PROJECT_ROOT_WIN}\\\\\\\\?[^\\\\]+|$PROJECT_ROOT/[^/]+)\\s*[\";]*\\s*\$"; then
  echo "BLOCKED: Recursive deletion of project root via PowerShell." >&2
  exit 2
fi

exit 0
