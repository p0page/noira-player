import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  createBridgeRequest,
  isWebViewBridgeAvailable,
  postNativeBack,
  postHostReady,
  requestBridge,
  subscribeHostInput,
  subscribeHostLifecycle,
} from './bridge';

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

  it('rejects requests outside WebView2 instead of fabricating app data', async () => {
    await expect(requestBridge('auth.bootstrap')).rejects.toThrow(
      'Noira catalog requires the WebView2 host.',
    );
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

  it('uses one listener per WebView host and routes concurrent responses by request id', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    const addEventListener = vi.fn(
      (_type: 'message', handler: (event: { data: unknown }) => void) => {
        messageHandler = handler;
      },
    );
    const postMessage = vi.fn();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener,
          removeEventListener: vi.fn(),
          postMessage,
        },
      },
    };

    const firstPromise = requestBridge<{ value: string }>('auth.bootstrap');
    const secondPromise = requestBridge<{ value: string }>('auth.bootstrap');
    const firstRequest = postMessage.mock.calls[0][0] as { id: string };
    const secondRequest = postMessage.mock.calls[1][0] as { id: string };

    expect(addEventListener).toHaveBeenCalledOnce();
    messageHandler?.({
      data: { id: secondRequest.id, ok: true, result: { value: 'second' } },
    });
    messageHandler?.({
      data: { id: firstRequest.id, ok: true, result: { value: 'first' } },
    });

    await expect(firstPromise).resolves.toEqual({ value: 'first' });
    await expect(secondPromise).resolves.toEqual({ value: 'second' });
  });

  it('routes typed host lifecycle events outside the request pending map', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    const postMessage = vi.fn();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage,
        },
      },
    };
    const lifecycleListener = vi.fn();
    const unsubscribe = subscribeHostLifecycle(lifecycleListener);
    const request = requestBridge<{ value: string }>('auth.bootstrap');
    const requestId = (postMessage.mock.calls[0]?.[0] as { id: string }).id;

    messageHandler?.({
      data: { type: 'host.lifecycle', event: 'playback-returned' },
    });
    expect(lifecycleListener).toHaveBeenCalledOnce();
    expect(lifecycleListener).toHaveBeenCalledWith({
      type: 'host.lifecycle',
      event: 'playback-returned',
    });

    messageHandler?.({
      data: { id: requestId, ok: true, result: { value: 'response' } },
    });
    await expect(request).resolves.toEqual({ value: 'response' });

    unsubscribe();
    messageHandler?.({
      data: { type: 'host.lifecycle', event: 'playback-returned' },
    });
    expect(lifecycleListener).toHaveBeenCalledOnce();
  });

  it('routes validated host.input v1 events without resolving pending bridge requests', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    const postMessage = vi.fn();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage,
        },
      },
    };
    const inputListener = vi.fn();
    const unsubscribe = subscribeHostInput(inputListener);
    const request = requestBridge<{ value: string }>('auth.bootstrap');
    const requestId = (postMessage.mock.calls[0]?.[0] as { id: string }).id;

    messageHandler?.({
      data: {
        type: 'host.input',
        version: 1,
        sequence: 7,
        command: 'moveLeft',
        phase: 'repeated',
        source: 'gamepad',
        timestamp: 1250,
      },
    });
    expect(inputListener).toHaveBeenCalledOnce();
    expect(inputListener).toHaveBeenCalledWith({
      type: 'host.input',
      version: 1,
      sequence: 7,
      command: 'moveLeft',
      phase: 'repeated',
      source: 'gamepad',
      timestamp: 1250,
    });

    messageHandler?.({
      data: { id: requestId, ok: true, result: { value: 'response' } },
    });
    await expect(request).resolves.toEqual({ value: 'response' });

    unsubscribe();
    messageHandler?.({
      data: {
        type: 'host.input',
        version: 1,
        sequence: 8,
        command: 'accept',
        phase: 'released',
        source: 'gamepad',
        timestamp: 1300,
      },
    });
    expect(inputListener).toHaveBeenCalledOnce();
  });

  it('drops malformed, duplicate, and backward host.input sequences per WebView state', () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    };
    const listener = vi.fn();
    subscribeHostInput(listener);
    const valid = {
      type: 'host.input',
      version: 1,
      sequence: 10,
      command: 'menu',
      phase: 'pressed',
      source: 'remote',
      timestamp: 2000,
    } as const;

    messageHandler?.({ data: valid });
    messageHandler?.({ data: valid });
    messageHandler?.({ data: { ...valid, sequence: 9 } });
    messageHandler?.({ data: { ...valid, sequence: 11, version: 2 } });
    messageHandler?.({ data: { ...valid, sequence: 11, command: 'escape' } });
    messageHandler?.({ data: { ...valid, sequence: 11, phase: 'down' } });
    messageHandler?.({ data: { ...valid, sequence: 11, source: 'mouse' } });
    messageHandler?.({ data: { ...valid, sequence: 11, timestamp: -1 } });
    messageHandler?.({ data: Object.assign([], { ...valid, sequence: 11 }) });
    messageHandler?.({ data: { ...valid, sequence: 11 } });

    expect(listener).toHaveBeenCalledTimes(2);
    expect(listener).toHaveBeenLastCalledWith({ ...valid, sequence: 11 });
  });

  it('keeps input sequence tracking isolated between WebView host objects', () => {
    const handlers: Array<(event: { data: unknown }) => void> = [];
    const listener = vi.fn();
    function installHost() {
      (globalThis as { window?: unknown }).window = {
        chrome: {
          webview: {
            addEventListener: vi.fn(
              (_type: 'message', handler: (event: { data: unknown }) => void) => {
                handlers.push(handler);
              },
            ),
            removeEventListener: vi.fn(),
            postMessage: vi.fn(),
          },
        },
      };
      subscribeHostInput(listener);
    }

    installHost();
    installHost();
    const event = {
      type: 'host.input',
      version: 1,
      sequence: 1,
      command: 'accept',
      phase: 'pressed',
      source: 'keyboard',
      timestamp: 1,
    } as const;
    handlers[0]?.({ data: event });
    handlers[1]?.({ data: event });

    expect(listener).toHaveBeenCalledTimes(2);
  });

  it('posts host.ready with the supported semantic input protocol version', () => {
    const postMessage = vi.fn();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          postMessage,
        },
      },
    };

    expect(postHostReady()).toBe(true);
    expect(postHostReady()).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'host.ready',
      inputVersion: 1,
    });
    expect(postMessage).toHaveBeenCalledOnce();
  });

  it('reports host.ready as unavailable outside WebView2', () => {
    expect(postHostReady()).toBe(false);
  });

  it('posts a semantic native Back request without entering the response map', () => {
    const postMessage = vi.fn();
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          postMessage,
        },
      },
    };

    expect(postNativeBack()).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'host.nativeBack',
    });
  });

  it('accepts activated-home as a typed lifecycle event', () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    (globalThis as { window?: unknown }).window = {
      chrome: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    };
    const lifecycleListener = vi.fn();
    subscribeHostLifecycle(lifecycleListener);

    messageHandler?.({
      data: { type: 'host.lifecycle', event: 'activated-home' },
    });

    expect(lifecycleListener).toHaveBeenCalledWith({
      type: 'host.lifecycle',
      event: 'activated-home',
    });
  });

  it('makes lifecycle subscription a no-op outside WebView2', () => {
    const listener = vi.fn();
    const unsubscribe = subscribeHostLifecycle(listener);

    expect(() => unsubscribe()).not.toThrow();
    expect(listener).not.toHaveBeenCalled();
  });

  it('never fabricates a successful native playback launch', async () => {
    await expect(
      requestBridge('playback.nativePlayItem', { itemId: 'anonymous-item' }),
    ).rejects.toThrow('Noira catalog requires the WebView2 host.');
  });
});
