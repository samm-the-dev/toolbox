/**
 * Web Share API utility with clipboard fallback.
 *
 * Pure browser-API module — no toast/notification dependency.
 * Consuming apps wrap the result to show their own feedback (sonner, custom toasts, etc.).
 *
 * Usage:
 *   import { shareUrl, shareText } from '../../.toolbox/lib/share';
 *   const result = await shareUrl('https://example.com', 'My Page');
 *   if (result === 'copied') toast.success('Link copied!');
 */

export type ShareResult = 'shared' | 'copied' | 'failed' | 'cancelled';

/**
 * Share a URL via the Web Share API, falling back to clipboard copy.
 *
 * Returns:
 * - 'shared'    — user completed the native share sheet
 * - 'copied'    — no native share; URL was copied to clipboard
 * - 'failed'    — clipboard write also failed
 * - 'cancelled' — user dismissed the share sheet (not an error)
 */
export async function shareUrl(url: string, title?: string): Promise<ShareResult> {
  if (navigator.share) {
    try {
      await navigator.share({ title, url });
      return 'shared';
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') return 'cancelled';
    }
  }

  try {
    await navigator.clipboard.writeText(url);
    return 'copied';
  } catch {
    return 'failed';
  }
}

/**
 * Share a text payload via the Web Share API, falling back to clipboard copy.
 *
 * Useful for sharing formatted text (e.g. session plan lineup) without a URL.
 */
export async function shareText(text: string, title?: string): Promise<ShareResult> {
  if (navigator.share) {
    try {
      await navigator.share({ title, text });
      return 'shared';
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') return 'cancelled';
    }
  }

  try {
    await navigator.clipboard.writeText(text);
    return 'copied';
  } catch {
    return 'failed';
  }
}
