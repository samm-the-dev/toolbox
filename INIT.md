# New Project Setup Checklist

Use this checklist when initializing a new project from the [react-vite-starter](https://github.com/samm-the-dev/react-vite-starter) template.

## 1. Scaffold and Connect

```bash
# Create repo from template (clean commit history)
gh repo create my-new-app --template samm-the-dev/react-vite-starter --clone --public
cd my-new-app

# Add toolbox submodule
git submodule add https://github.com/samm-the-dev/toolbox .toolbox
```

## 2. CI Workflow

Copy the CI workflow from the template's `config/` directory:

```bash
mkdir -p .github/workflows
cp config/github-workflows/ci.yml .github/workflows/
```

**Required npm scripts:** `lint`, `test`, `build`

See `config/github-workflows/README.md` for customization options.

## 3. GitHub Pages Deployment (if applicable)

For static sites deployed to GitHub Pages:

```bash
cp config/github-workflows/deploy-gh-pages.yml .github/workflows/
```

Then enable GitHub Pages with Actions as the build source:

```bash
gh api repos/OWNER/REPO/pages --method POST -f build_type=workflow
```

If Pages is already enabled and just needs switching from branch to Actions, use `PUT` instead of `POST`.

## 4. Vercel Preview Deploys (optional)

The template includes `vercel.json` configured for preview-only deploys (production builds are skipped since GitHub Pages handles production). The cleanup workflow at `.github/workflows/cleanup-preview-deployments.yml` automatically removes stale GitHub deployment records when PRs close.

To enable preview deploys:

1. Import the repo in the [Vercel Dashboard](https://vercel.com/import)
2. Optionally shorten the preview deployment retention period in project Settings > Security > Deployment Retention Policy (default is 6 months; dashboard-only setting)

## 5. Branch Protection + Copilot Auto-Review

Apply the shared ruleset template (includes PR requirements, branch protection, and Copilot auto-review):

```bash
gh api repos/OWNER/REPO/rulesets -X POST --input config/github-rulesets/main.json
```

This creates a ruleset with: required PRs (thread resolution enforced), deletion/force-push prevention, and Copilot code review on push.

Or configure manually via GitHub UI: Settings > Rules > Rulesets > New ruleset

To opt out of Copilot review, remove the `copilot_code_review` and `copilot_code_review_analysis_tools` rules from the template before applying.

## 6. Auto-Delete Branches

Automatically delete branches after PR merge:

```bash
gh api repos/OWNER/REPO -X PATCH -f delete_branch_on_merge=true
```

Or configure via GitHub UI: Settings > General > Pull Requests > "Automatically delete head branches"

## 7. Copilot Review Instructions (optional)

Copy the shared review instructions so Copilot knows what to flag and what to skip:

```bash
mkdir -p .github
cp .toolbox/ai-context/copilot-instructions.md .github/copilot-instructions.md
```

Append project-specific rules below a `---` separator, same as the base template sync pattern.

## 8. Accessibility Audit (optional)

For projects with UI, add automated accessibility testing:

```bash
cp config/a11y-audit/audit-a11y.mjs scripts/
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

Then add the a11y job to your CI workflow (see `config/github-workflows/README.md`).

## 9. Claude Code GitHub Action

Add the shared Claude Code workflow so @claude mentions, adversarial PR review, and Copilot response all work without duplicating the workflow in each repo:

```bash
mkdir -p .github/workflows
cp .toolbox/.github/workflows/claude-caller-example.yml .github/workflows/claude.yml
```

The caller references `samm-the-dev/toolbox/.github/workflows/claude.yml@main` and passes `ANTHROPIC_API_KEY` from the repo's secrets. To restrict which behaviors run, change the `mode` input to `on-demand`, `adversarial`, or `copilot-response` (default is `all`).

**Secret setup:** Add `ANTHROPIC_API_KEY` as a repo secret in each consumer repo:

```bash
gh secret set ANTHROPIC_API_KEY --repo samm-the-dev/my-new-app
```

Pull the key from your password manager (Dashlane, 1Password, etc.) to avoid storing it in plaintext. If you later move repos under a GitHub organization, you can replace per-repo secrets with a single [organization secret](https://docs.github.com/en/actions/security-for-github-actions/security-guides/using-secrets-in-github-actions#creating-secrets-for-an-organization) scoped to selected repos.

## 10. Claude Code Hooks (one-time machine setup)

Copy hooks to your Claude Code config directory:

```bash
mkdir -p ~/.claude/hooks
cp .toolbox/claude-hooks/*.sh ~/.claude/hooks/
cp .toolbox/claude-hooks/*.ps1 ~/.claude/hooks/   # Windows only
chmod +x ~/.claude/hooks/*.sh
```

Then add hook registrations to `~/.claude/settings.json` -- see
[claude-hooks/README.md](claude-hooks/README.md) for the full settings block.

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
- `guardrail.sh` -- set `PROJECT_ROOT` to your workspace directory
- `setup-path.sh` -- add paths to tools on your machine
- `notify.ps1` -- optionally set `-AppLogo` to a custom icon

This is a one-time setup per machine, not per project.

## 11. Clean Up Config Templates

After copying what you need from `config/`, remove it:

```bash
rm -rf config/
git add -A && git commit -m "Remove config templates after setup"
```

## 12. VSCode Configuration

The template includes `.vscode/launch.json` and `.vscode/tasks.json` for debugging and dev server tasks. Customize as needed.

## Verification

After setup, verify:

- [ ] `npm run lint` passes
- [ ] `npm test` passes
- [ ] `npm run build` passes
- [ ] Push a test branch and confirm CI runs
- [ ] (If Pages) Confirm deploy workflow triggers on main

## 13. Claude Code LSP (one-time machine setup)

Install language server binaries and plugins for semantic code navigation. See [ai-context/lsp-setup.md](ai-context/lsp-setup.md) for the full guide.

This is a one-time setup per machine, not per project.
