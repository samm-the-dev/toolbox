# Todo

- [ ] Script copilot-instructions.md base sync across projects (distribution documented in ai-context README)
- [ ] Add PWA template extension (vite-plugin-pwa + service worker config) — used by 4+ projects
- [ ] Add shared Google OAuth hook/pattern — used by recdeck, alt-text-gen
- [ ] Add GHA to auto-update submodule in consumer repos when toolbox main changes (avoids PR churn for mechanical submodule bumps; Copilot chokes on submodule diffs)
- [ ] Promote multi-theme system from the-enchiridion once extracted
- [ ] Add shared notification pattern (Discord webhook + Bluesky posting) — used by alamo-watcher, improv-watcher
- [ ] Extract shared share-link handler (Web Share API + clipboard fallback + sonner toast) — used by ohm, build-a-jam, bio
- [ ] Move `config/github-rulesets/` from react-vite-starter to toolbox (framework-agnostic repo setup); add `delete_branch_on_merge` to the setup instructions
