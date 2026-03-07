#!/bin/bash
# Notification: Show a desktop notification for Claude Code events.
# Receives JSON on stdin with hook_event_name, cwd, and event-specific fields.
#
# Register in ~/.claude/settings.json for Stop, PermissionRequest, and Notification:
#   "Stop": [{
#     "matcher": "",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }]
#   }],
#   "PermissionRequest": [{
#     "matcher": "",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }]
#   }],
#   "Notification": [{
#     "matcher": "",
#     "hooks": [{ "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }]
#   }]
#
# Platform-specific:
#   Windows - calls notify.ps1 (requires BurntToast module)
#   macOS   - uses osascript (built-in)
#   Linux   - uses notify-send (install: apt install libnotify-bin)

INPUT=$(cat)

# Extract fields using Python (jq not available on Windows Git Bash)
eval "$(echo "$INPUT" | python -c "
import sys, json, os, shlex

d = json.load(sys.stdin)
cwd = d.get('cwd', '')
project = os.path.basename(cwd) if cwd else 'Claude Code'
event = d.get('hook_event_name', '')

if event == 'Stop':
    title = project
    message = 'Claude finished'
elif event == 'PermissionRequest':
    tool = d.get('tool_name', 'Unknown')
    tool_input = d.get('tool_input', {})
    # Extract a short description based on tool type
    if tool == 'Bash':
        detail = tool_input.get('command', '')[:60]
        message = f'Permission needed: {tool} - {detail}'
    elif tool in ('Edit', 'Write'):
        path = tool_input.get('file_path', '')
        filename = os.path.basename(path) if path else ''
        message = f'Permission needed: {tool} {filename}'
    else:
        message = f'Permission needed: {tool}'
    title = project
elif event == 'Notification':
    title = d.get('title', '') or project
    message = d.get('message', 'Needs your attention')
else:
    title = project
    message = d.get('message', 'Needs your attention')

print(f'TITLE={shlex.quote(title)}')
print(f'MESSAGE={shlex.quote(message)}')
print(f'CWD={shlex.quote(cwd)}')
" 2>/dev/null || echo "TITLE='Claude Code'; MESSAGE='Needs your attention'; CWD=''")"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    # Windows (Git Bash) - delegate to PowerShell + BurntToast
    powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/notify.ps1" -Title "$TITLE" -Message "$MESSAGE" -Cwd "$CWD"
    ;;
  Darwin)
    # macOS - native notification
    osascript -e "display notification \"$MESSAGE\" with title \"$TITLE\""
    ;;
  Linux)
    # Linux - notify-send
    notify-send "$TITLE" "$MESSAGE" 2>/dev/null
    ;;
esac

exit 0
