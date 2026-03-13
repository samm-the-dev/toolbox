/**
 * Generic Google Drive sync factory.
 *
 * Provides Drive appDataFolder CRUD on top of a shared GoogleAuth instance.
 * Auth can be provided externally (shared across connectors) or created
 * internally from config fields (backward compatible).
 *
 * See ai-context/google-cloud-auth.md (or .toolbox/ai-context/google-cloud-auth.md in consuming repos).
 */

import { createGoogleAuth, type GoogleAuth } from './google-oauth';

export type { GoogleAuth } from './google-oauth';

export interface DriveSyncConfig<T> {
  /** Pre-built auth instance. If provided, auth-related fields below are ignored. */
  auth?: GoogleAuth;
  clientId?: string;
  fileName: string;
  mimeType: string;
  scope?: string;
  tokenExchangeUrl?: string;
  appId?: string;
  /** Prefix for localStorage keys (e.g., 'myapp-drive') */
  storageKeyPrefix?: string;
  /** Console log prefix (e.g., '[MyApp]') */
  logPrefix?: string;
  /** Validate/migrate loaded data to the expected shape */
  sanitize: (data: T) => T;
}

export interface DriveSync<T> {
  /** The underlying auth instance, for sharing with other connectors. */
  auth: GoogleAuth;
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
  const { fileName, mimeType, sanitize } = config;
  const logPrefix = config.logPrefix ?? '';

  // --- Auth: use provided instance or create one from legacy config fields ---

  const auth: GoogleAuth =
    config.auth ??
    createGoogleAuth({
      clientId: config.clientId ?? '',
      scope: config.scope ?? '',
      tokenExchangeUrl: config.tokenExchangeUrl ?? '',
      appId: config.appId ?? '',
      storageKeyPrefix: config.storageKeyPrefix ?? '',
      logPrefix,
    });

  // --- Drive file operations ---

  let cachedFileId: string | null = null;

  async function ensureHeaders(): Promise<HeadersInit> {
    if (!auth.isAuthenticated()) {
      const token = (await auth.silentRefresh()) ?? (await auth.requestAccessToken());
      if (!token) throw new Error('Not authenticated with Google Drive');
      return { Authorization: `Bearer ${token}` };
    }
    const headers = auth.getHeaders();
    if (!headers) throw new Error('Not authenticated with Google Drive');
    return headers;
  }

  async function findFileId(): Promise<string | null> {
    if (cachedFileId) return cachedFileId;

    const headers = await ensureHeaders();
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

    const headers = await ensureHeaders();
    const res = await fetch(`https://www.googleapis.com/drive/v3/files/${fileId}?alt=media`, {
      headers,
    });
    if (!res.ok) return null;

    const data = (await res.json()) as T;
    return sanitize(data);
  }

  async function saveToDrive(data: T): Promise<boolean> {
    const headers = await ensureHeaders();
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
          auth.invalidateToken();
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

  function disconnectDrive(): void {
    auth.disconnect();
    cachedFileId = null;
  }

  return {
    auth,
    isAuthenticated: () => auth.isAuthenticated(),
    getAuthLevel: () => auth.getAuthLevel(),
    getAccessToken: () => auth.getAccessToken(),
    initDriveAuth: () => auth.initAuth(),
    silentRefresh: () => auth.silentRefresh(),
    requestAccessToken: (prompt?) => auth.requestAccessToken(prompt),
    disconnectDrive,
    loadFromDrive,
    saveToDrive,
  };
}
