# Claude Code Hooks

Pre-built hooks for [Claude Code](https://docs.anthropic.com/en/docs/claude-code)
that enforce development conventions from AGENTS.md.

## Architecture

Claude Code has two safety layers that work together:

- **Allows** (in `settings.json`) are coarse — they match command prefixes like
  `Bash(git push:*)` to auto-approve common commands without prompting
- **Hooks** are precise — they inspect the full command string with regex to
  block specific dangerous patterns

This means you can keep convenient auto-approvals while still catching
`git push --force` or `git add .env`.

### Hook Design Pattern

For flag detection, use a **two-step regex** — first confirm the command type,
then check for dangerous flags separately. Single-pass regexes like
`git\s+push\s+.*--force` can fail when `\s+` consumes characters needed by
later patterns.

```bash
# Good: two-step
if echo "$COMMAND" | grep -qE 'git\s+push\b'; then
  if echo "$COMMAND" | grep -qE '\s--force|\s-f(\s|$)'; then
    # block
  fi
fi

# Bad: single-pass (misses "git push -f origin main")
if echo "$COMMAND" | grep -qE 'git\s+push\s+.*(--force|\s-f(\s|$))'; then
  # block
fi
```

## Hooks

| File | Event | Purpose |
|------|-------|---------|
| `guardrail.sh` | PreToolUse (Bash) | Blocks force push, squash/rebase merge, secrets staging, destructive git/rm |
| `copilot-reminder.sh` | PostToolUse (Bash) | After `git push`, reminds to check Copilot review status |
| `pre-compact.sh` | PreCompact | Captures git state (branch, changes, commits) before context compaction |
| `context-reminder.sh` | SessionStart (compact) | Re-injects key conventions after compaction |
| `setup-path.sh` | SessionStart | Adds CLI tools to PATH via `CLAUDE_ENV_FILE` |
| `notify.sh` / `notify.ps1` | Stop, PermissionRequest, Notification | Desktop notifications with focus detection and click-to-refocus (Windows/macOS/Linux) |

## Setup

### Quick Start

Copy hooks to your Claude Code config directory and register them:

```bash
# Copy all hooks
cp .toolbox/claude-hooks/*.sh ~/.claude/hooks/
cp .toolbox/claude-hooks/*.ps1 ~/.claude/hooks/   # Windows only
chmod +x ~/.claude/hooks/*.sh
```

Then add the hook registrations to `~/.claude/settings.json` (see
[Settings Configuration](#settings-configuration) below).

### Windows: Configure Shell

On Windows, Claude Code uses `cmd.exe` by default, which can't run `.sh` scripts.
Add `CLAUDE_CODE_SHELL` to your settings to use Git Bash instead.
Adjust the path if Git is installed elsewhere (`where bash` to find it):

```json
{
  "env": {
    "CLAUDE_CODE_SHELL": "C:\\Program Files\\Git\\bin\\bash.exe"
  }
}
```

This is a top-level key in `settings.json`, not inside `hooks`. Requires Claude Code
restart to take effect.

### Settings Configuration

Add to the `"hooks"` key in `~/.claude/settings.json`:

```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/setup-path.sh" }
        ]
      },
      {
        "matcher": "compact",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/context-reminder.sh" }
        ]
      }
    ],
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/guardrail.sh", "timeout": 5 }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/copilot-reminder.sh", "timeout": 5 }
        ]
      }
    ],
    "PreCompact": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/pre-compact.sh", "timeout": 10 }
        ]
      }
    ],
    "Stop": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }
        ]
      }
    ],
    "PermissionRequest": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }
        ]
      }
    ],
    "Notification": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": "~/.claude/hooks/notify.sh", "timeout": 15 }
        ]
      }
    ]
  }
}
```

### Customization

**guardrail.sh**: Edit `PROJECT_ROOT` and `PROJECT_ROOT_WIN` at the top to
match your workspace directory. The defaults use `/c/Dev` and `C:\Dev`.

**setup-path.sh**: Add or remove tool paths for your machine. Commented
examples are included.

**notify.sh / notify.ps1**: Enriched toast notifications with event-specific messages:

- **Stop**: "Claude finished" with project folder name as title
- **PermissionRequest**: "Permission needed: Edit foo.ts" with tool context
- **Notification**: Passes through `message`; uses `title` if present, otherwise project folder name

Features (Windows):
- **Focus detection** — suppressed when this project's VSCode window is focused; fires when you're in another window
- **Click-to-refocus** — clicking the toast opens the project's VSCode window via `vscode://` protocol
- **Custom icon** — set `$iconPath` in `notify.ps1` to a local PNG

Windows requires the [BurntToast](https://github.com/Windos/BurntToast) PowerShell module:

```powershell
Install-Module -Name BurntToast -Scope CurrentUser -Force
```

macOS and Linux use built-in notification tools (`osascript` and `notify-send`).

**VSCode note**: The `Notification` hook does not fire in the VSCode extension. Use `Stop` and `PermissionRequest` instead — they work in both the CLI and VSCode extension.

## How Hooks Work

| Event | When | stdin | stdout | stderr | Exit codes |
|-------|------|-------|--------|--------|------------|
| SessionStart | Session start or after compaction | JSON with `cwd`, `trigger` | Added to context | Ignored | 0 = ok |
| PreToolUse | Before a tool runs | JSON with `tool_name`, `tool_input` | Added to context | Shown to Claude on block | 0 = ok, 2 = block |
| PostToolUse | After a tool runs | JSON with `tool_name`, `tool_input` | Added to context | Ignored | 0 = ok |
| PreCompact | Before context compaction | JSON with `cwd`, `trigger` | Added to context | Ignored | 0 = ok |
| Stop | Claude finishes responding | JSON with `cwd`, `stop_hook_active` | Ignored | Ignored | 0 = ok |
| PermissionRequest | Permission prompt shown | JSON with `cwd`, `tool_name`, `tool_input` | Ignored | Ignored | 0 = ok |
| Notification | Idle or permission prompt | JSON with `message`, `notification_type` | Ignored | Ignored | 0 = ok |

The `matcher` field in settings is a regex matched against the tool name
(e.g., `"Bash"` matches the Bash tool). An empty string matches all tools/events.

## Guardrail Coverage

The guardrail hook enforces these AGENTS.md rules:

| # | Guard | Rule |
|---|-------|------|
| 1 | Force push | "Never force-push to shared branches" |
| 2 | PR merge strategy | "Always use merge commits (gh pr merge --merge)" |
| 3 | Secrets staging | Security Checklist: "No secrets in code or logs" |
| 4 | Destructive git | Blocks `git reset --hard`, `git clean -f` |
| 5 | Destructive rm | Blocks `rm -rf` on root, home, and project directories |

### What's Not Guarded (and Why)

- **`--no-verify`**: Claude's system prompt already forbids skipping hooks
  unless the user asks. A guardrail here would block legitimate use cases
  (broken pre-commit hooks, WIP commits).
- **`git commit --amend`**: Only dangerous after push, but detecting "has this
  commit been pushed?" requires running git commands inside the hook, adding
  latency to every commit.
