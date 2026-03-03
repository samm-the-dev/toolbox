# PR Review & Copilot Workflow

Detailed commands and procedures for managing pull request reviews via GitHub CLI. For core PR rules, see [AGENTS.md](AGENTS.md).

## Fetching Unresolved Review Threads

The REST API (`/pulls/{pr}/comments`) returns all comments with no resolved filter. Use GraphQL instead:

```bash
gh api graphql -f query='query {
  repository(owner: "OWNER", name: "REPO") {
    pullRequest(number: PR_NUMBER) {
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

Filter results for `isResolved: false`.

## Replying to Comments

```bash
gh api repos/OWNER/REPO/pulls/PR/comments/COMMENT_ID/replies -f body="Reply text"
```

Use `databaseId` from GraphQL (numeric) for the REST reply endpoint.

## Resolving Threads (Batched)

```bash
gh api graphql -f query='mutation {
  t1: resolveReviewThread(input: {threadId: "PRRT_..."}) { thread { isResolved } }
  t2: resolveReviewThread(input: {threadId: "PRRT_..."}) { thread { isResolved } }
}'
```

## Checking CI Status

**Default approach:** Use `--watch` to wait for checks to complete:

```bash
gh pr checks <PR_NUMBER> --watch
```

This is the preferred method — no polling or manual refresh needed.

## Copilot Auto-Review

### Setup

Enable Copilot reviews via repo Settings > Rules > Rulesets, or manually trigger via PR page > Reviewers > gear icon > select "copilot-pull-request-reviewer".

### Triage Process

**While CI runs**, check for Copilot comments (typically posts within a minute).

Categorize each comment:
- **Fix**: Real bugs, logic errors, missing edge cases in new code
- **Dismiss**: Stylistic preferences, over-engineering, suggestions for code not changed in the PR
- **Already fixed**: Issues addressed by other commits

**Common dismissals:**
- Unnecessary `useMemo`/`useCallback` wrapping (unless measured perf issue)
- Dependency array pedantry for stable React setState
- Suggestions to add complexity for hypothetical future cases
- Over-abstraction for patterns that appear < 3 times
- Suggestions assuming human copy-paste audience for agent-targeted docs (skill files, prompt templates)

### Resolution

1. **Push fixes before resolving** — if auto-merge is enabled, resolving threads can trigger merge before your fix commit lands. Always: fix > push > resolve.
2. **Resolve threads immediately after replying** — no need to wait for Copilot to re-review before resolving. Copilot typically reviews the new commit independently (though it may occasionally skip — see caveats below).
3. **Never batch-resolve** without reading each comment — Copilot occasionally finds real bugs.
4. **Present dismissals** to user for approval before posting replies — even when dismissal seems obvious based on existing patterns.
5. **Confirm merge readiness** — after resolving all threads, verify review is complete for latest commits before merging. Dismissal approval does not equal merge approval.
6. Use the thread management commands above for replying and resolving.

### Checking if Copilot Review is Complete

Copilot reviews take about 60 seconds. To check if the latest commit has been reviewed:

```bash
# Get the latest commit SHA on the PR branch
LATEST_COMMIT=$(gh pr view <PR_NUMBER> --json headRefOid -q .headRefOid)

# Get the commit SHA of Copilot's most recent review
REVIEWED_COMMIT=$(gh api repos/OWNER/REPO/pulls/<PR_NUMBER>/reviews \
  --jq '[.[] | select(.user.login | contains("copilot"))] | last | .commit_id')

# Compare them
if [ "$LATEST_COMMIT" = "$REVIEWED_COMMIT" ]; then
  echo "Review complete"
else
  echo "Review pending or not triggered"
fi
```

Or in a single line:
```bash
gh api repos/OWNER/REPO/pulls/<PR_NUMBER>/reviews --jq 'map(select(.user.login | contains("copilot"))) | last | .commit_id'
```

Compare this to the current HEAD. If they match, the review is complete.

### If Copilot Doesn't Review

Sometimes skips commits (small changes, rapid pushes). Manually trigger via GitHub UI: PR page > Reviewers (right sidebar) > gear icon > select "copilot-pull-request-reviewer". The `gh` CLI can't assign bot reviewers (`--add-reviewer` returns HTTP 422 for bot users) — manual trigger is UI-only.

**Don't confuse "pending" with "skipped."** If you check immediately after PR creation and get no reviews, wait and recheck before concluding it skipped (reviews typically take about a minute). Only manually trigger after at least 2 minutes with no review.

**Stacked PRs:** When a PR is created against a feature branch (not main) and the parent merges — causing GitHub to auto-update the base to main — Copilot review does NOT re-trigger. Close and recreate the PR to fix this:

```bash
gh pr close <NUMBER>
gh pr create --base main --title "..." --body "..."
```

To avoid the issue entirely, create PRs against main even for stacked work.

### Repeat Comments Across Rounds

Copilot reviews the full diff on each push, not just the delta. Previously dismissed comments will reappear as new threads if the code they reference hasn't changed. This is expected -- Copilot has no memory of prior dismissals.

When triaging, check if a comment matches a previously dismissed thread before re-analyzing. Reply with "Already addressed in previous round" and resolve.

## Branch Protection

GitHub has two protection systems:
- **Rulesets** (newer): `gh api repos/OWNER/REPO/rulesets`
- **Branch protection rules** (older): `gh api repos/OWNER/REPO/branches/main/protection`

Prefer rulesets for new repos.

## Fork PRs

When a repo has an `upstream` remote (common for submodules or forked repos), `gh pr create` defaults to the upstream owner. Always specify `--repo` to target the correct fork:

```bash
gh pr create --repo OWNER/REPO --title "..." --body "..."
```

## PR Wrap-up Manual Checks

Before merging, run these manual checks alongside automated CI:

### Code duplication

- Scan for duplicated logic across files
- Extract when a pattern appears **3+ times** AND reduces actual code
- Don't over-abstract for 2 instances
- Good: consolidating duplicate message strings
- Bad: a helper that just wraps a standard library call with no reduction

### Obsolete code

- Look for unused imports
- Dead functions or unreachable code
- Stale comments referencing removed features
- Old commented-out code blocks

### Documentation review

- Verify README matches current implementation
- Check that code examples still work
- Update architecture docs if structure changed
- Confirm file listings are accurate

### Attribution & licensing

- Update Credits page when adding external data sources (APIs, datasets)
- Review and follow API/data terms of service (rate limits, attribution, usage restrictions)
- Include required logos/text for APIs that mandate them (e.g., TMDB)
- Note fan-curated vs official content to avoid implying endorsement
- Verify license compatibility before adding dependencies or data sources

### Review comment triage (if using automated reviewers)

- Categorize comments: fix, dismiss, or already-addressed
- Reply to each comment explaining the action taken
- Resolve threads after addressing
- Present dismissals for approval before resolving

### Local branch cleanup

After merging, prune stale local branches whose remotes are gone:

```bash
git fetch --prune
git branch -vv | grep ': gone]' | awk '{print $1}' | xargs git branch -D
```
