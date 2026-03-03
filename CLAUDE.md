# toolbox

A collection of project templates and configurations for bootstrapping new projects.

@templates/ai-context/AGENTS.md

---

## Repository Structure

- `templates/` - Project templates and shared configurations
  - `react-vite/` - React + Vite + TypeScript starter
  - `ai-context/` - CLAUDE.md, AGENTS.md, and Copilot instructions
  - `skills/` - Claude Code skills (pr-flow, etc.)
  - `prettier/` - Shared Prettier configuration
  - `github-workflows/` - CI/CD workflow templates
  - `a11y-audit/` - Accessibility testing setup

## Working on Templates

When modifying templates, test changes by:

1. Running tests within the template directory (e.g., `npm run test:run`)
2. Creating a new project from the template to verify setup

Template `node_modules/` and `package-lock.json` are gitignored for local testing.
