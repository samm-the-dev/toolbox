# Claude Code Context

Claude-specific configuration that extends the cross-tool AGENTS.md guidance.

@AGENTS.md

---

## Claude-Specific Notes

### Co-Author Attribution

For AI-assisted commits, use:
```
Co-Authored-By: Claude <noreply@anthropic.com>
```

### Hooks

Claude Code hooks enforce AGENTS.md conventions automatically. See [hooks/README.md](../hooks/README.md) for the full reference.

**Key hooks:**
- **guardrail.sh** (PreToolUse) — blocks force push, secrets staging, destructive git/rm commands
- **copilot-reminder.sh** (PostToolUse) — post-push Copilot review reminder
- **pre-compact.sh** (PreCompact) — captures git state before compaction
- **context-reminder.sh** (SessionStart) — re-injects conventions after compaction
- **setup-path.sh** (SessionStart) — adds CLI tools to PATH
- **notify.sh** (Notification) — desktop notifications (Windows/macOS/Linux)

Hooks live in `~/.claude/hooks/` (user-global) and are registered in `~/.claude/settings.json`.

**Architecture:** Allows in settings are coarse (command prefix like `Bash(git push:*)`), hooks are precise (regex on full command). This lets you keep convenient auto-approvals while blocking specific dangerous patterns.

**Maintenance:** Periodically audit `~/.claude/settings.json` for accumulated one-off allows — they persist across sessions and can silently bypass guardrail hooks.

**Windows shell behavior:** `CLAUDE_CODE_SHELL` controls which shell the **Bash tool** uses — it does NOT affect standalone hook execution. Lifecycle hooks (PreCompact, Notification, SessionStart) resolve `bash` via the system PATH.

**Setup:** Ensure Git Bash's `bin` directory is on the Windows user PATH so that `bash` is globally available. Then use bare `.sh` paths in hook commands — no shell wrapper needed:

```json
"command": "~/.claude/hooks/my-hook.sh"
```

PreToolUse/PostToolUse hooks don't need this — they execute within the Bash tool context which already uses `CLAUDE_CODE_SHELL`.

**What doesn't work:** `CLAUDE_CODE_GIT_BASH_PATH` env var (not picked up by hook runner), `"%CLAUDE_CODE_SHELL%"` wrapper (cmd.exe can't reliably expand env vars in JSON command strings).

### Tool Setup (Claude Code)

The `gh` CLI should be available in PATH for PR management, review workflows, and GraphQL API access. The `setup-path.sh` hook handles this automatically by extending PATH on session start via `CLAUDE_ENV_FILE`.

### Import Syntax

Claude Code and SKILL.md files use `@path/to/file` syntax for imports:
- Paths are relative to the file containing the import
- Regular markdown links (`[text](url)`) are just text — not followed
- Max import depth: 5 hops
- Ignored inside fenced code blocks (no collision with `@scope/package` names)

### copilot-instructions.md Sync

GitHub Copilot does not support `@` imports or submodule references — it only reads `.github/copilot-instructions.md` from each repo. Projects copy the base template and append project-specific sections below a `---` separator. When the base template changes, propagate updates to the base portion (above the separator) in each project without overwriting project-specific content below it.

Only add "Do Flag" items for behaviors Copilot misses — don't list things it catches naturally (e.g., injection, null access). Test by observing Copilot reviews before adding new rules.
