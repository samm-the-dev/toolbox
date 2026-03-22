/**
 * Generic async key-value StorageService.
 *
 * Resolves OPFS as the primary adapter when available, with localStorage
 * as fallback. The factory is async because OPFS availability detection
 * (`navigator.storage.getDirectory()`) is itself async.
 *
 * Usage:
 *   const storage = await createStorageService({ prefix: 'myapp' });
 *   await storage.set('key', { some: 'value' });
 *   const data = await storage.get<MyType>('key');
 */

export type { StorageService, StorageAdapterType, StorageServiceOptions } from './types';

import type { StorageService, StorageServiceOptions } from './types';
import { createOpfsAdapter } from './opfs-adapter';
import { createLocalStorageAdapter } from './localstorage-adapter';

async function isOpfsAvailable(): Promise<boolean> {
  try {
    const dir = await navigator.storage.getDirectory();
    // Verify write access with a probe file
    const probe = '__storage_service_probe__';
    const handle = await dir.getFileHandle(probe, { create: true });
    const writable = await handle.createWritable();
    await writable.write('1');
    await writable.close();
    await dir.removeEntry(probe);
    return true;
  } catch {
    return false;
  }
}

export async function createStorageService(
  opts?: StorageServiceOptions,
): Promise<StorageService> {
  if (await isOpfsAvailable()) {
    return createOpfsAdapter(opts);
  }
  return createLocalStorageAdapter(opts);
}
