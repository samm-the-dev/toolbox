/**
 * Generic Google Drive sync factory.
 *
 * Provides dual-flow OAuth (authorization code + implicit fallback)
 * and Drive appDataFolder CRUD, parameterized by a config object.
 * Consumer provides app-specific values (client ID, file name, sanitizer).
 *
 * See toolbox/templates/ai-context/google-cloud-auth.md
 */

export interface DriveSyncConfig<T> {
  clientId: string;
  fileName: string;
  mimeType: string;
  scope: string;
  tokenExchangeUrl: string;
  appId: string;
  /** Prefix for localStorage keys (e.g., 'myapp-drive') */
  storageKeyPrefix: string;
  /** Console log prefix (e.g., '[MyApp]') */
  logPrefix: string;
  /** Validate/migrate loaded data to the expected shape */
  sanitize: (data: T) => T;
}

export interface DriveSync<T> {
  isAuthenticated(): boolean;
  getAuthLevel(): 0 | 1 | 2 | 3;
  getAccessToken(): string | null;
  initDriveAuth(): boolean;
  silentRefresh(): Promise<string | null>;
  requestAccessToken(prompt?: '' | 'consent'): Promise<string | null>;
  disconnectDrive(): void;
  loadFromDrive(): Promise<T | null>;
  saveToDrive(data: T): Promise<boolean>;
}

export function createDriveSync<T>(config: DriveSyncConfig<T>): DriveSync<T> {
  const {
    clientId,
    fileName,
    mimeType,
    scope,
    tokenExchangeUrl,
    appId,
    storageKeyPrefix,
    logPrefix,
    sanitize,
  } = config;

  const useCodeFlow = !!tokenExchangeUrl;

  // --- Token state ---

  let accessToken: string | null = null;
  let tokenExpiry = 0;

  let tokenClient: google.accounts.oauth2.TokenClient | null = null;
  let codeClient: google.accounts.oauth2.CodeClient | null = null;

  const REFRESH_TOKEN_KEY = `${storageKeyPrefix}-refresh-token`;
  const ACCESS_TOKEN_KEY = `${storageKeyPrefix}-access-token`;
  const TOKEN_EXPIRY_KEY = `${storageKeyPrefix}-token-expiry`;

  // --- Public API ---

  function isAuthenticated(): boolean {
    return !!accessToken && Date.now() < tokenExpiry;
  }

  /**
   * Detect the current persistence level for diagnostics.
   * 0 = localStorage unavailable
   * 1 = localStorage only (no cloud sync possible)
   * 2 = OAuth popup sync (tokens lost on refresh)
   * 3 = Persistent auth via Cloud Function (silent reconnect)
   */
  function getAuthLevel(): 0 | 1 | 2 | 3 {
    try {
      const key = `__${storageKeyPrefix}_ls_test__`;
      localStorage.setItem(key, '1');
      localStorage.removeItem(key);
    } catch {
      return 0;
    }
    if (!clientId || typeof google === 'undefined' || !google.accounts?.oauth2) {
      return 1;
    }
    if (useCodeFlow) {
      return localStorage.getItem(REFRESH_TOKEN_KEY) ? 3 : 1;
    }
    return accessToken ? 2 : 1;
  }

  function getAccessTokenValue(): string | null {
    return accessToken;
  }

  function initDriveAuth(): boolean {
    if (!clientId) return false;
    if (typeof google === 'undefined' || !google.accounts?.oauth2) return false;

    if (useCodeFlow) {
      codeClient = google.accounts.oauth2.initCodeClient({
        client_id: clientId,
        scope,
        ux_mode: 'popup',
        callback: () => {},
      });
    } else {
      tokenClient = google.accounts.oauth2.initTokenClient({
        client_id: clientId,
        scope,
        callback: () => {},
      });
    }
    return true;
  }

  // --- Token persistence helpers ---

  function storeTokens(access: string, expiry: number, refresh?: string): void {
    try {
      localStorage.setItem(ACCESS_TOKEN_KEY, access);
      localStorage.setItem(TOKEN_EXPIRY_KEY, String(expiry));
      if (refresh) {
        localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
      }
    } catch {
      // Swallow storage errors (private mode, quota exceeded) -- in-memory tokens still work.
    }
  }

  function clearStoredTokens(): void {
    try {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(TOKEN_EXPIRY_KEY);
    } catch {
      // Ignore storage errors when clearing tokens.
    }
  }

  // --- Silent refresh (code flow) ---

  let refreshPromise: Promise<string | null> | null = null;

  function silentRefresh(): Promise<string | null> {
    if (!useCodeFlow) return Promise.resolve(null);
    if (refreshPromise) return refreshPromise;
    refreshPromise = doSilentRefresh().finally(() => {
      refreshPromise = null;
    });
    return refreshPromise;
  }

  async function doSilentRefresh(): Promise<string | null> {
    let cachedToken: string | null = null;
    let cachedExpiry = 0;
    let refreshToken: string | null = null;

    try {
      cachedToken = localStorage.getItem(ACCESS_TOKEN_KEY);
      cachedExpiry = Number(localStorage.getItem(TOKEN_EXPIRY_KEY) || '0');
      refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    } catch {
      return null;
    }

    if (cachedToken && Date.now() < cachedExpiry - 60_000) {
      accessToken = cachedToken;
      tokenExpiry = cachedExpiry;
      return accessToken;
    }

    if (!refreshToken) return null;

    try {
      const res = await fetch(tokenExchangeUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-App-Id': appId },
        body: JSON.stringify({ action: 'refresh', refresh_token: refreshToken }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => null);
        console.error(`${logPrefix} Silent refresh failed:`, res.status, err);
        if (res.status === 400 || res.status === 401) {
          clearStoredTokens();
        }
        return null;
      }

      const data = await res.json();
      if (typeof data.access_token !== 'string' || typeof data.expires_in !== 'number') {
        console.error(`${logPrefix} Silent refresh returned invalid payload:`, data);
        return null;
      }
      accessToken = data.access_token;
      tokenExpiry = Date.now() + data.expires_in * 1000;
      storeTokens(data.access_token, tokenExpiry);
      return data.access_token;
    } catch (err) {
      console.error(`${logPrefix} Silent refresh network error:`, err);
      return null;
    }
  }

  // --- Token request ---

  function requestAccessToken(prompt: '' | 'consent' = 'consent'): Promise<string | null> {
    if (useCodeFlow) {
      if (prompt === '') {
        return silentRefresh();
      }

      return new Promise((resolve) => {
        if (!codeClient) {
          resolve(null);
          return;
        }

        codeClient.callback = async (response) => {
          if (response.error) {
            console.error(`${logPrefix} Code auth error:`, response.error_description);
            resolve(null);
            return;
          }

          try {
            const res = await fetch(tokenExchangeUrl, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json', 'X-App-Id': appId },
              body: JSON.stringify({
                action: 'exchange',
                code: response.code,
                redirect_uri: 'postmessage',
              }),
            });

            if (!res.ok) {
              const err = await res.json().catch(() => null);
              console.error(`${logPrefix} Token exchange failed:`, res.status, err);
              resolve(null);
              return;
            }

            const data = await res.json();
            if (typeof data.access_token !== 'string' || typeof data.expires_in !== 'number') {
              console.error(`${logPrefix} Token exchange returned invalid payload:`, data);
              resolve(null);
              return;
            }
            accessToken = data.access_token;
            tokenExpiry = Date.now() + data.expires_in * 1000;
            storeTokens(data.access_token, tokenExpiry, data.refresh_token);
            resolve(data.access_token);
          } catch (err) {
            console.error(`${logPrefix} Token exchange network error:`, err);
            resolve(null);
          }
        };

        codeClient.requestCode();
      });
    }

    // --- Implicit flow (fallback) ---
    return new Promise((resolve) => {
      if (!tokenClient) {
        resolve(null);
        return;
      }

      tokenClient.callback = (response) => {
        if (response.error) {
          if (prompt === 'consent') {
            console.error(`${logPrefix} Drive auth error:`, response.error_description);
          }
          accessToken = null;
          resolve(null);
          return;
        }
        accessToken = response.access_token;
        tokenExpiry = Date.now() + response.expires_in * 1000;
        resolve(accessToken);
      };

      tokenClient.requestAccessToken({ prompt });
    });
  }

  function disconnectDrive(): void {
    if (accessToken) {
      try {
        if (typeof google !== 'undefined' && google.accounts?.oauth2?.revoke) {
          google.accounts.oauth2.revoke(accessToken);
        }
      } catch {
        // Revoke is best-effort; token will expire on its own.
      }
    }
    accessToken = null;
    tokenExpiry = 0;
    cachedFileId = null;
    if (useCodeFlow) {
      clearStoredTokens();
    }
  }

  // --- Drive file operations ---

  let cachedFileId: string | null = null;

  async function getHeaders(): Promise<HeadersInit> {
    if (!isAuthenticated()) {
      if (useCodeFlow) {
        const token = await silentRefresh();
        if (token) return { Authorization: `Bearer ${token}` };
      }
      const token = await requestAccessToken();
      if (!token) throw new Error('Not authenticated with Google Drive');
    }
    return { Authorization: `Bearer ${accessToken}` };
  }

  async function findFileId(): Promise<string | null> {
    if (cachedFileId) return cachedFileId;

    const headers = await getHeaders();
    const params = new URLSearchParams({
      spaces: 'appDataFolder',
      q: `name='${fileName.replace(/'/g, "\\'")}' and trashed=false`,
      fields: 'files(id,modifiedTime)',
      orderBy: 'modifiedTime desc',
      pageSize: '1',
    });

    const res = await fetch(`https://www.googleapis.com/drive/v3/files?${params}`, { headers });
    if (!res.ok) {
      const err = await res.json().catch(() => null);
      console.error(`${logPrefix} Drive list failed:`, res.status, err);
      throw new Error(`Drive list failed: ${res.status}`);
    }

    const data = await res.json();
    cachedFileId = data.files?.[0]?.id ?? null;
    return cachedFileId;
  }

  async function loadFromDrive(): Promise<T | null> {
    const fileId = await findFileId();
    if (!fileId) return null;

    const headers = await getHeaders();
    const res = await fetch(`https://www.googleapis.com/drive/v3/files/${fileId}?alt=media`, {
      headers,
    });
    if (!res.ok) return null;

    const data = (await res.json()) as T;
    return sanitize(data);
  }

  async function saveToDrive(data: T): Promise<boolean> {
    const headers = await getHeaders();
    const fileId = await findFileId();
    const body = JSON.stringify(data);

    if (fileId) {
      const res = await fetch(
        `https://www.googleapis.com/upload/drive/v3/files/${fileId}?uploadType=media`,
        {
          method: 'PATCH',
          headers: { ...headers, 'Content-Type': mimeType },
          body,
        },
      );
      if (!res.ok) {
        const err = await res.json().catch(() => null);
        console.error(`${logPrefix} Drive save failed:`, res.status, err);
        if (res.status === 404) {
          cachedFileId = null;
        } else if (res.status === 401 || res.status === 403) {
          accessToken = null;
          tokenExpiry = 0;
          return false;
        } else {
          return false;
        }
      } else {
        return true;
      }
    }

    const metadata = {
      name: fileName,
      parents: ['appDataFolder'],
      mimeType,
    };

    const form = new FormData();
    form.append('metadata', new Blob([JSON.stringify(metadata)], { type: 'application/json' }));
    form.append('file', new Blob([body], { type: mimeType }));

    const res = await fetch(
      'https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id',
      { method: 'POST', headers, body: form },
    );
    if (!res.ok) {
      const err = await res.json().catch(() => null);
      console.error(`${logPrefix} Drive create failed:`, res.status, err);
    } else {
      const created = await res.json();
      cachedFileId = created.id;
    }
    return res.ok;
  }

  return {
    isAuthenticated,
    getAuthLevel,
    getAccessToken: getAccessTokenValue,
    initDriveAuth,
    silentRefresh,
    requestAccessToken,
    disconnectDrive,
    loadFromDrive,
    saveToDrive,
  };
}
