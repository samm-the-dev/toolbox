export type StorageAdapterType = 'opfs' | 'localstorage';

export interface StorageServiceOptions {
  /** Prefix for all keys (e.g., 'myapp'). Defaults to ''. */
  prefix?: string;
  /** Console log prefix (e.g., '[MyApp]'). Defaults to ''. */
  logPrefix?: string;
}

export interface StorageService {
  /** Which adapter resolved at creation time. */
  readonly adapter: StorageAdapterType;

  get<T>(key: string): Promise<T | null>;
  set<T>(key: string, value: T): Promise<void>;
  delete(key: string): Promise<void>;
  clear(): Promise<void>;
  keys(): Promise<string[]>;
}
