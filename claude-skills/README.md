# Claude Code Skills

Claude Code skills (custom slash commands) for common workflows.

## Available Skills

| Skill | Description | Trigger |
|-------|-------------|---------|
| `pr-flow/` | Create PR with standard workflow (branch, commit, push, PR). Hands off to `/pr-check` for CI and reviews. | `/pr-flow` |
| `pr-check/` | Watch CI checks and triage review comments on an existing PR | `/pr-check` |
| `review-lessons/` | Audit memory and project files for insights to promote to shared guidance | `/review-lessons` |
| `travel-planning/` | Comprehensive travel planning covering flight logistics, local shopping discovery, and activity planning | `/travel-planning` |

## Installation

### Personal Skills (Recommended)

Install once, available in all projects. Best for general-purpose skills like `pr-flow`.

```bash
cp -r .toolbox/claude-skills/pr-flow ~/.claude/skills/
```

Skills in `~/.claude/skills/` are loaded for every project. Update manually when
toolbox changes (rare for stable skills).

### Project Skills

Install per-project. Best for project-specific workflows or when you want the
skill version-controlled with the project.

```bash
mkdir -p .claude/skills
cp -r .toolbox/claude-skills/pr-flow .claude/skills/
```

**Note:** Claude Code doesn't support loading skills from arbitrary paths (like
a submodule). You must copy to `.claude/skills/` or use symlinks.

## Usage

After installation, invoke with:

```
/pr-flow
/pr-flow feature/add-dark-mode
/pr-flow "Add user authentication"
```

## Creating Custom Skills

Skills are markdown files with YAML frontmatter:

```yaml
---
name: my-skill
description: What this skill does (shown in help, used for auto-invocation)
disable-model-invocation: true  # Only user can trigger (recommended for actions)
allowed-tools:
  - Bash(git *)
  - Bash(npm *)
---

# Instructions for Claude

Describe what the skill should do...
```

### Frontmatter Options

| Field | Purpose |
|-------|---------|
| `name` | Slash command name (defaults to directory name) |
| `description` | When to use; shown in `/help` |
| `disable-model-invocation` | `true` = only you can invoke |
| `user-invocable` | `false` = only Claude can invoke |
| `allowed-tools` | Tools Claude can use without permission |
| `context` | `fork` = run in isolated subagent |

### File Imports in Skills

SKILL.md supports the same `@path/to/file` import syntax as CLAUDE.md. Use it to
pull in supporting files bundled with the skill:

```markdown
## Reference

@reference.md
```

Paths resolve relative to the SKILL.md file. Imports are recursive (max 5 hops)
and ignored inside code blocks. Keep SKILL.md under 500 lines and move detailed
reference material to separate files loaded via `@`.

See [Claude Code docs](https://code.claude.com/docs/en/skills) for full reference.
