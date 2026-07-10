import { isWebViewBridgeAvailable, requestBridge } from './bridge';
import type { SessionBootstrap } from './types';

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
type BridgeRequestLike = (
  type: 'emby.get',
  payload: unknown,
) => Promise<NativeEmbyGetResult>;

interface NativeEmbyGetResult {
  status: number;
  statusText: string;
  body: string;
}

interface EmbyFetchTransportOptions {
  directFetch?: FetchLike;
  bridgeRequest?: BridgeRequestLike;
  canUseNativeBridge?: () => boolean;
}

export function createEmbyFetchTransport(
  session: SessionBootstrap,
  options: EmbyFetchTransportOptions = {},
): FetchLike {
  const directFetch = options.directFetch ?? ((input, init) => fetch(input, init));
  const bridgeRequest =
    options.bridgeRequest ??
    ((type, payload) => requestBridge<NativeEmbyGetResult>(type, payload));
  const canUseNativeBridge = options.canUseNativeBridge ?? isWebViewBridgeAvailable;
  const serverBase = new URL(session.serverUrl.trim().replace(/\/+$/, '') + '/');
  let nativeMode = false;

  return async (input, init) => {
    const path = createNativePath(serverBase, input);
    const method =
      init?.method ??
      (typeof Request !== 'undefined' && input instanceof Request ? input.method : 'GET');
    if (method.toUpperCase() !== 'GET') {
      throw new Error('The Emby hybrid transport only supports GET requests.');
    }

    if (!nativeMode) {
      try {
        return await directFetch(input, init);
      } catch (cause) {
        if (!(cause instanceof TypeError) || !canUseNativeBridge()) {
          throw cause;
        }

        nativeMode = true;
      }
    }

    const result = await bridgeRequest('emby.get', { path });
    return new Response(result.body, {
      status: result.status,
      statusText: result.statusText,
      headers: { 'Content-Type': 'application/json' },
    });
  };
}

function createNativePath(serverBase: URL, input: RequestInfo | URL): string {
  const rawUrl =
    typeof input === 'string' || input instanceof URL
      ? String(input)
      : input.url;
  const requestUrl = new URL(rawUrl);
  if (requestUrl.origin !== serverBase.origin || !requestUrl.pathname.startsWith(serverBase.pathname)) {
    throw new Error('The request URL is outside the saved Emby server.');
  }

  if (requestUrl.hash) {
    throw new Error('Emby API request fragments are not supported.');
  }

  const relativePath = requestUrl.pathname.slice(serverBase.pathname.length) + requestUrl.search;
  if (!relativePath) {
    throw new Error('The Emby API path is empty.');
  }

  return relativePath;
}
