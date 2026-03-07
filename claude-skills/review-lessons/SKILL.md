---
name: review-lessons
description: Audit accumulated lessons in memory and project files. Identify insights worth promoting to shared guidance.
user-invocable: true
allowed-tools:
  - Read
  - Edit
  - Grep
  - Glob
  - Task
argument-hint: "[focus-area]"
---

# Review Lessons

Audit knowledge scattered across memory files, project instructions, and recent work. Identify lessons worth promoting to shared guidance — and guidance that is outdated or redundant.

## Arguments

`$ARGUMENTS` can be:
- Empty: Incremental audit (changes since last run)
- A focus area: Narrow the audit (e.g., "shell", "git", "data", "hooks")
- `full`: Force a complete audit regardless of last run date

## Run Tracking

Check for a run history log in the workspace memory directory:

```
~/.claude/projects/*/memory/.review-lessons-log
```

The file is an append-only log. Each entry is one line:

```
<ISO datetime> | <scope> | promoted:<N> trimmed:<N> dupes:<N> deferred:<N>
```

Example:

```
2026-02-05T14:30:00 | full | promoted:3 trimmed:1 dupes:2 deferred:5
2026-02-05T18:45:00 | shell | promoted:1 trimmed:0 dupes:0 deferred:2
```

- **Last entry's datetime** determines the "since" cutoff for incremental runs. Use file modification times and `git log --since` in the toolbox repo to identify what changed.
- If the log doesn't exist (first run) or `$ARGUMENTS` is `full`, do a complete audit.
- After completing the report, append a new entry with the current ISO datetime, scope, and summary counts.
- **Scope values:** `full` (explicit or first run), `incremental` (when `$ARGUMENTS` is empty), or the focus area string when `$ARGUMENTS` names a topic (e.g., `shell`, `git`).

## Discovery

Start by finding what exists. Don't hardcode paths — discover them.

### Memory files (accumulated lessons)

```
~/.claude/projects/*/memory/MEMORY.md
~/.claude/projects/*/memory/*.md
~/.claude/CLAUDE.md
```

### Shared guidance (promoted lessons)

Look for the shared guidance directory. It's typically referenced via `@path/to/file` import in `~/.claude/CLAUDE.md` or in a workspace `CLAUDE.md`. Follow the import chain to find the ai-context root, then read:
- `AGENTS.md` — cross-tool guidance
- `CLAUDE.md` — Claude Code-specific guidance
- Any companion files (see Companion File Pattern below)

**Important:** When promoting changes, write to the **source repo** of the shared guidance, not a submodule copy. The source path is the one referenced in `~/.claude/CLAUDE.md`. Projects embed shared guidance via git submodules — writing to the submodule copy (e.g., `.toolbox/`) only creates uncommittable changes in the wrong repo.

### Project instructions

```
<workspace>/CLAUDE.md
<workspace>/*/CLAUDE.md
<workspace>/*/.claude/CLAUDE.md
```

## Audit Process

### Read all sources

Read the discovered files. For each, note:
- What lessons/rules it contains
- Where each lesson likely originated (project-specific experience vs general wisdom)
- How specific vs universal each item is

### Categorize findings

Sort every notable item into one of these buckets:

| Bucket | Criteria | Action |
|--------|----------|--------|
| **Promote** | Applies to 2+ projects; about process/workflow/tooling, not project data | Draft addition to shared guidance |
| **Already covered** | Lesson exists in both memory and shared guidance | Note the duplication — consider removing from memory |
| **Outdated** | No longer accurate or relevant | Flag for removal from wherever it lives |
| **Project-specific** | Only relevant to one project | Keep in project memory/instructions, don't promote |
| **Trim from guidance** | In shared guidance but too detailed or niche | Move to companion file or remove |

### Check guidance health

For each shared guidance file, assess:
- **Line count** — AGENTS.md should target ≤150 lines of core rules
- **Signal-to-noise** — is every instruction earning its token cost?
- **Companion candidates** — detailed reference material (code snippets, command examples, troubleshooting) that could move to companion files without losing the core rule

## Output

Present findings as a structured report with these sections:

### Lessons to Promote

For each item:
- **Lesson**: One-line summary
- **Source**: Where it was found (file + context)
- **Target**: Which shared guidance file and section
- **Draft**: The actual text to add (ready to paste)

### Guidance Health

- Current line counts vs targets
- Items that should move to companion files
- Outdated items to remove

### Duplication & Cleanup

- Items duplicated between memory and shared guidance
- Memory items that can be removed (already promoted or outdated)

### Deferred

- Project-specific items staying where they are (brief list, no action needed)

---

## Companion File Pattern

When promoting content, follow this structure for the shared guidance directory:

```
ai-context/
├── AGENTS.md              # Core rules — universal, ≤150 lines
├── CLAUDE.md              # Claude Code-specific config and conventions
├── <topic>.md             # Companion: detailed reference for a topic
└── ...
```

### What goes in core AGENTS.md

- High-level principles and rules (the "what")
- Brief rationale when non-obvious (the "why")
- One-line pattern summaries with a pointer to the companion file
- Example: "Use merge commits for PRs. See [pr-workflow.md](pr-workflow.md) for the full review and triage process."

### What goes in companion files

- Step-by-step procedures and commands (the "how")
- Code snippets, GraphQL queries, shell examples
- Troubleshooting and edge cases
- Detailed checklists

### Naming companions

- Use kebab-case: `pr-workflow.md`, `shell-reference.md`, `data-practices.md`
- Name by topic, not by when it was created
- One file per concern — don't create a file for a single tip

### Cross-tool compatibility

- AGENTS.md uses standard markdown — no tool-specific syntax
- Claude Code follows `@path/to/file` imports; other tools discover files via directory walks
- Keep companion files self-contained (readable without AGENTS.md context)
- Don't use `@` imports in AGENTS.md itself — it inflates token cost on every request. Let Claude read companions on demand.
