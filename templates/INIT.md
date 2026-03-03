# New Project Setup Checklist

Use this checklist when initializing a new project that uses toolbox as a submodule.

## Prerequisites

- [ ] Repository created on GitHub
- [ ] Local clone with toolbox submodule added

```bash
git submodule add https://github.com/samm-the-dev/toolbox .toolbox
```

## 1. CI Workflow

Copy the CI workflow to run lint, test, and build on PRs:

```bash
mkdir -p .github/workflows
cp .toolbox/templates/github-workflows/ci.yml .github/workflows/
```

**Required npm scripts:** `lint`, `test`, `build`

See [github-workflows/README.md](github-workflows/README.md) for customization options.

## 2. GitHub Pages Deployment (if applicable)

For static sites deployed to GitHub Pages:

```bash
cp .toolbox/templates/github-workflows/deploy-gh-pages.yml .github/workflows/
```

Then enable GitHub Pages with Actions as the build source:

```bash
gh api repos/OWNER/REPO/pages --method POST -f build_type=workflow
```

If Pages is already enabled and just needs switching from branch to Actions, use `PUT` instead of `POST`.

## 3. Branch Protection + Copilot Auto-Review

Apply the shared ruleset template (includes PR requirements, branch protection, and Copilot auto-review):

```bash
cd .toolbox/templates/github-rulesets
gh api repos/OWNER/REPO/rulesets -X POST --input main.json
```

This creates a ruleset with: required PRs (thread resolution enforced), deletion/force-push prevention, and Copilot code review on push.

Or configure manually via GitHub UI: Settings → Rules → Rulesets → New ruleset

To opt out of Copilot review, remove the `copilot_code_review` and `copilot_code_review_analysis_tools` rules from the template before applying.

## 4. Auto-Delete Branches

Automatically delete branches after PR merge:

```bash
gh api repos/OWNER/REPO -X PATCH -f delete_branch_on_merge=true
```

Or configure via GitHub UI: Settings → General → Pull Requests → "Automatically delete head branches"

## 5. Copilot Review Instructions (optional)

Copy the shared review instructions so Copilot knows what to flag and what to skip:

```bash
mkdir -p .github
cp .toolbox/templates/ai-context/copilot-instructions.md .github/copilot-instructions.md
```

Append project-specific rules below a `---` separator, same as the base template sync pattern.

## 6. Accessibility Audit (optional)

For projects with UI, add automated accessibility testing:

```bash
cp .toolbox/templates/a11y-audit/audit-a11y.mjs scripts/
```

Add to package.json:
```json
{
  "scripts": {
    "audit:a11y": "node scripts/audit-a11y.mjs"
  },
  "devDependencies": {
    "@axe-core/playwright": "^4.x",
    "playwright": "^1.x"
  }
}
```

Then add the a11y job to your CI workflow (see github-workflows/README.md).

## 7. Claude Code Hooks (one-time machine setup)

Copy hooks to your Claude Code config directory:

```bash
mkdir -p ~/.claude/hooks
cp .toolbox/templates/hooks/*.sh ~/.claude/hooks/
cp .toolbox/templates/hooks/*.ps1 ~/.claude/hooks/   # Windows only
chmod +x ~/.claude/hooks/*.sh
```

Then add hook registrations to `~/.claude/settings.json` — see
[hooks/README.md](hooks/README.md) for the full settings block.

**Windows users:** Add `CLAUDE_CODE_SHELL` to settings.json so hooks run in bash.
Adjust the path if Git is installed elsewhere (`where bash` to find it):
```json
{
  "env": {
    "CLAUDE_CODE_SHELL": "C:\\Program Files\\Git\\bin\\bash.exe"
  }
}
```

**Customize:**
- `guardrail.sh` — set `PROJECT_ROOT` to your workspace directory
- `setup-path.sh` — add paths to tools on your machine
- `notify.ps1` — optionally set `-AppLogo` to a custom icon

This is a one-time setup per machine, not per project.

## 8. VSCode Configuration

Copy debug/task configuration for consistent dev experience:

```bash
mkdir -p .vscode
cp .toolbox/templates/react-vite/.vscode/* .vscode/ 2>/dev/null || true
```

## Verification

After setup, verify:

- [ ] `npm run lint` passes
- [ ] `npm test` passes
- [ ] `npm run build` passes
- [ ] Push a test branch and confirm CI runs
- [ ] (If Pages) Confirm deploy workflow triggers on main
