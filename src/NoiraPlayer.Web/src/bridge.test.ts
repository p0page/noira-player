import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBridgeRequest, isWebViewBridgeAvailable, requestBridge } from './bridge';

describe('bridge', () => {
  afterEach(() => {
    vi.useRealTimers();
    delete (globalThis as { window?: unknown }).window;
  });

  it('creates stable native request envelopes', () => {
    expect(createBridgeRequest('auth.bootstrap', {}, 'request-1')).toEqual({
      id: 'request-1',
      type: 'auth.bootstrap',
      payload: {},
    });
  });

  it('detects that normal browsers do not expose the WebView bridge', () => {
    expect(isWebViewBridgeAvailable()).toBe(false);
  });

  it('returns browser mock data outside UWP', async () => {
    const session = await requestBridge('auth.bootstrap');

    expect(session).toEqual({
      session: null,
    });
  });

  it('rejects instead of fabricating data when WebView2 has no native response', async () => {
    vi.useFakeTimers();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
      setTimeout,
      clearTimeout,
    };

    const sessionPromise = requestBridge('auth.bootstrap', {}, { timeoutMs: 1 });
    const rejection = expect(sessionPromise).rejects.toThrow(
      'Timed out waiting for native WebView2 response.',
    );
    await vi.advanceTimersByTimeAsync(1);

    await rejection;
  });

  it('rejects an incomplete WebView host instead of returning browser mocks', async () => {
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          postMessage: vi.fn(),
        },
      },
    };

    await expect(requestBridge('auth.bootstrap')).rejects.toThrow(
      'WebView2 bridge does not support response message events.',
    );
  });

  it('models playback as a native launch request in browser fallback mode', async () => {
    const result = await requestBridge('playback.nativePlayItem', {
      itemId: 'sample-movie',
      itemName: 'Sample Movie',
      startPositionTicks: 42,
      mediaSourceId: 'source-1',
      runtimeTicks: 84,
    });

    expect(result).toEqual({
      started: true,
      surface: 'native',
    });
  });
});
