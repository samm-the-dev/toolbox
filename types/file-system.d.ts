/** Augment FileSystemDirectoryHandle with async iteration (not yet in TS DOM lib). */
interface FileSystemDirectoryHandle {
  entries(): AsyncIterableIterator<[string, FileSystemHandle]>;
}
