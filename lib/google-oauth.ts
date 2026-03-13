/**
 * Generic Google OAuth token management.
 *
 * Provides dual-flow OAuth (authorization code + implicit fallback),
 * token persistence, silent refresh, and incremental consent.
 * Consumers use this to share a single auth instance across multiple
 * Google API connectors (Drive, Calendar, etc.).
 *
 * See ai-context/google-cloud-auth.md for architecture details.
 */

export interface GoogleAuthConfig {
  clientId: string;
  /** Space-separated OAuth scopes */
  scope: string;
  /** Cloud function URL for code flow token exchange (empty = implicit flow only) */
  tokenExchangeUrl: string;
  /** App identifier sent as X-App-Id header to the cloud function */
  appId: string;
  /** Prefix for localStorage keys (e.g., 'ohm-drive') */
  storageKeyPrefix: string;
  /** Console log prefix (e.g., '[Ohm]') */
  logPrefix: string;
}

export interface GoogleAuth {
  isAuthenticated(): boolean;
  getAuthLevel(): 0 | 1 | 2 | 3;
  getAccessToken(): string | null;
  /** Convenience: returns Authorization header or null if no valid token. */
  getHeaders(): { Authorization: string } | null;
  initAuth(): boolean;
  silentRefresh(): Promise<string | null>;
  requestAccessToken(prompt?: '' | 'consent'): Promise<string | null>;
  disconnect(): void;
  /** Clear the in-memory token without revoking or touching localStorage. */
  invalidateToken(): void;
  /**
   * Request an additional scope via incremental consent.
   * Uses include_granted_scopes: true so existing scopes are preserved.
   * Returns the new access token (which covers all granted scopes) or null.
   */
  requestIncrementalScope(additionalScope: string): Promise<string | null>;
}

export function createGoogleAuth(config: GoogleAuthConfig): GoogleAuth {
  const { clientId, scope, tokenExchangeUrl, appId, storageKeyPrefix, logPrefix } = config;

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

  function getHeaders(): { Authorization: string } | null {
    if (!accessToken) return null;
    return { Authorization: `Bearer ${accessToken}` };
  }

  function initAuth(): boolean {
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
            console.error(`${logPrefix} Auth error:`, response.error_description);
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

  // --- Incremental consent ---

  function requestIncrementalScope(additionalScope: string): Promise<string | null> {
    if (typeof google === 'undefined' || !google.accounts?.oauth2) {
      return Promise.resolve(null);
    }

    if (useCodeFlow) {
      return new Promise((resolve) => {
        const tempClient = google.accounts.oauth2.initCodeClient({
          client_id: clientId,
          scope: additionalScope,
          ux_mode: 'popup',
          include_granted_scopes: true,
          callback: async (response) => {
            if (response.error) {
              console.error(`${logPrefix} Incremental scope error:`, response.error_description);
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
                console.error(`${logPrefix} Incremental scope exchange failed:`, res.status, err);
                resolve(null);
                return;
              }

              const data = await res.json();
              if (typeof data.access_token !== 'string' || typeof data.expires_in !== 'number') {
                console.error(
                  `${logPrefix} Incremental scope exchange returned invalid payload:`,
                  data,
                );
                resolve(null);
                return;
              }
              accessToken = data.access_token;
              tokenExpiry = Date.now() + data.expires_in * 1000;
              storeTokens(data.access_token, tokenExpiry, data.refresh_token);
              resolve(data.access_token);
            } catch (err) {
              console.error(`${logPrefix} Incremental scope exchange network error:`, err);
              resolve(null);
            }
          },
        });

        tempClient.requestCode();
      });
    }

    // --- Implicit flow fallback ---
    return new Promise((resolve) => {
      const tempClient = google.accounts.oauth2.initTokenClient({
        client_id: clientId,
        scope: additionalScope,
        include_granted_scopes: true,
        callback: (response) => {
          if (response.error) {
            console.error(`${logPrefix} Incremental scope error:`, response.error_description);
            accessToken = null;
            resolve(null);
            return;
          }
          accessToken = response.access_token;
          tokenExpiry = Date.now() + response.expires_in * 1000;
          resolve(accessToken);
        },
      });

      tempClient.requestAccessToken({ prompt: 'consent' });
    });
  }

  // --- Token invalidation ---

  function invalidateToken(): void {
    accessToken = null;
    tokenExpiry = 0;
  }

  // --- Disconnect ---

  function disconnect(): void {
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
    if (useCodeFlow) {
      clearStoredTokens();
    }
  }

  return {
    isAuthenticated,
    getAuthLevel,
    getAccessToken: getAccessTokenValue,
    getHeaders,
    initAuth,
    silentRefresh,
    requestAccessToken,
    invalidateToken,
    disconnect,
    requestIncrementalScope,
  };
}
