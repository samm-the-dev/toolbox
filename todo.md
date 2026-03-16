# Todo

- [ ] Script copilot-instructions.md base sync across projects (distribution documented in ai-context README)
- [ ] Add PWA template extension (vite-plugin-pwa + service worker config) — used by 4+ projects
- [ ] Add shared Google OAuth hook/pattern — used by recdeck, alt-text-gen
- [ ] Add GHA to auto-update submodule in consumer repos when toolbox main changes (avoids PR churn for mechanical submodule bumps; Copilot chokes on submodule diffs)
- [ ] Promote multi-theme system from the-enchiridion once extracted
- [ ] Add shared notification pattern (Discord webhook + Bluesky posting) — used by alamo-watcher, improv-watcher
- [ ] Extract shared share-link handler (Web Share API + clipboard fallback + sonner toast) — used by ohm, build-a-jam, bio
- [ ] Extract useSwipeToDismiss hook to toolbox/hooks/ — used by build-a-jam, check if bio/ohm have equivalent
- [ ] Write PreToolUse hook to strip leading `cd <workdir> &&` and `-C <workdir>` from Bash commands — Claude redundantly includes these when the working directory is already set, which breaks auto-allows for readonly git ops (allows match on command prefix, `cd repo && git status` doesn't match `git status`)
- [ ] Consider making Claude guidance TDD by default — Copilot review threads on TagFilter and SearchInput repeatedly flagged missing unit tests post-implementation; proactively writing tests (or noting when to skip) would reduce review churn
