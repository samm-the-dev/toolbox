# toolbox

Personal shared infrastructure repo, consumed as a `.toolbox` git submodule.

## Contents

| Directory | Description |
|-----------|-------------|
| `ai-context/` | Cross-tool AI context (AGENTS.md + CLAUDE.md, copilot-instructions) |
| `claude-hooks/` | Claude Code hooks (guardrails, reminders, notifications) |
| `claude-skills/` | Claude Code skills (pr-flow, review-lessons, etc.) |
| `google-cloud-auth/` | Google Cloud auth utilities |
| `lib/` | Shared runtime code (shared-schema, utilities) |
| `types/` | Shared TypeScript type definitions |

## Usage

### As Git Submodule

```bash
# Add to your project
git submodule add https://github.com/samm-the-dev/toolbox .toolbox
```

In your project's CLAUDE.md, use the `@` import syntax:

```markdown
# My Project

@.toolbox/ai-context/CLAUDE.md

## Project-Specific Context
...
```

**Note:** Use `@path/to/file` syntax, not markdown links. Claude Code
actually reads imports; links are just text.

## Scaffolding New Projects

Use the [react-vite-starter](https://github.com/samm-the-dev/react-vite-starter) GitHub template repo:

```bash
gh repo create my-new-app --template samm-the-dev/react-vite-starter --clone --public
cd my-new-app
git submodule add https://github.com/samm-the-dev/toolbox .toolbox
```

See [INIT.md](INIT.md) for the full setup checklist.

## Design Principles

1. **No reinventing wheels** -- Use existing libraries when they exist
2. **Composable** -- Projects extend rather than duplicate
3. **Minimal** -- Only essential dependencies included

## License

MIT
