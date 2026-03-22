import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createOpfsAdapter } from './opfs-adapter';

// --- OPFS mocks ---

function createMockWritable() {
  return {
    write: vi.fn(),
    close: vi.fn(),
    abort: vi.fn(),
  } as unknown as FileSystemWritableFileStream;
}

function createMockFileHandle(content = '') {
  const writable = createMockWritable();
  return {
    handle: {
      getFile: vi.fn().mockResolvedValue({ text: () => Promise.resolve(content) }),
      createWritable: vi.fn().mockResolvedValue(writable),
    } as unknown as FileSystemFileHandle,
    writable,
  };
}

function createMockDirectory(files: Map<string, string> = new Map()) {
  const dir = {
    getFileHandle: vi.fn(async (name: string, opts?: { create?: boolean }) => {
      if (files.has(name) || opts?.create) {
        const { handle } = createMockFileHandle(files.get(name) ?? '');
        return handle;
      }
      throw new DOMException('Not found', 'NotFoundError');
    }),
    removeEntry: vi.fn(async (name: string) => {
      if (!files.has(name)) {
        throw new DOMException('Not found', 'NotFoundError');
      }
      files.delete(name);
    }),
    entries: vi.fn(() => {
      const entries = [...files.entries()].map(
        ([name]) => [name, {}] as [string, FileSystemHandle],
      );
      let i = 0;
      return {
        [Symbol.asyncIterator]() {
          return this;
        },
        next: async () =>
          i < entries.length
            ? { done: false as const, value: entries[i++] }
            : { done: true as const, value: undefined },
      };
    }),
  } as unknown as FileSystemDirectoryHandle;
  return dir;
}

function stubOpfs(dir: FileSystemDirectoryHandle) {
  const storageServiceDir = dir;
  const rootDir = {
    getDirectoryHandle: vi.fn().mockResolvedValue(storageServiceDir),
  } as unknown as FileSystemDirectoryHandle;

  vi.stubGlobal('navigator', {
    ...navigator,
    storage: {
      getDirectory: vi.fn().mockResolvedValue(rootDir),
    },
  });

  return { rootDir, storageServiceDir };
}

describe('createOpfsAdapter', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('reports adapter type as opfs', () => {
    const dir = createMockDirectory();
    stubOpfs(dir);
    const adapter = createOpfsAdapter();
    expect(adapter.adapter).toBe('opfs');
  });

  describe('set', () => {
    it('writes to OPFS and mirrors to localStorage', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);
      const adapter = createOpfsAdapter({ prefix: 'app' });

      await adapter.set('key1', { hello: 'world' });

      expect(dir.getFileHandle).toHaveBeenCalledWith(
        encodeURIComponent('app:key1') + '.json',
        { create: true },
      );
      expect(localStorage.getItem('app:key1')).toBe('{"hello":"world"}');
    });

    it('mirrors to localStorage before OPFS write so sync reads are never stale', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);

      // Make OPFS write hang indefinitely
      const { handle } = createMockFileHandle();
      (handle.createWritable as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
      (dir.getFileHandle as ReturnType<typeof vi.fn>).mockResolvedValue(handle);

      const adapter = createOpfsAdapter({ prefix: 'app' });
      // Don't await — the OPFS write will never resolve
      const promise = adapter.set('key1', 'value');

      // localStorage should already have the value
      await vi.waitFor(() => {
        expect(localStorage.getItem('app:key1')).toBe('"value"');
      });

      // Clean up: we don't care about the hanging promise
      void promise.catch(() => {});
    });

    it('mirrors to localStorage even when OPFS write fails', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);

      (dir.getFileHandle as ReturnType<typeof vi.fn>).mockRejectedValue(
        new Error('quota exceeded'),
      );

      const adapter = createOpfsAdapter({ prefix: 'app' });
      await expect(adapter.set('key1', 'data')).rejects.toThrow('quota exceeded');

      // localStorage should still have the value
      expect(localStorage.getItem('app:key1')).toBe('"data"');
    });
  });

  describe('get', () => {
    it('returns null for missing keys', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);
      const adapter = createOpfsAdapter();

      const result = await adapter.get('nonexistent');
      expect(result).toBeNull();
    });
  });

  describe('delete', () => {
    it('removes from OPFS and localStorage', async () => {
      const files = new Map([
        [encodeURIComponent('app:key1') + '.json', '"value"'],
      ]);
      const dir = createMockDirectory(files);
      stubOpfs(dir);
      localStorage.setItem('app:key1', '"value"');

      const adapter = createOpfsAdapter({ prefix: 'app' });
      await adapter.delete('key1');

      expect(dir.removeEntry).toHaveBeenCalledWith(
        encodeURIComponent('app:key1') + '.json',
      );
      expect(localStorage.getItem('app:key1')).toBeNull();
    });

    it('does not throw for missing keys', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);
      const adapter = createOpfsAdapter();

      await expect(adapter.delete('missing')).resolves.toBeUndefined();
    });
  });

  describe('clear', () => {
    it('only removes files matching the prefix', async () => {
      const files = new Map([
        [encodeURIComponent('app:key1') + '.json', '"v1"'],
        [encodeURIComponent('app:key2') + '.json', '"v2"'],
        [encodeURIComponent('other:key3') + '.json', '"v3"'],
      ]);
      const dir = createMockDirectory(files);
      stubOpfs(dir);

      localStorage.setItem('app:key1', '"v1"');
      localStorage.setItem('app:key2', '"v2"');
      localStorage.setItem('other:key3', '"v3"');

      const adapter = createOpfsAdapter({ prefix: 'app' });
      await adapter.clear();

      expect(localStorage.getItem('app:key1')).toBeNull();
      expect(localStorage.getItem('app:key2')).toBeNull();
      // Other prefix should be untouched
      expect(localStorage.getItem('other:key3')).toBe('"v3"');
    });
  });

  describe('keys', () => {
    it('returns unprefixed, decoded keys', async () => {
      const files = new Map([
        [encodeURIComponent('app:key1') + '.json', ''],
        [encodeURIComponent('app:key/with/slashes') + '.json', ''],
        [encodeURIComponent('other:nope') + '.json', ''],
      ]);
      const dir = createMockDirectory(files);
      stubOpfs(dir);

      const adapter = createOpfsAdapter({ prefix: 'app' });
      const result = await adapter.keys();

      expect(result).toEqual(['key1', 'key/with/slashes']);
    });
  });

  describe('key encoding', () => {
    it('encodes special characters in filenames', async () => {
      const dir = createMockDirectory();
      stubOpfs(dir);
      const adapter = createOpfsAdapter();

      await adapter.set('path/to:key', 'value');

      expect(dir.getFileHandle).toHaveBeenCalledWith(
        encodeURIComponent('path/to:key') + '.json',
        { create: true },
      );
    });
  });
});
