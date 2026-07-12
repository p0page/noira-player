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

export interface HostPlaybackReturnedEvent {
  type: 'host.lifecycle';
  event: 'playback-returned';
}

export interface HostActivatedHomeEvent {
  type: 'host.lifecycle';
  event: 'activated-home';
}

export type HostLifecycleEvent =
  | HostActivatedHomeEvent
  | HostPlaybackReturnedEvent;

export const hostInputVersion = 1 as const;

export type HostInputCommand =
  | 'accept'
  | 'back'
  | 'menu'
  | 'moveDown'
  | 'moveLeft'
  | 'moveRight'
  | 'moveUp'
  | 'view';

export type HostInputPhase = 'pressed' | 'released' | 'repeated';

export type HostInputSource = 'gamepad' | 'keyboard' | 'remote';

export interface HostInputEvent {
  type: 'host.input';
  version: typeof hostInputVersion;
  sequence: number;
  command: HostInputCommand;
  phase: HostInputPhase;
  source: HostInputSource;
  timestamp: number;
}

export interface HostReadyRequest {
  type: 'host.ready';
  inputVersion: typeof hostInputVersion;
}

export interface HostNativeBackRequest {
  type: 'host.nativeBack';
}

interface WebViewMessageEvent {
  data: unknown;
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
  inputListeners: Set<(event: HostInputEvent) => void>;
  lastInputSequence: number | null;
  lifecycleListeners: Set<(event: HostLifecycleEvent) => void>;
  pending: Map<string, PendingBridgeRequest>;
}

const bridgeStates = new WeakMap<WebViewHost, BridgeState>();
const readyHosts = new WeakSet<WebViewHost>();

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
    throw new Error('Noira catalog requires the WebView2 host.');
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

export function subscribeHostLifecycle(
  listener: (event: HostLifecycleEvent) => void,
): () => void {
  const webview = typeof window !== 'undefined' ? window.chrome?.webview : undefined;
  if (
    !webview ||
    typeof webview.postMessage !== 'function' ||
    !webview.addEventListener ||
    !webview.removeEventListener
  ) {
    return () => undefined;
  }

  const state = getBridgeState(webview);
  state.lifecycleListeners.add(listener);
  return () => {
    state.lifecycleListeners.delete(listener);
  };
}

export function subscribeHostInput(
  listener: (event: HostInputEvent) => void,
): () => void {
  const webview = typeof window !== 'undefined' ? window.chrome?.webview : undefined;
  if (
    !webview ||
    typeof webview.postMessage !== 'function' ||
    !webview.addEventListener ||
    !webview.removeEventListener
  ) {
    return () => undefined;
  }

  const state = getBridgeState(webview);
  state.inputListeners.add(listener);
  return () => {
    state.inputListeners.delete(listener);
  };
}

export function postHostReady(force = false): boolean {
  const webview = typeof window !== 'undefined' ? window.chrome?.webview : undefined;
  if (!webview || typeof webview.postMessage !== 'function') {
    return false;
  }
  if (!force && readyHosts.has(webview)) {
    return true;
  }

  const request: HostReadyRequest = {
    type: 'host.ready',
    inputVersion: hostInputVersion,
  };
  try {
    webview.postMessage(request);
    readyHosts.add(webview);
    return true;
  } catch {
    return false;
  }
}

export function postNativeBack(): boolean {
  const webview = typeof window !== 'undefined' ? window.chrome?.webview : undefined;
  if (!webview || typeof webview.postMessage !== 'function') {
    return false;
  }

  const request: HostNativeBackRequest = {
    type: 'host.nativeBack',
  };
  try {
    webview.postMessage(request);
    return true;
  } catch {
    return false;
  }
}

function getBridgeState(webview: WebViewHost): BridgeState {
  const existing = bridgeStates.get(webview);
  if (existing) {
    return existing;
  }

  const state: BridgeState = {
    inputListeners: new Set(),
    lastInputSequence: null,
    lifecycleListeners: new Set(),
    pending: new Map(),
  };
  webview.addEventListener?.('message', (event) => {
    if (isHostLifecycleEvent(event.data)) {
      for (const listener of [...state.lifecycleListeners]) {
        listener(event.data);
      }
      return;
    }

    if (isHostInputEvent(event.data)) {
      if (
        state.lastInputSequence !== null &&
        event.data.sequence <= state.lastInputSequence
      ) {
        return;
      }

      state.lastInputSequence = event.data.sequence;
      for (const listener of [...state.inputListeners]) {
        listener(event.data);
      }
      return;
    }

    if (!isBridgeResponse(event.data)) {
      return;
    }

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

function isHostLifecycleEvent(value: unknown): value is HostLifecycleEvent {
  return (
    isRecord(value) &&
    value.type === 'host.lifecycle' &&
    (value.event === 'activated-home' || value.event === 'playback-returned')
  );
}

function isHostInputEvent(value: unknown): value is HostInputEvent {
  return (
    isRecord(value) &&
    value.type === 'host.input' &&
    value.version === hostInputVersion &&
    Number.isSafeInteger(value.sequence) &&
    (value.sequence as number) >= 0 &&
    isHostInputCommand(value.command) &&
    isHostInputPhase(value.phase) &&
    isHostInputSource(value.source) &&
    Number.isSafeInteger(value.timestamp) &&
    (value.timestamp as number) >= 0
  );
}

function isHostInputCommand(value: unknown): value is HostInputCommand {
  return (
    value === 'accept' ||
    value === 'back' ||
    value === 'menu' ||
    value === 'moveDown' ||
    value === 'moveLeft' ||
    value === 'moveRight' ||
    value === 'moveUp' ||
    value === 'view'
  );
}

function isHostInputPhase(value: unknown): value is HostInputPhase {
  return value === 'pressed' || value === 'released' || value === 'repeated';
}

function isHostInputSource(value: unknown): value is HostInputSource {
  return value === 'gamepad' || value === 'keyboard' || value === 'remote';
}

function isBridgeResponse(value: unknown): value is BridgeResponse {
  if (!isRecord(value) || typeof value.id !== 'string' || typeof value.ok !== 'boolean') {
    return false;
  }

  if (value.ok) {
    return 'result' in value;
  }

  return (
    isRecord(value.error) &&
    typeof value.error.code === 'string' &&
    typeof value.error.message === 'string'
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function createRequestId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return 'request-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
}
