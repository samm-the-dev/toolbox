---
name: pr-flow
description: Create a PR with standard workflow (branch, commit, push, PR). Hands off to /pr-check for CI and reviews.
allowed-tools:
  - Bash(git *)
  - Bash(gh *)
---

# PR Flow

Create a pull request following the standard workflow.

## Arguments

`$ARGUMENTS` can be:
- Empty: Use staged/unstaged changes, auto-generate branch name
- A branch name: Use that specific branch name
- A description: Use it to name the branch and PR

## Workflow

Follow these steps in order:

### Check Current State

```bash
git status
git diff --stat
```

If no changes exist, stop and inform the user.

### Create Branch (if on main/master)

Generate a descriptive branch name from the changes:
- `feature/*` for new functionality
- `fix/*` for bug fixes
- `chore/*` for maintenance, deps, config
- `docs/*` for documentation only

```bash
git checkout -b <branch-name>
```

### Stage and Commit

Stage relevant files (prefer specific files over `git add -A`):

```bash
git add <files>
```

Create commit with descriptive message:

```bash
git commit -m "$(cat <<'EOF'
<type>: <short summary>

<optional body explaining why>

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

### Local Quality Checks

Before pushing, run local verification:

**Lint and Build:**
```bash
npm run lint
npm run build
```

**Code Quality Review:**
- Scan for duplicated logic (extract if pattern appears 3+ times)
- Check for unused imports, dead code, stale comments
- Verify no obsolete code or `// removed` markers

If issues found, fix them and amend the commit:
```bash
git add <fixed-files>
git commit --amend --no-edit
```

### Push and Create PR

```bash
git push -u origin <branch-name>
```

Create PR with summary and test plan:

```bash
gh pr create --title "<title>" --body "$(cat <<'EOF'
## Summary
<bullet points of changes>

## Test plan
- [ ] <verification steps>

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**After creating the PR, display the PR URL to the user.**

### Watch Checks and Review

After creating the PR, invoke `/pr-check` to watch CI checks and triage review comments.
