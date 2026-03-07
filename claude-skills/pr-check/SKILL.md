---
name: pr-check
description: Watch CI checks and triage review comments on an existing PR
user-invocable: true
allowed-tools:
  - Bash(git *)
  - Bash(gh *)
  - Read
  - Grep
  - Agent
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

### Wait for Copilot Review and Fetch Comments

**Run this section as a background agent** (`Agent` tool with `run_in_background: true`, `subagent_type: "general-purpose"`). This frees the conversation so the user can chat while waiting. Pass the agent the PR number, owner, and repo.

The background agent should:

1. Resolve owner/repo and latest commit:

```bash
OWNER_REPO=$(gh repo view --json owner,name -q '"\(.owner.login)/\(.name)"')
OWNER=${OWNER_REPO%/*}
REPO=${OWNER_REPO#*/}
LATEST_COMMIT=$(gh pr view <PR_NUMBER> --json headRefOid -q .headRefOid)
```

2. Poll for Copilot review with progressive backoff (30s, 60s, 90s, 120s, 150s — ~7 min total):

```bash
WAIT=30
while [ $WAIT -le 150 ]; do
  sleep $WAIT
  REVIEWED_COMMIT=$(gh api "repos/$OWNER/$REPO/pulls/<PR_NUMBER>/reviews" \
    --jq '[.[] | select(.user.login | contains("copilot"))] | last | .commit_id')
  if [ "$LATEST_COMMIT" = "$REVIEWED_COMMIT" ]; then
    echo "COPILOT_REVIEWED=true"
    break
  fi
  WAIT=$((WAIT + 30))
done
```

3. Once reviewed (or timeout), fetch all review comments and unresolved thread IDs:

```bash
gh api "repos/$OWNER/$REPO/pulls/<PR_NUMBER>/comments" \
  --jq '.[] | {id: .id, path: .path, line: .line, body: .body, user: .user.login}'
```

```bash
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

4. Return a summary: Copilot review status, unresolved comments (body, path, line, thread ID, comment database ID).

**When the background agent completes**, report results to the user and proceed to triage.

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
