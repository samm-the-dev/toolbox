/**
 * OPFS-backed StorageService adapter.
 *
 * Uses the Origin Private File System async API (getFile / createWritable)
 * for durable persistence. Writes are mirrored to localStorage so other
 * code paths (e.g., legacy sync readers) can access data synchronously.
 *
 * A BroadcastChannel named 'storage-service' is used for cross-tab
 * invalidation: when one tab writes/deletes/clears, other tabs receive
 * the event and can react accordingly.
 */

import type { StorageService, StorageServiceOptions } from './types';

interface InvalidationMessage {
  type: 'set' | 'delete' | 'clear';
  key?: string;
}

export function createOpfsAdapter(opts: StorageServiceOptions = {}): StorageService {
  const prefix = opts.prefix ?? '';
  const logPrefix = opts.logPrefix ?? '';
  const channel = new BroadcastChannel('storage-service');

  function prefixedKey(key: string): string {
    return prefix ? `${prefix}:${key}` : key;
  }

  function opfsFileName(key: string): string {
    return prefixedKey(key) + '.json';
  }

  function broadcast(msg: InvalidationMessage): void {
    try {
      channel.postMessage(msg);
    } catch {
      // Swallow -- broadcast is best-effort.
    }
  }

  async function getDirectory(): Promise<FileSystemDirectoryHandle> {
    return navigator.storage.getDirectory();
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

    broadcast({ type: 'set', key: prefixedKey(key) });
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

    broadcast({ type: 'delete', key: prefixedKey(key) });
  }

  async function clear(): Promise<void> {
    try {
      const dir = await getDirectory();
      const entries: string[] = [];
      for await (const [name] of dir.entries()) {
        const matchesPrefix = !prefix || name.startsWith(prefix + ':');
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

    broadcast({ type: 'clear' });
  }

  async function keys(): Promise<string[]> {
    const result: string[] = [];
    try {
      const dir = await getDirectory();
      for await (const [name] of dir.entries()) {
        const matchesPrefix = !prefix || name.startsWith(prefix + ':');
        if (matchesPrefix && name.endsWith('.json')) {
          const raw = name.slice(0, -5); // strip .json
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
