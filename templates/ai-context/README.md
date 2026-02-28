# AI Context Templates

Cross-tool AI assistant context following the [AGENTS.md convention](https://github.com/anthropics/agents-md).

## Files

| File | Purpose | Tool Support |
|------|---------|--------------|
| `AGENTS.md` | Core development rules (~150 lines) | Cursor, Copilot, Devin, Windsurf, Cline, etc. |
| `CLAUDE.md` | Claude Code-specific config (imports AGENTS.md) | Claude Code |
| `copilot-instructions.md` | Code review priorities | GitHub Copilot |
| `pr-workflow.md` | PR review commands, Copilot triage, wrap-up checks | Companion (read on demand) |
| `shell-reference.md` | Cross-platform paths, escaping, PowerShell | Companion (read on demand) |
| `data-practices.md` | Batch operations, auditing, external data | Companion (read on demand) |
| `testing.md` | Testing code examples | Companion (read on demand) |
| `checklists.md` | Security and accessibility checklists | Companion (read on demand) |

## Architecture

```
AGENTS.md          ← Core rules, loaded on every request (~150 lines)
├── pr-workflow.md ← Companion: read on demand by agents
├── shell-reference.md
├── data-practices.md
├── testing.md
├── checklists.md
    ↑
CLAUDE.md          ← Imports AGENTS.md via @AGENTS.md, adds Claude-specific notes
```

This approach gives you:
- **Broad compatibility** — AGENTS.md works with most AI coding tools
- **Claude-specific features** — CLAUDE.md adds co-author attribution, import syntax notes, etc.
- **Single source of truth** — Core rules in AGENTS.md, detailed reference in companions
- **Token efficiency** — Companion files load only when relevant, not every request

## Usage

### As a Git Submodule (Recommended)

```bash
git submodule add https://github.com/ISmarsh/planet-smars .planet-smars
```

In your project's CLAUDE.md, use the `@` import syntax:

```markdown
# My Project — Claude Context

## Universal Guidance

<!-- Claude Code resolves this @path import -->
@.planet-smars/templates/ai-context/CLAUDE.md

> *[View shared context](.planet-smars/templates/ai-context/CLAUDE.md) — git, testing, PR workflows*

---

## Project-Specific Context

[Your project-specific context here]
```

**Important:** The `@path/to/file` syntax is how Claude Code imports content. Regular markdown links (`[text](url)`) are just text — they won't be followed. The blockquote link is for human readers.

#### Auto-init Submodules

Add to your package.json to auto-init submodules on `npm install`:

```json
{
  "scripts": {
    "postinstall": "git submodule update --init --recursive"
  }
}
```

### Direct Copy

Copy files to your project root and customize as needed.

## What's Included

### AGENTS.md (Cross-Tool) — Core Rules

- **Git Practices** — Commit format, branching, merge commits over squash
- **PR Review Workflow** — Typical workflow + pointer to `pr-workflow.md`
- **Testing** — Philosophy, when/what to test + pointer to `testing.md`
- **Code Principles** — Minimal changes, no over-engineering + pointer to `checklists.md`
- **Workflow Discipline** — Scope creep, pre-commit, data workflows + pointer to `data-practices.md`
- **Shell & Path Handling** — Key rules + pointer to `shell-reference.md`

### Companion Files (Read on Demand)

- **`pr-workflow.md`** — GraphQL queries, thread resolution, Copilot triage/verification, wrap-up checks
- **`shell-reference.md`** — Escaping pitfalls, PowerShell interop, tool availability
- **`data-practices.md`** — Disposable scripts, verify-iterate, auditing, external sourcing
- **`testing.md`** — Code examples for hooks, components, utilities
- **`checklists.md`** — Security and WCAG 2.1 AA accessibility checklists

### CLAUDE.md (Claude-Specific)

- **Co-Author Attribution** — Standard format for AI-assisted commits
- **Hooks** — Overview of guardrails, reminders, and session management hooks
- **Tool Setup** — `CLAUDE_ENV_FILE` hook for GitHub CLI and other tools
- **Import Syntax** — How `@path/to/file` works in Claude Code

### copilot-instructions.md

- **What to Flag** — Bugs, security issues, accessibility problems
- **What NOT to Flag** — Style preferences, over-engineering suggestions

**Distribution note:** GitHub Copilot reads `.github/copilot-instructions.md` from each repo — it does not support `@` imports or submodule references. The base template must be copied into each project:

```bash
mkdir -p .github
cp templates/ai-context/copilot-instructions.md .github/copilot-instructions.md
```

Projects append project-specific sections below a `---` separator (e.g., Tailwind theme rules, game terminology). When updating the base template, merge only the portion above the separator to avoid clobbering project-specific content.

## Related Templates

- **[hooks/](../hooks/)** — Claude Code hooks that enforce AGENTS.md conventions (guardrails, reminders, notifications). See [hooks/README.md](../hooks/README.md).
