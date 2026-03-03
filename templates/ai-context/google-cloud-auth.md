# GCP OAuth Token Exchange Pattern

Server-side token exchange for SPAs that need persistent Google API auth without requiring users to re-authenticate on every page refresh.

## Problem

Google Identity Services (GIS) implicit OAuth flow stores access tokens in memory only. Tokens expire after ~1 hour and are lost on page refresh. FedCM only applies to Sign-In with Google (identity), not OAuth token requests (API authorization). SPAs with no backend have no way to silently refresh tokens.

## Solution

A lightweight GCP Cloud Function (gen2, runs on Cloud Run) acts as an OAuth token exchange proxy:

1. Client gets an authorization code via GIS `initCodeClient` (popup)
2. Cloud Function exchanges the code for access + refresh tokens using the client secret
3. Refresh token persists in client localStorage
4. On page load, client sends refresh token to Cloud Function for a fresh access token -- no popup, no user interaction

```
Browser (GitHub Pages)          Cloud Function              Google OAuth
  |                                |                           |
  |-- popup (initCodeClient) ----->|                           |
  |<-- authorization code ---------|                           |
  |-- POST /exchange {code} ------>|-- code + secret --------->|
  |<-- {access, refresh} ----------|<-- tokens ----------------|
  |                                |                           |
  |  (page refresh)                |                           |
  |-- POST /refresh {refresh} ---->|-- refresh + secret ------>|
  |<-- {access} -------------------|<-- new access token ------|
```

## Shared Code Structure

This pattern is implemented as shared code in toolbox. Consuming projects pull it in via the submodule:

```
.toolbox/
  google-cloud-auth/
    function/                       # Cloud Function source (generic)
      index.ts                      # HTTP handler (export: tokenExchange)
      package.json                  # Dependencies + deploy script ref
      tsconfig.json                 # Node 22 TypeScript config
    deploy.ps1                      # Zero-config deploy (auto-discovers source + config)
  lib/
    google-drive-sync.ts            # createDriveSync<T>() factory
    local-storage-sync.ts           # createLocalStorage<T>() factory
  types/
    google-identity.d.ts            # GIS ambient type declarations
  templates/ai-context/
    google-cloud-auth.md            # This doc
```

### What consuming projects provide

- **`google-cloud-auth.config.json`** at repo root -- per-project function name, entry point, and secret references
- **App-specific config** -- env vars for client ID, file name, scope, token exchange URL
- **App-specific sanitize function** -- passed to the factory for data validation/migration

### TypeScript setup

Include the submodule types directory in `tsconfig.app.json`:

```json
{
  "include": ["src", ".toolbox/types"]
}
```

Import library modules directly:

```typescript
import { createDriveSync } from '../../.toolbox/lib/google-drive-sync';
import { createLocalStorage } from '../../.toolbox/lib/local-storage-sync';
```

## Client-Side Libraries

### `createDriveSync<T>(config)` Factory

Creates a closure-scoped Drive sync instance. All token state, GIS clients, and file ID cache are per-instance (not module-level), which simplifies testing.

```typescript
import { createDriveSync } from '../../.toolbox/lib/google-drive-sync';

const driveSync = createDriveSync<MyData>({
  clientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
  fileName: 'my-app-data.json',
  mimeType: 'application/json',
  scope: 'https://www.googleapis.com/auth/drive.appdata',
  tokenExchangeUrl: import.meta.env.VITE_TOKEN_EXCHANGE_URL ?? '',
  appId: 'my-app',
  storageKeyPrefix: 'myapp-drive',
  logPrefix: '[MyApp]',
  sanitize: sanitizeMyData,
});

export const {
  isAuthenticated, getAuthLevel, initDriveAuth, silentRefresh,
  requestAccessToken, disconnectDrive, loadFromDrive, saveToDrive,
} = driveSync;
```

#### Config fields

| Field | Purpose |
|-------|---------|
| `clientId` | Google OAuth client ID |
| `fileName` | Name of the JSON file in Drive appDataFolder |
| `mimeType` | MIME type for the file |
| `scope` | OAuth scope (typically `drive.appdata`) |
| `tokenExchangeUrl` | Cloud Function URL (empty = implicit flow fallback) |
| `appId` | Sent as `X-App-Id` header for per-app logging |
| `storageKeyPrefix` | Prefix for localStorage keys (e.g., `myapp-drive`) |
| `logPrefix` | Console log prefix (e.g., `[MyApp]`) |
| `sanitize` | App-specific data validation/migration function |

#### Returned API

| Method | Description |
|--------|-------------|
| `isAuthenticated()` | Check if access token is valid |
| `getAuthLevel()` | Diagnostics: 0-3 persistence level |
| `getAccessToken()` | Current access token (for debug helpers) |
| `initDriveAuth()` | Initialize GIS client |
| `silentRefresh()` | Restore/refresh token without user interaction |
| `requestAccessToken(prompt)` | `''` = silent, `'consent'` = popup |
| `disconnectDrive()` | Revoke token + clear storage |
| `loadFromDrive()` | Load data from Drive appDataFolder |
| `saveToDrive(data)` | Save data to Drive appDataFolder |

### `createLocalStorage<T>(config)` Factory

Creates localStorage persistence with version checking and sanitization:

```typescript
import { createLocalStorage } from '../../.toolbox/lib/local-storage-sync';

const localSync = createLocalStorage<MyData>({
  storageKey: 'my-app-data',
  logPrefix: '[MyApp]',
  version: 1,
  sanitize: sanitizeMyData,
  createDefault: createDefaultData,
});

export const { saveToLocal, loadFromLocal, clearLocal } = localSync;
```

### Auth level diagnostics

`getAuthLevel()` returns 0-3 based on **actual state**, not just config:

| Level | Meaning | Condition |
|-------|---------|-----------|
| 0 | Storage unavailable | localStorage throws on write |
| 1 | Local only | No GIS loaded or no client ID configured |
| 2 | Session sync | GIS available but no refresh token stored (implicit flow, or code flow pre-connect) |
| 3 | Persistent sync | Code flow configured AND refresh token in localStorage |

Level 3 requires a stored refresh token -- not just `tokenExchangeUrl` being set. This accurately reflects whether the user will survive a page refresh without re-authenticating.

### Testing with factories

The factory pattern eliminates `vi.resetModules()` / dynamic `import()` for testing different configs. Each test creates a fresh instance:

```typescript
import { createDriveSync } from '../../.toolbox/lib/google-drive-sync';

function createSync(overrides = {}) {
  return createDriveSync<TestData>({
    clientId: 'test-client-id',
    fileName: 'test.json',
    mimeType: 'application/json',
    scope: 'https://www.googleapis.com/auth/drive.appdata',
    tokenExchangeUrl: 'https://example.com/token-exchange',
    appId: 'test',
    storageKeyPrefix: 'test-drive',
    logPrefix: '[Test]',
    sanitize: (d) => d,
    ...overrides,
  });
}

it('uses implicit flow when tokenExchangeUrl is empty', () => {
  const sync = createSync({ tokenExchangeUrl: '' });
  // ... no module cache tricks needed
});
```

### Redirect URI

For GIS popup-based code flow, use `'postmessage'` as the `redirect_uri` in the exchange request. This is the standard convention -- no redirect URI registration needed in GCP Console for popup mode.

## Cloud Function

### Source

The function source lives in `.toolbox/google-cloud-auth/function/`. It exports a single `tokenExchange` HTTP handler with two actions:

```
POST /token-exchange
Content-Type: application/json
X-App-Id: my-app

{ "action": "exchange", "code": "...", "redirect_uri": "postmessage" }
{ "action": "refresh", "refresh_token": "..." }
```

### CORS

Origins are configured via the `ALLOWED_ORIGINS` env var (comma-separated). Must be explicitly set -- defaults to empty (fail closed). Localhost is always allowed for dev. When adding a custom domain, update both `ALLOWED_ORIGINS` and the Google OAuth console -- see the [custom domain checklist](github-pages.md#5-oauth--cors-if-applicable).

To add origins without changing code, update the env var and redeploy:

```bash
gcloud functions deploy my-function --update-env-vars "^;^ALLOWED_ORIGINS=https://my-user.github.io,https://app.example.com"
```

### Security

- **Client secret**: loaded from GCP Secret Manager at runtime, never exposed to the browser
- **CORS**: explicit origin allowlist (no `*`), returns specific requesting origin
- **Origin validation**: rejects requests from unlisted origins
- **X-App-Id header**: logged for per-app usage tracking (not authenticated)

## GCP Project Strategy

### One project per app (recommended)

Each app that needs OAuth gets its own GCP project. This ensures:

- **Consent screen isolation**: users see only the scopes the specific app requests, with app-appropriate branding
- **Scope isolation**: one app requesting `drive.appdata` doesn't show calendar scopes from another app
- **Independent secret management**: each project's secrets are self-contained

Each project deploys its own instance of the shared Cloud Function source. The function code is identical -- only the deploy config differs.

Google's own guidance: consent is managed at the project level. Use separate projects for apps with distinct brands or different scope requirements.

### When to share a project

Multiple apps can share a single GCP project (and function deployment) when they:
- Use the same OAuth scopes
- Can share a consent screen (same branding)
- Are comfortable with shared consent grants (granting one app implicitly trusts others in the project)

## GCP Setup (Per Project)

### Prerequisites

- GCP project with OAuth 2.0 Web Application client
- `gcloud` CLI authenticated
- Google Drive API enabled (or whichever API the app needs)

### 1. Enable APIs

```bash
gcloud services enable \
  cloudfunctions.googleapis.com \
  cloudbuild.googleapis.com \
  secretmanager.googleapis.com \
  run.googleapis.com
```

### 2. Create secrets

```bash
echo -n "YOUR_CLIENT_ID" | gcloud secrets create my-client-id --data-file=-
echo -n "YOUR_CLIENT_SECRET" | gcloud secrets create my-client-secret --data-file=-
```

### 3. Grant secret access

```bash
PROJECT_NUMBER=$(gcloud projects describe $(gcloud config get-value project) \
  --format='value(projectNumber)')

for SECRET in my-client-id my-client-secret; do
  gcloud secrets add-iam-policy-binding $SECRET \
    --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
    --role="roles/secretmanager.secretAccessor"
done
```

### 4. Deploy

Each consuming project has a `google-cloud-auth.config.json` at the repo root:

```json
{
  "functionName": "my-app-token-exchange",
  "entryPoint": "tokenExchange",
  "secrets": "GOOGLE_CLIENT_ID=my-client-id:latest,GOOGLE_CLIENT_SECRET=my-client-secret:latest",
  "envVars": "ALLOWED_ORIGINS=https://my-user.github.io"
}
```

The `entryPoint` must be `tokenExchange` (matching the shared source export). The `functionName` is per-project and determines the deployed URL. `envVars` sets non-secret environment variables (like `ALLOWED_ORIGINS`) during deploy.

Deploy from the consuming project root:

```bash
powershell -ExecutionPolicy Bypass -File .toolbox/google-cloud-auth/deploy.ps1
```

The script auto-discovers the function source relative to its own location in the submodule, and reads `google-cloud-auth.config.json` from the current directory. It handles `npm install`, build, and `gcloud` deploy. Region defaults to us-central1, runtime to nodejs22 -- override by adding those fields to the config.

### 5. Wire up deployment

Add `TOKEN_EXCHANGE_URL` (the deployed function URL) as a secret/env var in the app's CI pipeline.

## Deployment Gotchas

Lessons from first deployment (ohm project, March 2025):

- **Gen2 deploys are slow**: 2-5 minutes is normal. The first deploy provisions a Cloud Build, builds a container, and pushes to Cloud Run. Don't assume it hung.
- **Node.js runtime deprecation**: Google deprecates Node.js versions on the community EOL date. Node 20 EOL is April 2026. Use `nodejs22` to stay current. The deploy script defaults to nodejs22.
- **PowerShell quoting**: the `--set-secrets` flag uses commas. PowerShell splits on unquoted commas. If deploying manually, quote the value: `--set-secrets="KEY1=val:latest,KEY2=val:latest"`. The shared deploy script handles this.
- **`--set-env-vars` comma escaping**: gcloud uses commas to separate `KEY=VALUE` pairs. If a value contains commas (e.g., multiple origins in `ALLOWED_ORIGINS`), prefix the value with `^;^` to use `;` as the pair delimiter instead: `"envVars": "^;^ALLOWED_ORIGINS=https://app.example.com,https://fallback.example.com"`. See [gcloud topic escaping](https://cloud.google.com/sdk/gcloud/reference/topic/escaping).
- **No CI deploy**: Cloud Function changes are infrequent and require `gcloud` auth. Manual deploy from the submodule source directory is sufficient. Note this in the consuming project's CLAUDE.md.
- **Vitest test leakage**: if the cloud function directory has its own `node_modules`, vitest may pick up tests from those dependencies. Add `'google-cloud-auth'` to the vitest `exclude` array.
- **`gcloud` on Windows**: the default installer puts the CLI in `%LOCALAPPDATA%\Google\Cloud SDK\`. The `setup-path.sh` hook adds it to Claude's bash PATH. The deploy script uses PowerShell where gcloud is on the user PATH natively.
- **Function renaming**: GCP does not support in-place renames. Deploy a new function with the new name, update `VITE_TOKEN_EXCHANGE_URL` in consuming apps, then delete the old function: `gcloud functions delete old-name --region us-central1 --gen2`.

## Free Tier Limits

Cloud Run functions gen2 free tier (us-central1, per billing account, monthly):

| Resource | Free allocation |
|----------|----------------|
| Requests | 2,000,000 |
| vCPU-seconds | 180,000 |
| Memory (GiB-seconds) | 360,000 |
| Cloud Monitoring metrics | Free for Cloud Run |
| Cloud Logging | 50 GiB/month |

A token exchange function uses ~2 requests per user session (initial exchange + occasional refresh). Even with multiple apps each deploying their own function, this is negligible relative to the free tier.

**Free tier regions**: us-central1, us-east1, us-west1. Deploy in one of these to stay free.

## Monitoring

### Built-in (free)

- **Cloud Run metrics dashboard**: invocation count, error rate, latency per function
- **Cloud Logging**: structured logs with `[app-id]` prefix for per-app breakdowns
- **Billing budget alert**: set a $1 threshold to catch unexpected usage

### Per-app tracking

Clients send an `X-App-Id` header. The function logs it with every request. Use Cloud Logging filters to break down usage:

```
resource.type="cloud_run_revision"
textPayload=~"\[my-app\]"
```

### Recommended alert

Create a budget alert at the billing account level:

```bash
# In GCP Console: Billing > Budgets & alerts > Create budget
# Set amount: $1.00
# Alert at: 50%, 90%, 100%
# Email notifications to billing admins
```

## Edge Cases

- **Refresh token revoked** (user revokes in Google Account settings): Cloud Function returns 400/401. Client clears stored tokens, shows reconnect UI.
- **Missing refresh token on re-grant**: Google may omit the refresh token on subsequent grants. Client preserves any existing refresh token -- does not overwrite with undefined.
- **Concurrent refresh calls**: Deduplicated via a shared in-flight promise. Only one Cloud Function call at a time.
- **Cloud Function unreachable**: `silentRefresh()` catches the error, returns null. App falls back to reconnect UI.
- **No `tokenExchangeUrl`**: implicit flow, identical to pre-migration behavior. Zero breaking change.

## Reference Implementation

First deployed in the [ohm](https://github.com/samm-the-dev/ohm) project (PR #11). Key files:

- `google-cloud-auth.config.json` -- per-project deploy parameters
- `src/utils/google-drive.ts` -- thin wrapper using `createDriveSync<OhmBoard>()`
- `src/utils/storage.ts` -- thin wrapper using `createLocalStorage<OhmBoard>()`
- `src/utils/google-drive.test.ts` -- 19 tests covering both flows (factory-based, no module cache tricks)
- `src/config/drive.ts` -- env var config
