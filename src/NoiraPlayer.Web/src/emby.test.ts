import { describe, expect, it, vi } from 'vitest';
import { EmbyRequestError, EmbyWebClient } from './emby';
import type { SessionBootstrap } from './types';

const session: SessionBootstrap = {
  serverUrl: 'https://media.example/emby',
  userId: 'user 7',
  userName: 'Alice',
  accessToken: 'token +/42',
  authorization:
    'Emby UserId="user 7", Client="Noira", Device="Xbox", DeviceId="device-1", Version="0.1.0"',
};

describe('EmbyWebClient', () => {
  it('does not rebind the browser fetch function to the client instance', async () => {
    let receiver: unknown;
    const fetcher = vi.fn(function (
      this: unknown,
      _input: RequestInfo | URL,
      _init?: RequestInit,
    ) {
      receiver = this;
      if (this instanceof EmbyWebClient) {
        throw new TypeError('Illegal invocation');
      }

      return Promise.resolve(jsonResponse({ Items: [] }));
    });
    const client = new EmbyWebClient(session, fetcher);

    await expect(client.getViews()).resolves.toEqual([]);
    expect(receiver).not.toBe(client);
  });

  it('loads user views directly with the native bootstrap identity', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Items: [
          {
            Id: 'movies',
            Name: 'Movies',
            Type: 'CollectionFolder',
            CollectionType: 'movies',
            ImageTags: { Primary: 'image-tag' },
          },
        ],
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const views = await client.getViews();

    expect(views).toEqual([
      {
        id: 'movies',
        name: 'Movies',
        collectionType: 'movies',
        imageUrl:
          'https://media.example/emby/Items/movies/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2F42',
      },
    ]);
    expect(fetcher).toHaveBeenCalledOnce();
    const [url, init] = fetcher.mock.calls[0];
    expect(String(url)).toBe(
      'https://media.example/emby/Users/user%207/Views?Fields=PrimaryImageAspectRatio%2CImageTags',
    );
    expect(init).toMatchObject({
      method: 'GET',
      headers: {
        Accept: 'application/json',
        Authorization: session.authorization,
        'X-Emby-Token': session.accessToken,
      },
    });
  });

  it('maps item details into the native playback launch hints', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Items: [
          {
            Id: 'movie-1',
            Name: 'Movie One',
            Type: 'Movie',
            Overview: 'Overview',
            RunTimeTicks: 900,
            UserData: { PlaybackPositionTicks: 123 },
            MediaSources: [{ Id: 'source-1' }],
            ImageTags: { Primary: 'tag-1' },
          },
        ],
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const items = await client.getItems('folder/one');

    expect(items).toEqual([
      {
        id: 'movie-1',
        name: 'Movie One',
        type: 'Movie',
        overview: 'Overview',
        runtimeTicks: 900,
        startPositionTicks: 123,
        mediaSourceId: 'source-1',
        imageUrl:
          'https://media.example/emby/Items/movie-1/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2F42',
      },
    ]);
    expect(String(fetcher.mock.calls[0][0])).toContain('ParentId=folder%2Fone');
    expect(String(fetcher.mock.calls[0][0])).toContain('IncludeItemTypes=Movie%2CEpisode%2CVideo');
    expect(String(fetcher.mock.calls[0][0])).toContain('Fields=Overview%2CRunTimeTicks%2CMediaSources%2CUserData%2CImageTags');
  });

  it('loads one item from the user-scoped details endpoint', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Id: 'episode 2',
        Name: 'Episode Two',
        Type: 'Episode',
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const item = await client.getItem('episode 2');

    expect(item.id).toBe('episode 2');
    expect(String(fetcher.mock.calls[0][0])).toBe(
      'https://media.example/emby/Users/user%207/Items/episode%202?Fields=Overview%2CRunTimeTicks%2CMediaSources%2CUserData%2CImageTags',
    );
  });

  it('reports Emby HTTP failures without returning fabricated data', async () => {
    const client = new EmbyWebClient(
      session,
      vi.fn(async () => new Response('', { status: 401, statusText: 'Unauthorized' })),
    );

    await expect(client.getViews()).rejects.toEqual(
      new EmbyRequestError(401, 'Emby request failed: 401 Unauthorized'),
    );
  });
});

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}
