import { describe, it, expect, beforeEach, vi } from 'vitest';
import { shareUrl, shareText } from './share';

function stubShare(impl: typeof navigator.share | undefined) {
  if (impl === undefined) {
    // Remove the property so 'navigator.share' is falsy
    Object.defineProperty(navigator, 'share', { value: undefined, configurable: true, writable: true });
  } else {
    Object.defineProperty(navigator, 'share', { value: impl, configurable: true, writable: true });
  }
}

function stubClipboard(impl: { writeText: (text: string) => Promise<void> } | undefined) {
  Object.defineProperty(navigator, 'clipboard', {
    value: impl,
    configurable: true,
    writable: true,
  });
}

describe('shareUrl', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    stubShare(undefined);
    stubClipboard(undefined);
  });

  it('returns "shared" when navigator.share succeeds', async () => {
    stubShare(vi.fn().mockResolvedValue(undefined));
    const result = await shareUrl('https://example.com', 'Example');
    expect(result).toBe('shared');
    expect(navigator.share).toHaveBeenCalledWith({ title: 'Example', url: 'https://example.com' });
  });

  it('returns "cancelled" when user dismisses the share sheet (AbortError)', async () => {
    const err = new DOMException('User cancelled', 'AbortError');
    stubShare(vi.fn().mockRejectedValue(err));
    const result = await shareUrl('https://example.com');
    expect(result).toBe('cancelled');
  });

  it('falls back to clipboard and returns "copied" when share throws non-Abort error', async () => {
    stubShare(vi.fn().mockRejectedValue(new Error('not supported')));
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard({ writeText });
    const result = await shareUrl('https://example.com');
    expect(result).toBe('copied');
    expect(writeText).toHaveBeenCalledWith('https://example.com');
  });

  it('returns "copied" via clipboard when navigator.share is unavailable', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard({ writeText });
    const result = await shareUrl('https://example.com');
    expect(result).toBe('copied');
  });

  it('returns "failed" when both share and clipboard fail', async () => {
    stubShare(vi.fn().mockRejectedValue(new Error('not supported')));
    stubClipboard({ writeText: vi.fn().mockRejectedValue(new Error('no clipboard')) });
    const result = await shareUrl('https://example.com');
    expect(result).toBe('failed');
  });
});

describe('shareText', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    stubShare(undefined);
    stubClipboard(undefined);
  });

  it('returns "shared" when navigator.share succeeds with text payload', async () => {
    stubShare(vi.fn().mockResolvedValue(undefined));
    const result = await shareText('Hello world', 'My Title');
    expect(result).toBe('shared');
    expect(navigator.share).toHaveBeenCalledWith({ title: 'My Title', text: 'Hello world' });
  });

  it('returns "cancelled" on AbortError', async () => {
    stubShare(vi.fn().mockRejectedValue(new DOMException('cancelled', 'AbortError')));
    expect(await shareText('text')).toBe('cancelled');
  });

  it('copies text to clipboard when share unavailable', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard({ writeText });
    const result = await shareText('lineup text');
    expect(result).toBe('copied');
    expect(writeText).toHaveBeenCalledWith('lineup text');
  });

  it('returns "failed" when both share and clipboard fail', async () => {
    stubClipboard({ writeText: vi.fn().mockRejectedValue(new Error('no clipboard')) });
    expect(await shareText('text')).toBe('failed');
  });
});
