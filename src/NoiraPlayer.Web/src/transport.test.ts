import { describe, expect, it, vi } from 'vitest';
import { createEmbyFetchTransport } from './transport';
import type { SessionBootstrap } from './types';

const session: SessionBootstrap = {
  serverUrl: 'https://media.example/emby',
  userId: 'user-1',
  userName: 'Alice',
  accessToken: 'token-1',
  authorization: 'Emby UserId="user-1", Client="Noira"',
};

describe('createEmbyFetchTransport', () => {
  it('keeps using browser fetch while the Emby server supports it', async () => {
    const directResponse = jsonResponse({ Items: [] });
    const directFetch = vi.fn(async () => directResponse);
    const bridge = vi.fn();
    const transport = createEmbyFetchTransport(session, {
      directFetch,
      bridgeRequest: bridge,
      canUseNativeBridge: () => true,
    });

    const response = await transport('https://media.example/emby/Users/user-1/Views');

    expect(response.ok).toBe(true);
    expect(response).toBe(directResponse);
    expect(directFetch).toHaveBeenCalledOnce();
    expect(bridge).not.toHaveBeenCalled();
  });

  it('switches once to the native GET transport after a browser network failure', async () => {
    const directFetch = vi.fn(async () => {
      throw new TypeError('Failed to fetch');
    });
    const bridge = vi.fn(async () => ({
      status: 200,
      statusText: 'OK',
      body: '{"Items":[]}',
      timing: {
        networkMs: 12.5,
        bodyBytes: 12,
      },
    }));
    const now = vi.fn()
      .mockReturnValueOnce(100)
      .mockReturnValueOnce(104)
      .mockReturnValueOnce(200)
      .mockReturnValueOnce(203);
    const transport = createEmbyFetchTransport(session, {
      directFetch,
      bridgeRequest: bridge,
      canUseNativeBridge: () => true,
      now,
    });

    const first = await transport(
      'https://media.example/emby/Users/user-1/Views?Fields=ImageTags',
    );
    const second = await transport(
      'https://media.example/emby/Users/user-1/Items?ParentId=movies',
    );

    expect(await first.json()).toEqual({ Items: [] });
    expect(second.status).toBe(200);
    expect(first.headers.get('X-Noira-Transport')).toBe('native');
    expect(first.headers.get('X-Noira-Network-Ms')).toBe('12.5');
    expect(first.headers.get('X-Noira-Bridge-Ms')).toBe('4');
    expect(first.headers.get('X-Noira-Body-Bytes')).toBe('12');
    expect(directFetch).toHaveBeenCalledOnce();
    expect(bridge).toHaveBeenNthCalledWith(1, 'emby.get', {
      path: 'Users/user-1/Views?Fields=ImageTags',
    });
    expect(bridge).toHaveBeenNthCalledWith(2, 'emby.get', {
      path: 'Users/user-1/Items?ParentId=movies',
    });
  });

  it('does not use native transport for an HTTP response or outside the saved server', async () => {
    const directFetch = vi.fn(async () => new Response('', { status: 401 }));
    const bridge = vi.fn();
    const transport = createEmbyFetchTransport(session, {
      directFetch,
      bridgeRequest: bridge,
      canUseNativeBridge: () => true,
    });

    const response = await transport('https://media.example/emby/Users/user-1/Views');
    await expect(
      transport('https://other.example/Users/user-1/Views'),
    ).rejects.toThrow('outside the saved Emby server');

    expect(response.status).toBe(401);
    expect(bridge).not.toHaveBeenCalled();
  });
});

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}
