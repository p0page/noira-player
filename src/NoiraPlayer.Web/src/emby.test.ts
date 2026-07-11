import { describe, expect, it, vi } from 'vitest';
import { EmbyRequestError, EmbyWebClient } from './emby';
import type { SessionBootstrap } from './types';

const session: SessionBootstrap = {
  serverUrl: 'https://media.example/emby/',
  userId: 'user 1/slash',
  userName: 'Test User',
  accessToken: 'token +/test',
  authorization:
    'Emby UserId="user 1/slash", Client="Noira", Device="Test", DeviceId="test-device", Version="0.0.0"',
};

const mappedItemFields = [
  'Overview',
  'ProductionYear',
  'RunTimeTicks',
  'SeriesName',
  'IndexNumber',
  'ParentIndexNumber',
  'MediaSources',
  'UserData',
  'ImageTags',
  'BackdropImageTags',
  'PrimaryImageItemId',
  'ParentThumbItemId',
  'ParentBannerItemId',
  'ParentBackdropItemId',
  'ParentThumbImageTag',
];

const fallbackLibraryItemTypes =
  'Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,MusicAlbum,Audio,Photo';
const maxStartIndex = 1_000_000;

interface LibraryQueryExpectation {
  label: string;
  collectionType?: string;
  includeItemTypes: string;
  mediaTypes?: string;
  isFolder?: string;
}

const libraryQueryExpectations: LibraryQueryExpectation[] = [
  { label: 'movies', collectionType: 'movies', includeItemTypes: 'Movie' },
  { label: 'TV shows', collectionType: 'tvshows', includeItemTypes: 'Series' },
  {
    label: 'box sets',
    collectionType: 'boxsets',
    includeItemTypes: 'BoxSet',
    isFolder: 'false',
  },
  {
    label: 'playlists',
    collectionType: 'playlists',
    includeItemTypes: 'Playlist',
    isFolder: 'false',
  },
  { label: 'music', collectionType: 'music', includeItemTypes: 'MusicAlbum,Audio' },
  {
    label: 'photos',
    collectionType: 'photos',
    includeItemTypes: 'Photo',
    mediaTypes: 'Photo',
  },
  { label: 'home videos', collectionType: 'homevideos', includeItemTypes: 'Video' },
  {
    label: 'unknown libraries',
    collectionType: 'unknown',
    includeItemTypes: fallbackLibraryItemTypes,
  },
  { label: 'omitted options', includeItemTypes: fallbackLibraryItemTypes },
];

const startIndexExpectations = [
  { label: 'NaN', value: Number.NaN, expected: 0 },
  { label: 'positive infinity', value: Number.POSITIVE_INFINITY, expected: 0 },
  { label: 'negative', value: -12, expected: 0 },
  { label: 'fractional', value: 12.9, expected: 12 },
  { label: 'over the maximum', value: maxStartIndex + 500, expected: maxStartIndex },
];

const invalidResponseStartIndices = [
  { label: 'null', value: null },
  { label: 'NaN', value: Number.NaN },
  { label: 'infinity', value: Number.POSITIVE_INFINITY },
  { label: 'negative', value: -1 },
  { label: 'fractional', value: 1.5 },
  { label: 'over the maximum', value: maxStartIndex + 1 },
];

const invalidTotalRecordCountResponses = [
  { label: 'missing', response: { Items: [] } },
  { label: 'null', response: { Items: [], TotalRecordCount: null } },
  {
    label: 'non-finite',
    response: { Items: [], TotalRecordCount: Number.POSITIVE_INFINITY },
  },
  { label: 'negative', response: { Items: [], TotalRecordCount: -1 } },
  {
    label: 'shorter than the returned page',
    response: {
      Items: [{ Id: 'item-page', Name: 'Page item', Type: 'Video' }],
      StartIndex: 5,
      TotalRecordCount: 5,
    },
  },
];

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
            Id: 'view-1',
            Name: 'Library',
            Type: 'CollectionFolder',
            CollectionType: 'movies',
            ImageTags: { Primary: 'test-tag' },
          },
        ],
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const views = await client.getViews();

    expect(views).toEqual([
      {
        id: 'view-1',
        name: 'Library',
        collectionType: 'movies',
        imageUrl:
          'https://media.example/emby/Items/view-1/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
      },
    ]);
    expect(fetcher).toHaveBeenCalledOnce();
    const [url, init] = fetcher.mock.calls[0];
    expect(String(url)).toBe(
      'https://media.example/emby/Users/user%201%2Fslash/Views?Fields=PrimaryImageAspectRatio%2CImageTags',
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

  it('loads resume items and maps playback, episode, and parent-owned artwork fields', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Items: [
          {
            Id: 'item-1',
            Name: 'Item',
            Type: 'Episode',
            ProductionYear: 2026,
            RunTimeTicks: 900,
            SeriesName: 'Series',
            IndexNumber: 4,
            ParentIndexNumber: 2,
            Overview: 'Summary',
            UserData: { PlaybackPositionTicks: 123 },
            MediaSources: [{ Id: 'source-1' }],
            ImageTags: {
              Primary: 'primary-tag',
              Thumb: 'thumb-tag',
              Banner: 'banner-tag',
            },
            BackdropImageTags: ['backdrop-tag'],
            PrimaryImageItemId: 'primary owner',
            ParentThumbItemId: 'thumb/owner',
            ParentBannerItemId: 'banner owner',
            ParentBackdropItemId: 'backdrop owner',
          },
        ],
        TotalRecordCount: 1,
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const items = await client.getResumeItems(24);

    expect(items).toEqual([
      {
        id: 'item-1',
        name: 'Item',
        type: 'Episode',
        productionYear: 2026,
        runtimeTicks: 900,
        seriesName: 'Series',
        indexNumber: 4,
        parentIndexNumber: 2,
        overview: 'Summary',
        startPositionTicks: 123,
        mediaSourceId: 'source-1',
        artwork: {
          primary:
            'https://media.example/emby/Items/primary%20owner/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
          thumb:
            'https://media.example/emby/Items/thumb%2Fowner/Images/Thumb?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
          banner:
            'https://media.example/emby/Items/banner%20owner/Images/Banner?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
          backdrop:
            'https://media.example/emby/Items/backdrop%20owner/Images/Backdrop?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
        },
        imageUrl:
          'https://media.example/emby/Items/primary%20owner/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
      },
    ]);

    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.pathname).toBe('/emby/Users/user%201%2Fslash/Items/Resume');
    expect(url.searchParams.get('IncludeItemTypes')).toBe('Movie,Episode');
    expect(url.searchParams.get('Fields')).toBe(mappedItemFields.join(','));
    expect(url.searchParams.get('Limit')).toBe('24');
    expectImageQuery(url);
  });

  it('loads next-up items from the global endpoint with exactly one user identity', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Items: [{ Id: 'item-next', Name: 'Next item', Type: 'Episode' }],
        TotalRecordCount: 1,
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const items = await client.getNextUpItems(24);

    expect(items[0]).toMatchObject({ id: 'item-next', name: 'Next item', type: 'Episode' });
    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.pathname).toBe('/emby/Shows/NextUp');
    expect(url.searchParams.getAll('UserId')).toEqual([session.userId]);
    expect(url.searchParams.get('Fields')).toBe(mappedItemFields.join(','));
    expect(url.searchParams.get('Limit')).toBe('24');
    expectImageQuery(url);
  });

  it('loads latest items from a top-level array with library options', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse([{ Id: 'item-latest', Name: 'Latest item', Type: 'Movie' }]),
    );
    const client = new EmbyWebClient(session, fetcher);

    const items = await client.getLatestItems({
      parentId: 'view /1',
      includeItemTypes: 'Movie,Episode',
      limit: 16,
    });

    expect(items[0]).toMatchObject({ id: 'item-latest', name: 'Latest item', type: 'Movie' });
    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.pathname).toBe('/emby/Users/user%201%2Fslash/Items/Latest');
    expect(url.searchParams.get('ParentId')).toBe('view /1');
    expect(url.searchParams.get('IncludeItemTypes')).toBe('Movie,Episode');
    expect(url.searchParams.get('Fields')).toBe(mappedItemFields.join(','));
    expect(url.searchParams.get('Limit')).toBe('16');
    expectImageQuery(url);
  });

  it('maps a null latest response to an empty list', async () => {
    const client = new EmbyWebClient(
      session,
      vi.fn(async () => jsonResponse(null)),
    );

    await expect(client.getLatestItems()).resolves.toEqual([]);
  });

  it('normalizes every catalog limit to a finite integer from 1 through 100', async () => {
    const fetcher = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
      const url = new URL(String(input));
      return url.pathname.endsWith('/Latest')
        ? objectResponse([])
        : objectResponse({ Items: [], TotalRecordCount: 0 });
    });
    const client = new EmbyWebClient(session, fetcher);

    await client.getResumeItems(Number.NaN);
    await client.getNextUpItems(Number.POSITIVE_INFINITY);
    await client.getLatestItems({ limit: -3 });
    await client.getItemsPage('view-1', 0, 12.9);
    await client.getLatestItems({ limit: 10_000 });

    const limits = fetcher.mock.calls.map(([input]) =>
      new URL(String(input)).searchParams.get('Limit'),
    );
    expect(limits).toEqual(['20', '20', '1', '12', '100']);
  });

  it('loads a page of library items and preserves server paging metadata', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Items: [{ Id: 'item-page', Name: 'Page item', Type: 'Video' }],
        StartIndex: 25,
        TotalRecordCount: 81,
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const page = await client.getItemsPage('folder /1', 25, 20);

    expect(page).toMatchObject({
      items: [{ id: 'item-page', name: 'Page item', type: 'Video' }],
      startIndex: 25,
      totalRecordCount: 81,
    });
    expect(page.items[0].artwork).toEqual({
      primary: undefined,
      thumb: undefined,
      banner: undefined,
      backdrop: undefined,
    });
    expect(page.items[0].imageUrl).toBeUndefined();

    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.pathname).toBe('/emby/Users/user%201%2Fslash/Items');
    expect(url.searchParams.get('ParentId')).toBe('folder /1');
    expect(url.searchParams.get('IncludeItemTypes')).toBe(fallbackLibraryItemTypes);
    expect(url.searchParams.get('Recursive')).toBe('true');
    expect(url.searchParams.get('SortBy')).toBe('SortName');
    expect(url.searchParams.get('SortOrder')).toBe('Ascending');
    expect(url.searchParams.get('StartIndex')).toBe('25');
    expect(url.searchParams.get('Limit')).toBe('20');
    expect(url.searchParams.get('Fields')).toBe(mappedItemFields.join(','));
    expectImageQuery(url);
  });

  it('accepts an empty page beyond the current total record count', async () => {
    const client = new EmbyWebClient(
      session,
      vi.fn(async () =>
        objectResponse({
          Items: [],
          StartIndex: 100,
          TotalRecordCount: 50,
        }),
      ),
    );

    await expect(client.getItemsPage('view-1', 100, 20)).resolves.toEqual({
      items: [],
      startIndex: 100,
      totalRecordCount: 50,
    });
  });

  it.each(startIndexExpectations)(
    'normalizes a $label start index without sending an invalid number',
    async ({ value, expected }) => {
      const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
        objectResponse({ Items: [], TotalRecordCount: maxStartIndex }),
      );
      const client = new EmbyWebClient(session, fetcher);

      const page = await client.getItemsPage('view-1', value, 20);

      const url = new URL(String(fetcher.mock.calls[0][0]));
      expect(url.searchParams.get('StartIndex')).toBe(String(expected));
      expect(page.startIndex).toBe(expected);
    },
  );

  it.each(invalidResponseStartIndices)(
    'rejects a $label response StartIndex as a protocol error',
    async ({ value }) => {
      const client = new EmbyWebClient(
        session,
        vi.fn(async () =>
          objectResponse({
            Items: [],
            StartIndex: value,
            TotalRecordCount: maxStartIndex + 1,
          }),
        ),
      );

      await expect(client.getItemsPage('view-1', 0, 20)).rejects.toMatchObject({
        name: 'EmbyProtocolError',
        message: expect.stringContaining('StartIndex'),
      });
    },
  );

  it.each(invalidTotalRecordCountResponses)(
    'rejects a $label TotalRecordCount as a protocol error',
    async ({ response }) => {
      const client = new EmbyWebClient(
        session,
        vi.fn(async () => objectResponse(response)),
      );

      await expect(client.getItemsPage('view-1', 0, 20)).rejects.toMatchObject({
        name: 'EmbyProtocolError',
        message: expect.stringContaining('TotalRecordCount'),
      });
    },
  );

  it.each(libraryQueryExpectations)(
    'uses the native library item strategy for $label',
    async ({ collectionType, includeItemTypes, mediaTypes, isFolder }) => {
      const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
        jsonResponse({ Items: [], StartIndex: 0, TotalRecordCount: 0 }),
      );
      const client = new EmbyWebClient(session, fetcher);

      if (collectionType === undefined) {
        await client.getItemsPage('view-1', 0, 20);
      } else {
        await client.getItemsPage('view-1', 0, 20, { collectionType });
      }

      const url = new URL(String(fetcher.mock.calls[0][0]));
      expect(url.searchParams.get('IncludeItemTypes')).toBe(includeItemTypes);
      expect(url.searchParams.get('MediaTypes')).toBe(mediaTypes ?? null);
      expect(url.searchParams.get('IsFolder')).toBe(isFolder ?? null);
      if (collectionType === 'tvshows') {
        expect(url.searchParams.get('IncludeItemTypes')).not.toContain('Episode');
      }
    },
  );

  it('lets an explicit include-item-types option override the collection mapping', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      objectResponse({ Items: [], StartIndex: 0, TotalRecordCount: 0 }),
    );
    const client = new EmbyWebClient(session, fetcher);

    await client.getItemsPage('view-1', 0, 20, {
      collectionType: 'tvshows',
      includeItemTypes: 'Episode',
    });

    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.searchParams.get('IncludeItemTypes')).toBe('Episode');
  });

  it('keeps getItems as a temporary wrapper over the first paged result', async () => {
    const client = new EmbyWebClient(
      session,
      vi.fn(async () => {
        throw new Error('getItems should delegate without fetching directly');
      }),
    );
    const itemsPage = {
      items: [],
      startIndex: 0,
      totalRecordCount: 0,
    };
    const getItemsPage = vi.spyOn(client, 'getItemsPage').mockResolvedValue(itemsPage);

    await expect(client.getItems('view-1')).resolves.toBe(itemsPage.items);
    expect(getItemsPage).toHaveBeenCalledWith('view-1', 0, 50, {
      includeItemTypes: 'Movie,Episode,Video',
    });
  });

  it('loads one item with image metadata and maps own and parent-owned artwork', async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      jsonResponse({
        Id: 'item 2',
        Name: 'Item details',
        Type: 'Episode',
        ImageTags: {
          Primary: 'primary-tag',
          Thumb: 'thumb-tag',
          Banner: 'banner-tag',
        },
        BackdropImageTags: ['backdrop-tag'],
        ParentThumbItemId: 'thumb owner',
        ParentBannerItemId: 'banner owner',
        ParentBackdropItemId: 'backdrop owner',
      }),
    );
    const client = new EmbyWebClient(session, fetcher);

    const item = await client.getItem('item 2');

    expect(item.id).toBe('item 2');
    expect(item.artwork).toEqual({
      primary:
        'https://media.example/emby/Items/item%202/Images/Primary?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
      thumb:
        'https://media.example/emby/Items/thumb%20owner/Images/Thumb?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
      banner:
        'https://media.example/emby/Items/banner%20owner/Images/Banner?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
      backdrop:
        'https://media.example/emby/Items/backdrop%20owner/Images/Backdrop?maxWidth=480&quality=90&api_key=token+%2B%2Ftest',
    });

    const url = new URL(String(fetcher.mock.calls[0][0]));
    expect(url.pathname).toBe('/emby/Users/user%201%2Fslash/Items/item%202');
    expect(url.searchParams.get('Fields')).toBe(mappedItemFields.join(','));
    expectImageQuery(url);
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

function expectImageQuery(url: URL): void {
  expect(url.searchParams.get('EnableImages')).toBe('true');
  expect(url.searchParams.get('EnableImageTypes')).toBe('Primary,Backdrop,Thumb,Banner,Logo');
  expect(url.searchParams.get('ImageTypeLimit')).toBe('1');
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

function objectResponse(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    json: async () => body,
  } as Response;
}
