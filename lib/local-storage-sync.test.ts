import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createLocalStorage } from './local-storage-sync';
import type { StorageService } from './storage-service/types';

interface TestData {
  version: number;
  value: string;
}

const DEFAULTS: TestData = { version: 1, value: 'default' };

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

describe('createLocalStorage with StorageService', () => {
  let mockStorage: ReturnType<typeof createMockStorage>;

  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    mockStorage = createMockStorage();
  });

  it('saveToLocal writes to localStorage synchronously and fires async StorageService write', () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    const data: TestData = { version: 1, value: 'hello' };
    sync.saveToLocal(data);

    // localStorage updated synchronously — no await needed
    const raw = localStorage.getItem('test-data');
    expect(raw).toBe(JSON.stringify(data));

    // StorageService also called
    expect(mockStorage.set).toHaveBeenCalledWith('test-data', data);
  });

  it('loadFromLocal reads synchronously from localStorage after saveToLocal', () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    const data: TestData = { version: 1, value: 'saved' };
    sync.saveToLocal(data);

    // Immediately readable — no async gap
    const loaded = sync.loadFromLocal();
    expect(loaded).toEqual(data);
  });

  it('clearLocal removes from localStorage synchronously and fires async StorageService delete', () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    localStorage.setItem('test-data', JSON.stringify({ version: 1, value: 'x' }));
    sync.clearLocal();

    expect(localStorage.getItem('test-data')).toBeNull();
    expect(mockStorage.delete).toHaveBeenCalledWith('test-data');
  });

  it('loadFromLocal returns default when version mismatches', () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 2,
      sanitize: (d) => d,
      createDefault: () => ({ version: 2, value: 'fresh' }),
      storage: mockStorage,
    });

    localStorage.setItem('test-data', JSON.stringify({ version: 1, value: 'old' }));
    const loaded = sync.loadFromLocal();
    expect(loaded).toEqual({ version: 2, value: 'fresh' });
  });
});

describe('recoverFromStorage', () => {
  let mockStorage: ReturnType<typeof createMockStorage>;

  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    mockStorage = createMockStorage();
  });

  it('recovers data from StorageService', async () => {
    const stored: TestData = { version: 1, value: 'recovered' };
    mockStorage.store.set('test-data', stored);

    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    const result = await sync.recoverFromStorage();
    expect(result).toEqual(stored);
    expect(mockStorage.get).toHaveBeenCalledWith('test-data');
  });

  it('returns null when no data exists in StorageService', async () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    const result = await sync.recoverFromStorage();
    expect(result).toBeNull();
  });

  it('returns null when version mismatches', async () => {
    mockStorage.store.set('test-data', { version: 1, value: 'old' });

    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 2,
      sanitize: (d) => d,
      createDefault: () => ({ version: 2, value: 'fresh' }),
      storage: mockStorage,
    });

    const result = await sync.recoverFromStorage();
    expect(result).toBeNull();
  });

  it('returns null when no storage is configured', async () => {
    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => d,
      createDefault: () => DEFAULTS,
    });

    const result = await sync.recoverFromStorage();
    expect(result).toBeNull();
  });

  it('applies sanitize to recovered data', async () => {
    mockStorage.store.set('test-data', { version: 1, value: 'raw' });

    const sync = createLocalStorage<TestData>({
      storageKey: 'test-data',
      logPrefix: '[Test]',
      version: 1,
      sanitize: (d) => ({ ...d, value: `${d.value}-sanitized` }),
      createDefault: () => DEFAULTS,
      storage: mockStorage,
    });

    const result = await sync.recoverFromStorage();
    expect(result).toEqual({ version: 1, value: 'raw-sanitized' });
  });
});
