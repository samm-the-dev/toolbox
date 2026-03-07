# toolbox

Shared infrastructure for all projects, consumed as a `.toolbox` git submodule.

@ai-context/AGENTS.md

---

## Repository Structure

- `ai-context/` - Cross-tool AI guidance (AGENTS.md, CLAUDE.md, copilot-instructions)
- `claude-hooks/` - Claude Code hooks (guardrails, reminders, notifications)
- `claude-skills/` - Claude Code skills (pr-flow, review-lessons, etc.)
- `google-cloud-auth/` - Google Cloud auth utilities
- `lib/` - Shared runtime code (shared-schema, utilities)
- `types/` - Shared TypeScript type definitions

## Scaffolding New Projects

Use the [react-vite-starter](https://github.com/samm-the-dev/react-vite-starter) template repo, then add this as a submodule. See [INIT.md](INIT.md) for the full checklist.
