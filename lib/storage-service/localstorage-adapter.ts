/**
 * localStorage-backed StorageService adapter.
 *
 * Pure localStorage fallback for environments where OPFS is unavailable.
 * Values are JSON-serialized and stored under prefixed keys.
 */

import type { StorageService, StorageServiceOptions } from './types';

export function createLocalStorageAdapter(opts: StorageServiceOptions = {}): StorageService {
  const prefix = opts.prefix ?? '';
  const logPrefix = opts.logPrefix ?? '';

  function prefixedKey(key: string): string {
    return prefix ? `${prefix}:${key}` : key;
  }

  async function get<T>(key: string): Promise<T | null> {
    try {
      const raw = localStorage.getItem(prefixedKey(key));
      if (raw === null) return null;
      return JSON.parse(raw) as T;
    } catch (e) {
      console.error(`${logPrefix} localStorage get failed for "${key}":`, e);
      return null;
    }
  }

  async function set<T>(key: string, value: T): Promise<void> {
    try {
      localStorage.setItem(prefixedKey(key), JSON.stringify(value));
    } catch (e) {
      console.error(`${logPrefix} localStorage set failed for "${key}":`, e);
      throw e;
    }
  }

  async function del(key: string): Promise<void> {
    try {
      localStorage.removeItem(prefixedKey(key));
    } catch (e) {
      console.error(`${logPrefix} localStorage delete failed for "${key}":`, e);
    }
  }

  async function clear(): Promise<void> {
    try {
      const keysToRemove: string[] = [];
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k && (!prefix || k.startsWith(prefix + ':'))) {
          keysToRemove.push(k);
        }
      }
      for (const k of keysToRemove) {
        localStorage.removeItem(k);
      }
    } catch (e) {
      console.error(`${logPrefix} localStorage clear failed:`, e);
    }
  }

  async function keys(): Promise<string[]> {
    const result: string[] = [];
    try {
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k && (!prefix || k.startsWith(prefix + ':'))) {
          result.push(prefix ? k.slice(prefix.length + 1) : k);
        }
      }
    } catch (e) {
      console.error(`${logPrefix} localStorage keys failed:`, e);
    }
    return result;
  }

  return {
    adapter: 'localstorage',
    get,
    set,
    delete: del,
    clear,
    keys,
  };
}
