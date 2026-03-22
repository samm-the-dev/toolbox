/**
 * Tests for the Google OAuth factory.
 *
 * Covers the new functionality introduced by the extraction:
 * getHeaders, invalidateToken, requestIncrementalScope,
 * and requestAccessToken deduplication.
 */

import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { createGoogleAuth } from './google-oauth';
import type { GoogleAuthConfig } from './google-oauth';
import type { StorageService } from './storage-service/types';

// --- GIS mocks ---

const mockRequestCode = vi.fn();
const mockRequestAccessToken = vi.fn();
const mockRevoke = vi.fn();
const mockInitCodeClient = vi.fn();
const mockInitTokenClient = vi.fn();
const mockCodeClient = { requestCode: mockRequestCode, callback: vi.fn() };
const mockTokenClient = { requestAccessToken: mockRequestAccessToken, callback: vi.fn() };

function setupGoogleGlobal() {
  mockInitCodeClient.mockReturnValue(mockCodeClient);
  mockInitTokenClient.mockReturnValue(mockTokenClient);
  (globalThis as Record<string, unknown>).google = {
    accounts: {
      oauth2: {
        initCodeClient: mockInitCodeClient,
        initTokenClient: mockInitTokenClient,
        revoke: mockRevoke,
      },
    },
  };
}

// --- Factory helper ---

const KEY_PREFIX = 'test-auth';
const REFRESH_KEY = `${KEY_PREFIX}-refresh-token`;
const ACCESS_KEY = `${KEY_PREFIX}-access-token`;
const EXPIRY_KEY = `${KEY_PREFIX}-token-expiry`;
const TOKEN_URL = 'https://example.com/token-exchange';

function createAuth(overrides: Partial<GoogleAuthConfig> = {}) {
  return createGoogleAuth({
    clientId: 'test-client-id',
    scope: 'https://www.googleapis.com/auth/drive.appdata',
    tokenExchangeUrl: TOKEN_URL,
    appId: 'test',
    storageKeyPrefix: KEY_PREFIX,
    logPrefix: '[Test]',
    ...overrides,
  });
}

describe('getAuthLevel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns 0 when localStorage throws', () => {
    vi.stubGlobal('localStorage', {
      setItem: () => { throw new Error('SecurityError'); },
      getItem: () => null,
      removeItem: () => {},
      clear: () => {},
    });

    const auth = createAuth();
    expect(auth.getAuthLevel()).toBe(0);
  });

  it('returns 1 when clientId is falsy', () => {
    const auth = createAuth({ clientId: '' });
    expect(auth.getAuthLevel()).toBe(1);
  });

  it('returns 1 when google.accounts.oauth2 is unavailable', () => {
    delete (globalThis as Record<string, unknown>).google;
    const auth = createAuth();
    expect(auth.getAuthLevel()).toBe(1);
  });

  it('returns 1 in code flow when no refresh token is stored', () => {
    const auth = createAuth();
    expect(auth.getAuthLevel()).toBe(1);
  });

  it('returns 3 in code flow when a refresh token exists in localStorage', () => {
    localStorage.setItem(REFRESH_KEY, 'stored-refresh');
    const auth = createAuth();
    expect(auth.getAuthLevel()).toBe(3);
  });

  it('returns 2 in implicit flow when an access token is held in memory', async () => {
    const auth = createAuth({ tokenExchangeUrl: '' });
    auth.initAuth();

    const p = auth.requestAccessToken('consent');
    mockTokenClient.callback({ access_token: 'mem-token', expires_in: 3600 });
    await p;

    expect(auth.getAuthLevel()).toBe(2);
  });

  it('returns 1 in implicit flow when no access token is present', () => {
    const auth = createAuth({ tokenExchangeUrl: '' });
    expect(auth.getAuthLevel()).toBe(1);
  });
});

describe('getHeaders', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('returns null when no token is set', () => {
    const auth = createAuth();
    expect(auth.getHeaders()).toBeNull();
  });

  it('returns Authorization header when authenticated', async () => {
    const futureExpiry = Date.now() + 3600_000;
    localStorage.setItem(ACCESS_KEY, 'valid-token');
    localStorage.setItem(EXPIRY_KEY, String(futureExpiry));
    localStorage.setItem(REFRESH_KEY, 'refresh-token');

    const auth = createAuth();
    await auth.silentRefresh();

    const headers = auth.getHeaders();
    expect(headers).toEqual({ Authorization: 'Bearer valid-token' });
  });

  it('returns null when token is expired', async () => {
    // Get a token into state, then let it expire
    localStorage.setItem(ACCESS_KEY, 'expired-token');
    localStorage.setItem(EXPIRY_KEY, String(Date.now() + 3600_000));
    localStorage.setItem(REFRESH_KEY, 'refresh-token');

    const auth = createAuth();
    await auth.silentRefresh();
    expect(auth.getHeaders()).not.toBeNull();

    // Invalidate to simulate expiry
    auth.invalidateToken();
    expect(auth.getHeaders()).toBeNull();
  });
});

describe('invalidateToken', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('clears in-memory token and persisted access token, keeps refresh token', async () => {
    const futureExpiry = Date.now() + 3600_000;
    localStorage.setItem(ACCESS_KEY, 'cached-token');
    localStorage.setItem(EXPIRY_KEY, String(futureExpiry));
    localStorage.setItem(REFRESH_KEY, 'refresh-token');

    const auth = createAuth();
    await auth.silentRefresh();
    expect(auth.isAuthenticated()).toBe(true);

    auth.invalidateToken();
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.getAccessToken()).toBeNull();

    // Persisted access token/expiry should be cleared
    expect(localStorage.getItem(ACCESS_KEY)).toBeNull();
    expect(localStorage.getItem(EXPIRY_KEY)).toBeNull();
    // Refresh token should be preserved for re-auth
    expect(localStorage.getItem(REFRESH_KEY)).toBe('refresh-token');
  });

  it('silentRefresh hits cloud function after invalidateToken (not stale cache)', async () => {
    const futureExpiry = Date.now() + 3600_000;
    localStorage.setItem(ACCESS_KEY, 'stale-token');
    localStorage.setItem(EXPIRY_KEY, String(futureExpiry));
    localStorage.setItem(REFRESH_KEY, 'refresh-token');

    const auth = createAuth();
    await auth.silentRefresh();
    expect(auth.getAccessToken()).toBe('stale-token');

    auth.invalidateToken();

    // Next silentRefresh should call the cloud function, not reuse cache
    (globalThis.fetch as Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ access_token: 'fresh-token', expires_in: 3600 }),
    });

    const result = await auth.silentRefresh();
    expect(result).toBe('fresh-token');
    expect(globalThis.fetch).toHaveBeenCalledWith(
      TOKEN_URL,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ action: 'refresh', refresh_token: 'refresh-token' }),
      }),
    );
  });
});

describe('requestAccessToken deduplication', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('deduplicates concurrent requestAccessToken calls (code flow)', async () => {
    (globalThis.fetch as Mock).mockResolvedValue({
      ok: true,
      json: async () => ({
        access_token: 'deduped-token',
        expires_in: 3600,
        refresh_token: 'new-refresh',
      }),
    });

    const auth = createAuth();
    auth.initAuth();

    const p1 = auth.requestAccessToken('consent');
    const p2 = auth.requestAccessToken('consent');

    // Only one popup should have been triggered
    expect(mockRequestCode).toHaveBeenCalledTimes(1);

    // Simulate the code callback
    mockCodeClient.callback({ code: 'auth-code', scope: 'drive.appdata' });

    const [r1, r2] = await Promise.all([p1, p2]);
    expect(r1).toBe('deduped-token');
    expect(r2).toBe('deduped-token');
  });

  it('deduplicates concurrent requestAccessToken calls (implicit flow)', async () => {
    const auth = createAuth({ tokenExchangeUrl: '' });
    auth.initAuth();

    const p1 = auth.requestAccessToken('consent');
    const p2 = auth.requestAccessToken('consent');

    // Only one request should have been triggered
    expect(mockRequestAccessToken).toHaveBeenCalledTimes(1);

    // Simulate the token callback
    mockTokenClient.callback({
      access_token: 'implicit-deduped',
      expires_in: 3600,
    });

    const [r1, r2] = await Promise.all([p1, p2]);
    expect(r1).toBe('implicit-deduped');
    expect(r2).toBe('implicit-deduped');
  });

  it('allows a new request after the previous one completes', async () => {
    (globalThis.fetch as Mock).mockResolvedValue({
      ok: true,
      json: async () => ({
        access_token: 'token-1',
        expires_in: 3600,
        refresh_token: 'rt',
      }),
    });

    const auth = createAuth();
    auth.initAuth();

    // First request
    const p1 = auth.requestAccessToken('consent');
    mockCodeClient.callback({ code: 'code-1', scope: 'drive.appdata' });
    await p1;

    // Second request should go through (not deduped)
    (globalThis.fetch as Mock).mockResolvedValue({
      ok: true,
      json: async () => ({
        access_token: 'token-2',
        expires_in: 3600,
        refresh_token: 'rt',
      }),
    });

    const p2 = auth.requestAccessToken('consent');
    expect(mockRequestCode).toHaveBeenCalledTimes(2);
    mockCodeClient.callback({ code: 'code-2', scope: 'drive.appdata' });
    const r2 = await p2;
    expect(r2).toBe('token-2');
  });
});

describe('requestIncrementalScope', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('creates a temporary code client with include_granted_scopes (code flow)', async () => {
    (globalThis.fetch as Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        access_token: 'calendar-token',
        expires_in: 3600,
        refresh_token: 'cal-refresh',
      }),
    });

    // Capture the callback passed in the config to initCodeClient
    let capturedCallback: (response: { code: string; scope: string }) => void = () => {};
    mockInitCodeClient.mockImplementationOnce(
      (config: { callback: typeof capturedCallback }) => {
        capturedCallback = config.callback;
        return { requestCode: vi.fn() };
      },
    );

    const auth = createAuth();
    const promise = auth.requestIncrementalScope(
      'https://www.googleapis.com/auth/calendar.readonly',
    );

    expect(mockInitCodeClient).toHaveBeenCalledWith(
      expect.objectContaining({
        scope: 'https://www.googleapis.com/auth/calendar.readonly',
        include_granted_scopes: true,
      }),
    );

    // Simulate the code callback
    capturedCallback({ code: 'cal-code', scope: 'calendar.readonly' });

    const result = await promise;
    expect(result).toBe('calendar-token');
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('creates a temporary token client with include_granted_scopes (implicit flow)', async () => {
    // Capture the callback passed in the config to initTokenClient
    let capturedCallback: (response: {
      access_token: string;
      expires_in: number;
      error?: string;
    }) => void = () => {};
    mockInitTokenClient.mockImplementationOnce(
      (config: { callback: typeof capturedCallback }) => {
        capturedCallback = config.callback;
        return { requestAccessToken: vi.fn() };
      },
    );

    const auth = createAuth({ tokenExchangeUrl: '' });
    const promise = auth.requestIncrementalScope(
      'https://www.googleapis.com/auth/calendar.readonly',
    );

    expect(mockInitTokenClient).toHaveBeenCalledWith(
      expect.objectContaining({
        scope: 'https://www.googleapis.com/auth/calendar.readonly',
        include_granted_scopes: true,
      }),
    );

    // Simulate the token callback
    capturedCallback({
      access_token: 'implicit-cal-token',
      expires_in: 3600,
    });

    const result = await promise;
    expect(result).toBe('implicit-cal-token');
  });

  it('preserves existing token when incremental consent is denied (implicit flow)', async () => {
    const auth = createAuth({ tokenExchangeUrl: '' });
    auth.initAuth();

    // Get an initial token
    const tokenPromise = auth.requestAccessToken('consent');
    mockTokenClient.callback({ access_token: 'existing-token', expires_in: 3600 });
    await tokenPromise;
    expect(auth.isAuthenticated()).toBe(true);

    // Capture the incremental callback
    let capturedCallback: (response: {
      access_token: string;
      expires_in: number;
      error?: string;
      error_description?: string;
    }) => void = () => {};
    mockInitTokenClient.mockImplementationOnce(
      (config: { callback: typeof capturedCallback }) => {
        capturedCallback = config.callback;
        return { requestAccessToken: vi.fn() };
      },
    );

    // Now attempt incremental consent that fails
    const incPromise = auth.requestIncrementalScope('calendar.readonly');
    capturedCallback({
      access_token: '',
      expires_in: 0,
      error: 'access_denied',
      error_description: 'User denied',
    });
    const result = await incPromise;

    expect(result).toBeNull();
    // Existing token should still be valid
    expect(auth.getAccessToken()).toBe('existing-token');
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('returns null when GIS is not loaded', async () => {
    delete (globalThis as Record<string, unknown>).google;
    const auth = createAuth();
    const result = await auth.requestIncrementalScope('calendar.readonly');
    expect(result).toBeNull();
  });
});

describe('disconnect', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('revokes token, clears state, and clears localStorage (code flow)', async () => {
    localStorage.setItem(REFRESH_KEY, 'rt');
    localStorage.setItem(ACCESS_KEY, 'at');
    localStorage.setItem(EXPIRY_KEY, String(Date.now() + 3600_000));

    const auth = createAuth();
    await auth.silentRefresh();
    expect(auth.isAuthenticated()).toBe(true);

    auth.disconnect();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.getAccessToken()).toBeNull();
    expect(localStorage.getItem(REFRESH_KEY)).toBeNull();
    expect(localStorage.getItem(ACCESS_KEY)).toBeNull();
    expect(mockRevoke).toHaveBeenCalledWith('at');
  });
});

// --- StorageService integration ---

function createMockStorage(): StorageService & { store: Map<string, unknown> } {
  const store = new Map<string, unknown>();
  return {
    adapter: 'opfs',
    store,
    get: vi.fn(async <T>(key: string) => (store.get(key) as T) ?? null),
    set: vi.fn(async <T>(_key: string, value: T) => { store.set(_key, value); }),
    delete: vi.fn(async (key: string) => { store.delete(key); }),
    clear: vi.fn(async () => { store.clear(); }),
    keys: vi.fn(async () => [...store.keys()]),
  };
}

describe('StorageService token persistence', () => {
  let mockStorage: ReturnType<typeof createMockStorage>;

  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setupGoogleGlobal();
    vi.stubGlobal('fetch', vi.fn());
    mockStorage = createMockStorage();
  });

  it('storeTokens writes to StorageService instead of localStorage', async () => {
    (globalThis.fetch as Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        access_token: 'new-token',
        expires_in: 3600,
        refresh_token: 'new-refresh',
      }),
    });

    const auth = createAuth({ storage: mockStorage });
    auth.initAuth();

    const p = auth.requestAccessToken('consent');
    mockCodeClient.callback({ code: 'code-1', scope: 'drive.appdata' });
    await p;

    // Tokens written to StorageService
    expect(mockStorage.set).toHaveBeenCalledWith(ACCESS_KEY, 'new-token');
    expect(mockStorage.set).toHaveBeenCalledWith(REFRESH_KEY, 'new-refresh');
    expect(mockStorage.store.get(ACCESS_KEY)).toBe('new-token');
    expect(mockStorage.store.get(REFRESH_KEY)).toBe('new-refresh');

    // localStorage should NOT have tokens (StorageService branch skips it)
    expect(localStorage.getItem(ACCESS_KEY)).toBeNull();
  });

  it('silentRefresh reads from StorageService when provided', async () => {
    const futureExpiry = Date.now() + 3600_000;
    mockStorage.store.set(ACCESS_KEY, 'cached-token');
    mockStorage.store.set(EXPIRY_KEY, String(futureExpiry));
    mockStorage.store.set(REFRESH_KEY, 'refresh-token');

    const auth = createAuth({ storage: mockStorage });
    const result = await auth.silentRefresh();

    expect(result).toBe('cached-token');
    expect(auth.isAuthenticated()).toBe(true);
    expect(mockStorage.get).toHaveBeenCalledWith(ACCESS_KEY);
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('silentRefresh falls back to cloud function when StorageService token is expired', async () => {
    mockStorage.store.set(ACCESS_KEY, 'old-token');
    mockStorage.store.set(EXPIRY_KEY, String(Date.now() - 1000));
    mockStorage.store.set(REFRESH_KEY, 'my-refresh');

    (globalThis.fetch as Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ access_token: 'fresh-token', expires_in: 3600 }),
    });

    const auth = createAuth({ storage: mockStorage });
    const result = await auth.silentRefresh();

    expect(result).toBe('fresh-token');
    expect(globalThis.fetch).toHaveBeenCalledWith(
      TOKEN_URL,
      expect.objectContaining({
        body: JSON.stringify({ action: 'refresh', refresh_token: 'my-refresh' }),
      }),
    );
  });

  it('invalidateToken deletes access token/expiry from StorageService, keeps refresh token', async () => {
    const futureExpiry = Date.now() + 3600_000;
    mockStorage.store.set(ACCESS_KEY, 'active-token');
    mockStorage.store.set(EXPIRY_KEY, String(futureExpiry));
    mockStorage.store.set(REFRESH_KEY, 'refresh-token');

    const auth = createAuth({ storage: mockStorage });
    await auth.silentRefresh();
    expect(auth.isAuthenticated()).toBe(true);

    auth.invalidateToken();

    expect(auth.isAuthenticated()).toBe(false);
    expect(mockStorage.delete).toHaveBeenCalledWith(ACCESS_KEY);
    expect(mockStorage.delete).toHaveBeenCalledWith(EXPIRY_KEY);
    expect(mockStorage.delete).not.toHaveBeenCalledWith(REFRESH_KEY);
    expect(mockStorage.store.has(REFRESH_KEY)).toBe(true);
  });

  it('disconnect clears all tokens from StorageService (code flow)', async () => {
    const futureExpiry = Date.now() + 3600_000;
    mockStorage.store.set(ACCESS_KEY, 'active-token');
    mockStorage.store.set(EXPIRY_KEY, String(futureExpiry));
    mockStorage.store.set(REFRESH_KEY, 'refresh-token');

    const auth = createAuth({ storage: mockStorage });
    await auth.silentRefresh();

    auth.disconnect();

    expect(auth.isAuthenticated()).toBe(false);
    expect(mockStorage.delete).toHaveBeenCalledWith(REFRESH_KEY);
    expect(mockStorage.delete).toHaveBeenCalledWith(ACCESS_KEY);
    expect(mockStorage.delete).toHaveBeenCalledWith(EXPIRY_KEY);
  });

  it('silentRefresh clears StorageService tokens on 401 from cloud function', async () => {
    mockStorage.store.set(ACCESS_KEY, 'old');
    mockStorage.store.set(EXPIRY_KEY, '0');
    mockStorage.store.set(REFRESH_KEY, 'revoked-token');

    (globalThis.fetch as Mock).mockResolvedValueOnce({
      ok: false,
      status: 401,
      json: async () => ({ error: 'invalid_grant' }),
    });

    const auth = createAuth({ storage: mockStorage });
    const result = await auth.silentRefresh();

    expect(result).toBeNull();
    expect(mockStorage.delete).toHaveBeenCalledWith(REFRESH_KEY);
    expect(mockStorage.delete).toHaveBeenCalledWith(ACCESS_KEY);
    expect(mockStorage.delete).toHaveBeenCalledWith(EXPIRY_KEY);
  });
});
