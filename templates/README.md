# Templates

Reusable project scaffolding and configuration templates.

**Starting a new project?** Follow the [INIT.md checklist](INIT.md) for step-by-step setup.

## Available Templates

| Template | Description | Usage |
|----------|-------------|-------|
| `a11y-audit/` | Playwright + axe-core accessibility audit | Copy to `scripts/` |
| `ai-context/` | Cross-tool AI context (AGENTS.md + CLAUDE.md) | Submodule or copy |
| `hooks/` | Claude Code hooks (guardrails, reminders, notifications) | Copy to `~/.claude/hooks/` |
| `github-workflows/` | GitHub Actions for CI and deployment | Copy to `.github/workflows/` |
| `react-vite/` | React + Vite + Tailwind + TypeScript starter | Copy and customize |
| `skills/` | Claude Code skills (custom slash commands) | Copy to `~/.claude/skills/` |

---

## ai-context

Cross-tool AI context following the [AGENTS.md convention](https://github.com/anthropics/agents-md) (60,000+ repos).

### Contents

| File | Purpose |
|------|---------|
| `AGENTS.md` | Cross-tool guidance (Git, testing, PR workflows, security) — works with Cursor, Copilot, Devin, etc. |
| `CLAUDE.md` | Claude Code-specific config — imports AGENTS.md via `@AGENTS.md` |
| `copilot-instructions.md` | Code review priorities for GitHub Copilot |

### Usage

**Option 1: Git Submodule (Recommended)**

```bash
git submodule add https://github.com/samm-the-dev/toolbox .toolbox
```

In your project's CLAUDE.md:

```markdown
@.toolbox/templates/ai-context/CLAUDE.md
```

**Option 2: Direct Copy**

```bash
cp templates/ai-context/AGENTS.md .
cp templates/ai-context/CLAUDE.md .
cp templates/ai-context/copilot-instructions.md .github/
```

---

## hooks

Claude Code hooks that enforce AGENTS.md conventions automatically.

### Contents

| File | Event | Purpose |
|------|-------|---------|
| `guardrail.sh` | PreToolUse | Blocks force push, squash merge, secrets staging, destructive git/rm |
| `copilot-reminder.sh` | PostToolUse | Reminds to check Copilot review after push |
| `pre-compact.sh` | PreCompact | Captures git state before context compaction |
| `context-reminder.sh` | SessionStart | Re-injects conventions after compaction |
| `setup-path.sh` | SessionStart | Adds CLI tools to PATH |
| `notify.sh` / `.ps1` | Notification | Desktop notifications (Windows/macOS/Linux) |

### Usage

```bash
mkdir -p ~/.claude/hooks
cp templates/hooks/*.sh ~/.claude/hooks/
cp templates/hooks/*.ps1 ~/.claude/hooks/   # Windows only
chmod +x ~/.claude/hooks/*.sh
```

Then register in `~/.claude/settings.json`. See `hooks/README.md` for the
full settings block and customization options.

---

## github-workflows

GitHub Actions workflow templates for Node.js projects.

### Contents

| File | Purpose |
|------|---------|
| `ci.yml` | Lint, test, and build on PRs and main pushes |
| `deploy-gh-pages.yml` | Deploy to GitHub Pages |

### Usage

```bash
mkdir -p .github/workflows
cp templates/github-workflows/*.yml .github/workflows/
```

See `github-workflows/README.md` for customization options.

---

## react-vite

Minimal React starter with Tailwind CSS, TypeScript, and dark mode.

### Features

- React 19 + Vite
- TypeScript with path aliases (`@/`)
- Tailwind CSS with CSS variable theming
- [shadcn/ui](https://ui.shadcn.com/) ready (utility deps + `cn()` included)
- Dark mode via `useTheme` hook
- React Router v7
- Vitest + Testing Library
- ESLint + jsx-a11y
- Credits/licensing page

### Usage

```bash
# Copy template
cp -r templates/react-vite my-new-app
cd my-new-app

# Install and run
npm install
npm run dev
```

### Customization

After copying:

1. Update `name` in `package.json`
2. Update `GITHUB_URL` in `src/components/Layout.tsx`
3. Customize colors in `src/index.css`
4. Update license info in `src/pages/CreditsPage.tsx`

See `react-vite/README.md` for full documentation.

---

## skills

Claude Code skills (custom slash commands) for common workflows.

### Contents

| Skill | Description |
|-------|-------------|
| `pr-flow/` | Create PR with full workflow (branch, commit, push, PR, checks, reviews) |

### Usage

```bash
# Copy to personal skills (all projects)
cp -r templates/skills/pr-flow ~/.claude/skills/

# Or copy to project skills (single project)
mkdir -p .claude/skills
cp -r templates/skills/pr-flow .claude/skills/
```

Then invoke with `/pr-flow` in Claude Code.

See `skills/README.md` for creating custom skills.

---

## Design Principles

1. **Ready to use** — No placeholders that break. Works immediately.
2. **Minimal** — Only essential dependencies. Add what you need.
3. **Documented** — Clear customization instructions included.
4. **Composable** — Templates can reference each other.
