/**
 * OPFS-backed StorageService adapter.
 *
 * Uses the Origin Private File System async API (getFile / createWritable)
 * for durable persistence. All files are scoped to a `storage-service/`
 * subdirectory to avoid collisions with other OPFS consumers. Writes are
 * mirrored to localStorage so other code paths (e.g., legacy sync readers)
 * can access data synchronously.
 */

import type { StorageService, StorageServiceOptions } from './types';

/** Fixed OPFS subdirectory to isolate StorageService files. */
const OPFS_DIR = 'storage-service';

export function createOpfsAdapter(opts: StorageServiceOptions = {}): StorageService {
  const prefix = opts.prefix ?? '';
  const logPrefix = opts.logPrefix ?? '';

  function prefixedKey(key: string): string {
    return prefix ? `${prefix}:${key}` : key;
  }

  function opfsFileName(key: string): string {
    return encodeURIComponent(prefixedKey(key)) + '.json';
  }

  async function getDirectory(): Promise<FileSystemDirectoryHandle> {
    const root = await navigator.storage.getDirectory();
    return root.getDirectoryHandle(OPFS_DIR, { create: true });
  }

  async function get<T>(key: string): Promise<T | null> {
    try {
      const dir = await getDirectory();
      const fileHandle = await dir.getFileHandle(opfsFileName(key));
      const file = await fileHandle.getFile();
      const text = await file.text();
      return JSON.parse(text) as T;
    } catch (e) {
      if (e instanceof DOMException && e.name === 'NotFoundError') {
        return null;
      }
      console.error(`${logPrefix} OPFS get failed for "${key}":`, e);
      return null;
    }
  }

  async function set<T>(key: string, value: T): Promise<void> {
    const json = JSON.stringify(value);

    try {
      const dir = await getDirectory();
      const fileHandle = await dir.getFileHandle(opfsFileName(key), { create: true });
      const writable = await fileHandle.createWritable();
      await writable.write(json);
      await writable.close();
    } catch (e) {
      console.error(`${logPrefix} OPFS set failed for "${key}":`, e);
      throw e;
    }

    // localStorage write-through mirror
    try {
      localStorage.setItem(prefixedKey(key), json);
    } catch {
      // Swallow -- mirror is best-effort.
    }
  }

  async function del(key: string): Promise<void> {
    try {
      const dir = await getDirectory();
      await dir.removeEntry(opfsFileName(key));
    } catch (e) {
      if (!(e instanceof DOMException && e.name === 'NotFoundError')) {
        console.error(`${logPrefix} OPFS delete failed for "${key}":`, e);
      }
    }

    try {
      localStorage.removeItem(prefixedKey(key));
    } catch {
      // Swallow.
    }
  }

  async function clear(): Promise<void> {
    try {
      const dir = await getDirectory();
      const entries: string[] = [];
      for await (const [name] of dir.entries()) {
        const matchesPrefix = !prefix || name.startsWith(encodeURIComponent(prefix + ':'));
        if (matchesPrefix && name.endsWith('.json')) {
          entries.push(name);
        }
      }
      for (const name of entries) {
        await dir.removeEntry(name);
      }
    } catch (e) {
      console.error(`${logPrefix} OPFS clear failed:`, e);
    }

    // Clear mirrored localStorage keys
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
    } catch {
      // Swallow.
    }
  }

  async function keys(): Promise<string[]> {
    const result: string[] = [];
    try {
      const dir = await getDirectory();
      for await (const [name] of dir.entries()) {
        const matchesPrefix = !prefix || name.startsWith(encodeURIComponent(prefix + ':'));
        if (matchesPrefix && name.endsWith('.json')) {
          const raw = decodeURIComponent(name.slice(0, -5)); // strip .json, decode
          result.push(prefix ? raw.slice(prefix.length + 1) : raw);
        }
      }
    } catch (e) {
      console.error(`${logPrefix} OPFS keys failed:`, e);
    }
    return result;
  }

  return {
    adapter: 'opfs',
    get,
    set,
    delete: del,
    clear,
    keys,
  };
}
