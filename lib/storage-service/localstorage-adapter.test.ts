import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createLocalStorageAdapter } from './localstorage-adapter';

describe('createLocalStorageAdapter', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('reports adapter type as localstorage', () => {
    const adapter = createLocalStorageAdapter();
    expect(adapter.adapter).toBe('localstorage');
  });

  describe('set / get roundtrip', () => {
    it('stores and retrieves JSON values', async () => {
      const adapter = createLocalStorageAdapter({ prefix: 'test' });
      await adapter.set('key1', { count: 42 });

      const result = await adapter.get<{ count: number }>('key1');
      expect(result).toEqual({ count: 42 });
    });

    it('prefixes localStorage keys', async () => {
      const adapter = createLocalStorageAdapter({ prefix: 'myapp' });
      await adapter.set('k', 'v');

      expect(localStorage.getItem('myapp:k')).toBe('"v"');
      expect(localStorage.getItem('k')).toBeNull();
    });

    it('works without a prefix', async () => {
      const adapter = createLocalStorageAdapter();
      await adapter.set('bare', 123);

      expect(localStorage.getItem('bare')).toBe('123');
      expect(await adapter.get('bare')).toBe(123);
    });
  });

  describe('get', () => {
    it('returns null for missing keys', async () => {
      const adapter = createLocalStorageAdapter();
      expect(await adapter.get('nope')).toBeNull();
    });
  });

  describe('delete', () => {
    it('removes the key from localStorage', async () => {
      const adapter = createLocalStorageAdapter({ prefix: 'p' });
      await adapter.set('k', 'v');
      expect(localStorage.getItem('p:k')).toBe('"v"');

      await adapter.delete('k');
      expect(localStorage.getItem('p:k')).toBeNull();
      expect(await adapter.get('k')).toBeNull();
    });
  });

  describe('clear', () => {
    it('only removes keys matching the prefix', async () => {
      const adapter = createLocalStorageAdapter({ prefix: 'app' });
      await adapter.set('a', 1);
      await adapter.set('b', 2);
      localStorage.setItem('other:c', '3');

      await adapter.clear();

      expect(await adapter.get('a')).toBeNull();
      expect(await adapter.get('b')).toBeNull();
      expect(localStorage.getItem('other:c')).toBe('3');
    });
  });

  describe('keys', () => {
    it('returns unprefixed keys', async () => {
      const adapter = createLocalStorageAdapter({ prefix: 'ns' });
      await adapter.set('alpha', 1);
      await adapter.set('beta', 2);
      localStorage.setItem('other:gamma', '3');

      const result = await adapter.keys();
      expect(result.sort()).toEqual(['alpha', 'beta']);
    });
  });
});
