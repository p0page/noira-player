export type BridgeCommandType =
  | 'auth.bootstrap'
  | 'auth.login'
  | 'auth.logout'
  | 'emby.get'
  | 'playback.nativePlayItem';

export interface BridgeRequest<TPayload = unknown> {
  id: string;
  type: BridgeCommandType;
  payload: TPayload;
}

export type BridgeResponse<TResult = unknown> =
  | {
      id: string;
      ok: true;
      result: TResult;
    }
  | {
      id: string;
      ok: false;
      error: {
        code: string;
        message: string;
      };
    };

interface WebViewMessageEvent {
  data: BridgeResponse;
}

interface WebViewHost {
  postMessage(message: unknown): void;
  addEventListener?(type: 'message', handler: (event: WebViewMessageEvent) => void): void;
  removeEventListener?(type: 'message', handler: (event: WebViewMessageEvent) => void): void;
}

interface BridgeRequestOptions {
  timeoutMs?: number;
}

interface PendingBridgeRequest {
  resolve(value: unknown): void;
  reject(reason?: unknown): void;
  timeout: ReturnType<typeof setTimeout>;
}

interface BridgeState {
  pending: Map<string, PendingBridgeRequest>;
}

const bridgeStates = new WeakMap<WebViewHost, BridgeState>();

declare global {
  interface Window {
    chrome?: {
      webview?: WebViewHost;
    };
  }
}

export function createBridgeRequest<TPayload>(
  type: BridgeCommandType,
  payload: TPayload,
  id = createRequestId(),
): BridgeRequest<TPayload> {
  return {
    id,
    type,
    payload,
  };
}

export function isWebViewBridgeAvailable(): boolean {
  return typeof window !== 'undefined' && typeof window.chrome?.webview?.postMessage === 'function';
}

export async function requestBridge<TResult = unknown, TPayload = unknown>(
  type: BridgeCommandType,
  payload = {} as TPayload,
  options: BridgeRequestOptions = {},
): Promise<TResult> {
  if (!isWebViewBridgeAvailable()) {
    return getBrowserMockResponse(type, payload) as TResult;
  }

  const webview = window.chrome?.webview;
  if (!webview?.addEventListener || !webview.removeEventListener) {
    throw new Error('WebView2 bridge does not support response message events.');
  }

  const request = createBridgeRequest(type, payload);
  const timeoutMs = options.timeoutMs ?? 20000;
  const state = getBridgeState(webview);
  return await new Promise<TResult>((resolve, reject) => {
    const timeout = setTimeout(() => {
      if (!state.pending.delete(request.id)) {
        return;
      }

      reject(new Error('Timed out waiting for native WebView2 response.'));
    }, timeoutMs);

    state.pending.set(request.id, {
      resolve: (value) => resolve(value as TResult),
      reject,
      timeout,
    });

    try {
      webview.postMessage(request);
    } catch (cause) {
      clearTimeout(timeout);
      state.pending.delete(request.id);
      reject(cause);
    }
  });
}

function getBridgeState(webview: WebViewHost): BridgeState {
  const existing = bridgeStates.get(webview);
  if (existing) {
    return existing;
  }

  const state: BridgeState = {
    pending: new Map(),
  };
  webview.addEventListener?.('message', (event) => {
    const response = event.data;
    const pending = state.pending.get(response.id);
    if (!pending) {
      return;
    }

    state.pending.delete(response.id);
    clearTimeout(pending.timeout);
    if (response.ok) {
      pending.resolve(response.result);
    } else {
      pending.reject(new Error(response.error.message));
    }
  });
  bridgeStates.set(webview, state);
  return state;
}

function createRequestId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return 'request-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
}

function getBrowserMockResponse(type: BridgeCommandType, payload: unknown): unknown {
  switch (type) {
    case 'auth.bootstrap':
      return { session: null };

    case 'auth.login':
      const serverUrl = getPayloadValue(payload, 'serverUrl') || 'https://emby.local';
      const userName = getPayloadValue(payload, 'username') || 'Demo User';
      return {
        session: {
          serverUrl,
          userId: 'browser-user',
          userName,
          accessToken: 'browser-mock-token',
          authorization:
            'Emby UserId="browser-user", Client="Noira", Device="Browser", DeviceId="browser", Version="0.1.0"',
        },
      };

    case 'auth.logout':
      return { signedOut: true };

    case 'emby.get':
      return { status: 501, statusText: 'Browser mock unavailable', body: '{}' };

    case 'playback.nativePlayItem':
      return { started: true, surface: 'native' };
  }
}

function getPayloadValue(payload: unknown, key: string): string {
  if (payload && typeof payload === 'object' && key in payload) {
    const value = (payload as Record<string, unknown>)[key];
    return typeof value === 'string' ? value : '';
  }

  return '';
}
