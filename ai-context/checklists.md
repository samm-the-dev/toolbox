# Quality Checklists

Pre-completion checklists for code changes. For core principles, see [AGENTS.md](AGENTS.md).

## Security Checklist

Before completing any change, verify:

- [ ] No user input rendered as raw HTML (XSS)
- [ ] No string concatenation in SQL/commands (injection)
- [ ] No secrets in code or logs
- [ ] No overly permissive CORS or auth
- [ ] Dependencies are from trusted sources

## Accessibility Checklist

Target **WCAG 2.1 AA** compliance:

- [ ] Images have alt text (or `alt=""` for decorative)
- [ ] Form inputs have associated labels
- [ ] Interactive elements are keyboard accessible
- [ ] Focus indicators are visible
- [ ] Color is not the only means of conveying information
- [ ] Text has sufficient contrast (4.5:1 for normal, 3:1 for large)
- [ ] Page has proper heading hierarchy (h1 → h2 → h3)
- [ ] ARIA attributes used correctly (prefer semantic HTML first)
- [ ] Icon-only buttons have `aria-label` describing their action
- [ ] Toggle buttons use `aria-pressed` to expose on/off state
- [ ] Responsive collapsibles: don't attach `aria-expanded` to elements whose
      controlled content is always visible at the current breakpoint; use a
      separate `md:hidden` toggle button with ARIA only where collapse is active

**Automated testing:** Use Playwright + axe-core for CI audits.

## Manual Content Review (Non-Code Projects)

For markdown, data files, and docs-only repos, check before committing:

- [ ] No duplicates across sections (same item in two tables/categories)
- [ ] Items in correct categories (not miscategorized by type or scope)
- [ ] Descriptions match actual behavior (no contradictions)
- [ ] Consistent empty field handling (use explicit "--" vs omitting)
