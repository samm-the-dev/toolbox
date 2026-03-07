# GitHub Pages Deployment

Standard deployment pattern for Vite + React SPAs deployed to GitHub Pages via GitHub Actions.

## Deploy Workflow

All projects use the same workflow: build on push to `main`, deploy via `actions/deploy-pages`. See `config/github-workflows/deploy-gh-pages.yml` in your project (from the starter template) and [INIT.md](../INIT.md#3-github-pages-deployment-if-applicable) for setup.

## Custom Domain Setup

When moving a project from `<user>.github.io/<repo>/` to a custom domain (e.g., `app.example.com`), there are five touchpoints. Do them in this order -- DNS and GitHub Pages config can happen in parallel, but the repo changes should deploy after the domain is configured.

### 1. DNS

Add a CNAME record for the subdomain at your registrar:

| Type | Host | Answer |
|------|------|--------|
| CNAME | `app` | `<user>.github.io` |

Delete any existing parking records for that subdomain first.

For an apex domain (e.g., `example.com` with no subdomain), use A records instead:

| Type | Host | Answer |
|------|------|--------|
| A | *(blank)* | `185.199.108.153` |
| A | *(blank)* | `185.199.109.153` |
| A | *(blank)* | `185.199.110.153` |
| A | *(blank)* | `185.199.111.153` |

### 2. GitHub Pages custom domain

Set via API (before repo changes, so GitHub can provision the SSL cert):

```bash
gh api repos/OWNER/REPO/pages -X PUT -f cname="app.example.com"
```

Certificate provisioning takes a few minutes. Check status:

```bash
gh api repos/OWNER/REPO/pages --jq '.https_certificate'
```

Once `state` is `"approved"`, enable HTTPS enforcement:

```bash
gh api repos/OWNER/REPO/pages -X PUT -f cname="app.example.com" -F https_enforced=true
```

### 3. Repo: CNAME file

Add `public/CNAME` with the custom domain:

```
app.example.com
```

This tells GitHub Pages which domain to serve. Without it, the custom domain setting resets on each deploy.

### 4. Repo: Vite base path

Update `vite.config.ts` -- change `base` from the repo subpath to `/`:

```typescript
// Before: conditional subpath for GitHub Pages
base: command === 'build' ? '/repo-name/' : '/',

// After: root path for custom domain
base: '/',
```

If the config used a `({ command }) =>` function wrapper only for this conditional, simplify to a plain object.

Update PWA manifest if present:

```typescript
manifest: {
  scope: '/',       // was: '/repo-name/'
  start_url: '/',   // was: '/repo-name/'
}
```

### 5. OAuth / CORS (if applicable)

If the app uses the [token exchange pattern](google-cloud-auth.md):

1. **Cloud Function CORS**: Add the new domain to `ALLOWED_ORIGINS` in `google-cloud-auth.config.json` and redeploy. Keep the old GitHub Pages origin during transition.

2. **Google OAuth Console**: Add the new domain as an authorized JavaScript origin in **APIs & Services > Credentials > OAuth 2.0 Client ID**. Google propagation can take a few minutes.

### Post-deploy verification

- [ ] `https://app.example.com` loads the app
- [ ] `<user>.github.io/<repo>/` redirects (301) to the custom domain
- [ ] PWA install works on the new domain
- [ ] OAuth login works (if applicable)
- [ ] HTTPS enforced (no mixed content warnings)

### Gotchas

- **SSL cert timing**: GitHub won't provision the cert until DNS resolves to their servers. Set up DNS first, then the Pages custom domain.
- **CNAME file required**: Without `public/CNAME`, every deploy resets the custom domain setting in GitHub Pages.
- **Old bookmarks**: GitHub automatically 301-redirects from `<user>.github.io/<repo>/` to the custom domain. No action needed.
- **Branch protection**: Direct pushes to `main` may be blocked. Create a PR for the repo changes.

## Subdomain Strategy

Use subdomains of a base domain for deployed projects (e.g., `ohm.samm-the.dev`, `app2.samm-the.dev`). Each subdomain gets its own CNAME record pointing to the same GitHub Pages host. GitHub routes by matching the CNAME file in each repo.

The base domain's A records only need to be configured when something is hosted at the apex (e.g., a personal site).
