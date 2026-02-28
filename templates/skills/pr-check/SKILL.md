---
name: pr-check
description: Watch CI checks and triage review comments on an existing PR
user-invocable: true
allowed-tools:
  - Bash(git *)
  - Bash(gh *)
  - Read
  - Grep
argument-hint: "[PR number]"
---

# PR Check

Watch CI checks and triage review comments on an existing pull request. Typically run after `/pr-flow` or when returning to a PR later.

## Arguments

`$ARGUMENTS` can be:
- A PR number: Check that specific PR
- Empty: Auto-detect from the current branch

### Auto-detection

If no PR number is provided:

```bash
gh pr view --json number -q .number
```

If this fails, list open PRs and ask the user which one.

## Workflow

### Watch CI Checks

```bash
gh pr checks <PR_NUMBER> --watch
```

If checks fail, report which ones and offer to investigate. Read the failed check logs:

```bash
gh run view <RUN_ID> --log-failed
```

### Check for Review Comments

**Initial wait:** If invoked immediately after PR creation (e.g., from `/pr-flow`), wait 60 seconds before the first check. Copilot reviews take about a minute from request to completion — checking earlier always finds nothing.

```bash
sleep 60
```

Then fetch and display review comments using the built-in `/pr-comments` tool for formatted output:

```
/pr-comments <PR_NUMBER>
```

Then fetch unresolved thread IDs (needed for triage and resolution):

```bash
OWNER_REPO=$(gh repo view --json owner,name -q '"\(.owner.login)/\(.name)"')
OWNER=${OWNER_REPO%/*}
REPO=${OWNER_REPO#*/}

gh api graphql -f query='query {
  repository(owner: "'"$OWNER"'", name: "'"$REPO"'") {
    pullRequest(number: '"$PR_NUMBER"') {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          comments(first: 1) {
            nodes { body path line databaseId }
          }
        }
      }
    }
  }
}'
```

Filter for `isResolved: false`. Use the `/pr-comments` output for context when triaging.

### Check Copilot Review Completeness

If the repo uses Copilot auto-review, verify the latest commit has been reviewed:

```bash
LATEST_COMMIT=$(gh pr view <PR_NUMBER> --json headRefOid -q .headRefOid)

REVIEWED_COMMIT=$(gh api repos/OWNER/REPO/pulls/<PR_NUMBER>/reviews \
  --jq '[.[] | select(.user.login | contains("copilot"))] | last | .commit_id')
```

If they don't match, note that Copilot review is pending. Wait briefly and recheck — reviews take about 60 seconds. Only flag as "skipped" after 2+ minutes.

### Triage Comments

Categorize each unresolved comment:

| Category | Action |
|----------|--------|
| **Fix** | Real bugs, logic errors, missing edge cases in changed code |
| **Dismiss** | Stylistic preferences, over-engineering, suggestions for unchanged code |
| **Already fixed** | Issues addressed by other commits |

**Common dismissals:**
- Unnecessary `useMemo`/`useCallback` wrapping
- Dependency array pedantry for stable React setState
- Complexity for hypothetical future cases
- Over-abstraction for patterns appearing < 3 times

**Present dismissals to the user for approval** before posting replies — even when dismissal seems obvious.

### Address Feedback

For items categorized as "Fix":

1. Make the code changes
2. Commit with a descriptive message
3. **Push before resolving threads** — if auto-merge is enabled, resolving can trigger merge before your fix lands

```bash
git add <files>
git commit -m "$(cat <<'EOF'
address review feedback: <summary>

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
git push
```

### Reply and Resolve Threads

After pushing, resolve threads immediately — no need to wait for Copilot to re-review.

Reply to each comment explaining the action taken:

```bash
gh api repos/OWNER/REPO/pulls/<PR_NUMBER>/comments/COMMENT_ID/replies -f body="Reply text"
```

Batch-resolve after all replies are posted:

```bash
gh api graphql -f query='mutation {
  t1: resolveReviewThread(input: {threadId: "PRRT_..."}) { thread { isResolved } }
  t2: resolveReviewThread(input: {threadId: "PRRT_..."}) { thread { isResolved } }
}'
```

### Report Status

Summarize for the user:
- **PR URL** (always include — make it clickable)
- **CI checks**: pass/fail with details on failures
- **Review threads**: resolved count, any remaining
- **Merge readiness**: whether the PR is clear to merge

If all checks pass and all threads are resolved, ask if they'd like to merge:

```bash
gh pr merge <PR_NUMBER> --merge
```

### Lessons Check

After the PR is complete, prompt:

> "Any lessons worth capturing? Run `/review-lessons` to audit for promotable insights."

Skip if the user wants to move on.
