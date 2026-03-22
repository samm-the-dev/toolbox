/**
 * Generic localStorage persistence factory.
 *
 * Wraps save/load/clear with version checking and sanitization.
 * Consumer provides the storage key, version, sanitizer, and default factory.
 *
 * Optionally accepts a StorageService instance. When provided, save/load/clear
 * delegate to StorageService instead of raw localStorage. The sync loadFromLocal
 * still reads localStorage (for instant mount), but saveToLocal writes through
 * StorageService which handles OPFS + localStorage mirroring.
 */

import type { StorageService } from './storage-service/types';

export interface LocalStorageConfig<T> {
  storageKey: string;
  logPrefix: string;
  version: number;
  sanitize: (data: T) => T;
  createDefault: () => T;
  /**
   * Optional async storage backend. When provided, saveToLocal/clearLocal
   * delegate writes/deletes to StorageService instead of raw localStorage.
   *
   * loadFromLocal always reads from localStorage synchronously (for instant
   * mount). This works because the OPFS adapter mirrors every write to
   * localStorage under the same key. If a custom adapter is used that does
   * NOT mirror to localStorage, loadFromLocal will return stale/default data.
   */
  storage?: StorageService;
}

export interface LocalStorageSync<T> {
  saveToLocal(data: T): void;
  loadFromLocal(): T;
  clearLocal(): void;
}

export function createLocalStorage<T extends { version: number }>(
  config: LocalStorageConfig<T>,
): LocalStorageSync<T> {
  const { storageKey, logPrefix, version, sanitize, createDefault, storage } = config;

  function saveToLocal(data: T): void {
    if (storage) {
      storage.set(storageKey, data).catch((e) => {
        console.error(`${logPrefix} Failed to save via StorageService:`, e);
      });
      return;
    }
    try {
      localStorage.setItem(storageKey, JSON.stringify(data));
    } catch (e) {
      console.error(`${logPrefix} Failed to save to localStorage:`, e);
    }
  }

  function loadFromLocal(): T {
    // Always read from localStorage synchronously for instant mount.
    // StorageService (OPFS adapter) mirrors writes to localStorage,
    // so this stays in sync with the async backend.
    try {
      const raw = localStorage.getItem(storageKey);
      if (raw) {
        const parsed = JSON.parse(raw) as T;
        if (parsed.version === version) {
          return sanitize(parsed);
        }
      }
    } catch (e) {
      console.error(`${logPrefix} Failed to load from localStorage:`, e);
    }
    return createDefault();
  }

  function clearLocal(): void {
    if (storage) {
      storage.delete(storageKey).catch((e) => {
        console.error(`${logPrefix} Failed to clear via StorageService:`, e);
      });
      return;
    }
    try {
      localStorage.removeItem(storageKey);
    } catch (e) {
      console.error(`${logPrefix} Failed to clear localStorage:`, e);
    }
  }

  return { saveToLocal, loadFromLocal, clearLocal };
}
