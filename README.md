# planet-smars

Personal utility repo for submoduling into various projects.

## Contents

### `/templates`

Reusable project scaffolding and configuration templates.

| Template | Description |
|----------|-------------|
| `a11y-audit/` | Playwright + axe-core accessibility audit |
| `ai-context/` | Cross-tool AI context (AGENTS.md + CLAUDE.md, copilot-instructions) |
| `github-workflows/` | GitHub Actions for CI and deployment |
| `react-vite/` | React + Vite + Tailwind + TypeScript starter |
| `skills/` | Claude Code skills (pr-flow, etc.) |

## Usage

### As Git Submodule

```bash
# Add to your project
git submodule add https://github.com/ISmarsh/planet-smars .planet-smars
```

In your project's CLAUDE.md, use the `@` import syntax:

```markdown
# My Project

@.planet-smars/templates/ai-context/CLAUDE.md

## Project-Specific Context
...
```

**Note:** Use `@path/to/file` syntax, not markdown links. Claude Code
actually reads imports; links are just text.

### Direct Copy

```bash
# Copy what you need
cp -r planet-smars/templates/react-vite my-new-app
```

## Placeholders to Update

When using the **react-vite** template, update these placeholders:

| File | Placeholder | Purpose |
|------|-------------|---------|
| `package.json` | `"name": "my-app"` | Your app name |
| `index.html` | `<title>My App</title>` | Browser tab title |
| `src/components/Layout.tsx` | `GITHUB_URL` constant | Your GitHub repo link |
| `src/components/Layout.tsx` | `"My App"` in header | Your app name |
| `src/pages/CreditsPage.tsx` | License text | Your license |
| `src/index.css` | CSS variables | Your color scheme |
| `vite.config.ts` | `base` path | For GitHub Pages deployment |

## Design Principles

1. **No reinventing wheels** — Use existing libraries when they exist
2. **Ready to use** — Templates work immediately after copying
3. **Minimal** — Only essential dependencies included
4. **Composable** — Projects extend rather than duplicate

## License

MIT
