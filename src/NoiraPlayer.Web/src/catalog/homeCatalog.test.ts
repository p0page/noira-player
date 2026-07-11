import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  loadHomeCatalog,
  loadLibraryLatestCatalog,
  loadLibraryLatestRows,
} from './homeCatalog';
import type { HomeCatalogClient } from './homeCatalog';
import type { LatestItemsOptions, LibraryView, MediaItem } from '../types';

const latestItemTypeCases = [
  { collectionType: 'movies', includeItemTypes: 'Movie' },
  { collectionType: 'tvshows', includeItemTypes: 'Series' },
  { collectionType: 'boxsets', includeItemTypes: 'BoxSet' },
  { collectionType: 'playlists', includeItemTypes: 'Playlist' },
  { collectionType: 'music', includeItemTypes: 'MusicAlbum,Audio' },
  { collectionType: 'photos', includeItemTypes: 'Photo' },
  { collectionType: 'homevideos', includeItemTypes: 'Video' },
] as const;

describe('loadHomeCatalog', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts every core request before any resolves and builds rows in fixed kind order', async () => {
    const resumeRequest = deferred<MediaItem[]>();
    const nextUpRequest = deferred<MediaItem[]>();
    const librariesRequest = deferred<LibraryView[]>();
    const latestRequest = deferred<MediaItem[]>();
    const started: string[] = [];

    const getResumeItems = vi.fn((limit: number) => {
      started.push(`resume:${limit}`);
      return resumeRequest.promise;
    });
    const getNextUpItems = vi.fn((limit: number) => {
      started.push(`nextUp:${limit}`);
      return nextUpRequest.promise;
    });
    const getViews = vi.fn(() => {
      started.push('libraries');
      return librariesRequest.promise;
    });
    const getLatestItems = vi.fn((options: LatestItemsOptions = {}) => {
      started.push(`latest:${options.limit}`);
      return latestRequest.promise;
    });
    const client: HomeCatalogClient = {
      getResumeItems,
      getNextUpItems,
      getViews,
      getLatestItems,
    };

    const resumeItems = [anonymousItem('anonymous-resume')];
    const nextUpItems = [anonymousItem('anonymous-next-up')];
    const libraries = [anonymousLibrary('anonymous-library', 'Anonymous Library', 'movies')];
    const latestItems = [anonymousItem('anonymous-latest')];

    const catalogPromise = loadHomeCatalog(client);

    expect(started).toEqual(['resume:24', 'nextUp:24', 'libraries', 'latest:24']);
    expect(getResumeItems).toHaveBeenCalledWith(24);
    expect(getNextUpItems).toHaveBeenCalledWith(24);
    expect(getViews).toHaveBeenCalledWith();
    expect(getLatestItems).toHaveBeenCalledWith({ limit: 24 });

    latestRequest.resolve(latestItems);
    librariesRequest.resolve(libraries);
    nextUpRequest.resolve(nextUpItems);
    resumeRequest.resolve(resumeItems);

    await expect(catalogPromise).resolves.toEqual({
      rows: [
        {
          key: 'resume',
          title: 'Continue Watching',
          kind: 'resume',
          items: resumeItems,
        },
        {
          key: 'nextUp',
          title: 'Next Up',
          kind: 'nextUp',
          items: nextUpItems,
        },
        {
          key: 'libraries',
          title: 'My Media',
          kind: 'libraries',
          items: libraries,
        },
        {
          key: 'latest',
          title: 'Latest',
          kind: 'latest',
          items: latestItems,
        },
      ],
      failedKinds: [],
    });
  });

  it('omits empty rows, isolates failures, preserves duplicate items, and does not log payloads', async () => {
    const resumeRequest = deferred<MediaItem[]>();
    const librariesRequest = deferred<LibraryView[]>();
    const latestRequest = deferred<MediaItem[]>();
    const duplicateItems = [
      anonymousItem('anonymous-duplicate'),
      anonymousItem('anonymous-duplicate'),
    ];
    const originalItems = [...duplicateItems];
    const logSpies = [
      vi.spyOn(console, 'log').mockImplementation(() => undefined),
      vi.spyOn(console, 'warn').mockImplementation(() => undefined),
      vi.spyOn(console, 'error').mockImplementation(() => undefined),
    ];
    const client: HomeCatalogClient = {
      getResumeItems: () => resumeRequest.promise,
      getNextUpItems: async () => duplicateItems,
      getViews: () => librariesRequest.promise,
      getLatestItems: () => latestRequest.promise,
    };

    const catalogPromise = loadHomeCatalog(client);

    librariesRequest.reject(new Error('anonymous library failure'));
    latestRequest.resolve([]);
    resumeRequest.reject(new Error('anonymous resume failure'));

    await expect(catalogPromise).resolves.toEqual({
      rows: [
        {
          key: 'nextUp',
          title: 'Next Up',
          kind: 'nextUp',
          items: duplicateItems,
        },
      ],
      failedKinds: ['resume', 'libraries'],
    });
    expect(duplicateItems).toEqual(originalItems);
    expect(duplicateItems).toHaveLength(2);
    for (const logSpy of logSpies) {
      expect(logSpy).not.toHaveBeenCalled();
    }
  });

  it('starts the remaining core requests when resume throws synchronously', async () => {
    const started: string[] = [];
    const nextUpItems = [anonymousItem('anonymous-sync-next-up')];
    const libraries = [anonymousLibrary('anonymous-sync-library', 'Anonymous Sync', 'movies')];
    const latestItems = [anonymousItem('anonymous-sync-latest')];
    const client: HomeCatalogClient = {
      getResumeItems: () => {
        started.push('resume');
        throw new Error('anonymous synchronous resume failure');
      },
      getNextUpItems: async () => {
        started.push('nextUp');
        return nextUpItems;
      },
      getViews: async () => {
        started.push('libraries');
        return libraries;
      },
      getLatestItems: async () => {
        started.push('latest');
        return latestItems;
      },
    };

    const catalogPromise = loadHomeCatalog(client);
    const catalogExpectation = expect(catalogPromise).resolves.toEqual({
      rows: [
        {
          key: 'nextUp',
          title: 'Next Up',
          kind: 'nextUp',
          items: nextUpItems,
        },
        {
          key: 'libraries',
          title: 'My Media',
          kind: 'libraries',
          items: libraries,
        },
        {
          key: 'latest',
          title: 'Latest',
          kind: 'latest',
          items: latestItems,
        },
      ],
      failedKinds: ['resume'],
    });

    expect(started).toEqual(['resume', 'nextUp', 'libraries', 'latest']);
    await catalogExpectation;
  });
});

describe('loadLibraryLatestRows', () => {
  it('reports rejected library rows separately from successful empty results', async () => {
    const getLatestItems = vi.fn((options: LatestItemsOptions = {}) => {
      if (options.parentId === 'anonymous-library-a') {
        return Promise.resolve([
          {
            id: 'anonymous-item-a',
            name: 'Anonymous item A',
            type: 'Movie',
            artwork: {},
          },
        ]);
      }
      if (options.parentId === 'anonymous-library-b') {
        return Promise.resolve([]);
      }

      return Promise.reject(new Error('anonymous supplemental failure'));
    });
    const client: HomeCatalogClient = {
      getResumeItems: async () => [],
      getNextUpItems: async () => [],
      getViews: async () => [],
      getLatestItems,
    };

    await expect(
      loadLibraryLatestCatalog(client, [
        {
          id: '  anonymous-library-a  ',
          name: 'Anonymous A',
          collectionType: 'movies',
        },
        {
          id: 'anonymous-library-a',
          name: 'Anonymous duplicate A',
          collectionType: 'tvshows',
        },
        {
          id: ' ',
          name: 'Anonymous blank',
          collectionType: 'movies',
        },
        {
          id: 'anonymous-library-b',
          name: 'Anonymous B',
          collectionType: 'tvshows',
        },
        {
          id: 'anonymous-library-c',
          name: 'Anonymous C',
          collectionType: 'music',
        },
      ]),
    ).resolves.toEqual({
      rows: [
        {
          key: 'latest:anonymous-library-a',
          title: 'Latest in Anonymous A',
          kind: 'latest',
          items: [
            {
              id: 'anonymous-item-a',
              name: 'Anonymous item A',
              type: 'Movie',
              artwork: {},
            },
          ],
        },
      ],
      failedRowKeys: ['latest:anonymous-library-c'],
    });

    expect(getLatestItems.mock.calls.map(([options]) => options?.parentId)).toEqual([
      'anonymous-library-a',
      'anonymous-library-b',
      'anonymous-library-c',
    ]);
  });

  it('starts every valid library request together, then preserves input order while omitting empty and failed rows', async () => {
    const libraries = [
      anonymousLibrary('anonymous-library-a', 'Anonymous A', 'movies'),
      anonymousLibrary('   ', 'Invalid Anonymous Library', 'movies'),
      anonymousLibrary('anonymous-library-b', 'Anonymous B', 'tvshows'),
      anonymousLibrary('anonymous-library-c', 'Anonymous C', 'music'),
      anonymousLibrary('anonymous-library-d', 'Anonymous D', 'homevideos'),
    ];
    const originalLibraries = libraries.map((library) => ({ ...library }));
    const requests: Record<string, Deferred<MediaItem[]>> = {
      'anonymous-library-a': deferred<MediaItem[]>(),
      'anonymous-library-b': deferred<MediaItem[]>(),
      'anonymous-library-c': deferred<MediaItem[]>(),
      'anonymous-library-d': deferred<MediaItem[]>(),
    };
    const started: string[] = [];
    const getLatestItems = vi.fn((options: LatestItemsOptions = {}) => {
      const parentId = options.parentId ?? '';
      started.push(parentId);
      return requests[parentId].promise;
    });
    const client: HomeCatalogClient = {
      getResumeItems: async () => [],
      getNextUpItems: async () => [],
      getViews: async () => [],
      getLatestItems,
    };
    const firstItems = [anonymousItem('anonymous-item-a')];
    const lastItems = [anonymousItem('anonymous-item-d')];

    const rowsPromise = loadLibraryLatestRows(client, libraries);

    expect(started).toEqual([
      'anonymous-library-a',
      'anonymous-library-b',
      'anonymous-library-c',
      'anonymous-library-d',
    ]);
    expect(getLatestItems.mock.calls.map(([options]) => options)).toEqual([
      { parentId: 'anonymous-library-a', includeItemTypes: 'Movie', limit: 24 },
      { parentId: 'anonymous-library-b', includeItemTypes: 'Series', limit: 24 },
      {
        parentId: 'anonymous-library-c',
        includeItemTypes: 'MusicAlbum,Audio',
        limit: 24,
      },
      {
        parentId: 'anonymous-library-d',
        includeItemTypes: 'Video',
        limit: 24,
      },
    ]);

    requests['anonymous-library-d'].resolve(lastItems);
    requests['anonymous-library-c'].resolve([]);
    requests['anonymous-library-b'].reject(new Error('anonymous latest failure'));
    requests['anonymous-library-a'].resolve(firstItems);

    await expect(rowsPromise).resolves.toEqual([
      {
        key: 'latest:anonymous-library-a',
        title: 'Latest in Anonymous A',
        kind: 'latest',
        items: firstItems,
      },
      {
        key: 'latest:anonymous-library-d',
        title: 'Latest in Anonymous D',
        kind: 'latest',
        items: lastItems,
      },
    ]);
    expect(libraries).toEqual(originalLibraries);
  });

  it('snapshots library identity and deduplicates normalized ids before requests settle', async () => {
    const firstLibrary = anonymousLibrary(
      'anonymous-library-a',
      'Anonymous Original A',
      'movies',
    );
    const exactDuplicate = anonymousLibrary(
      'anonymous-library-a',
      'Anonymous Duplicate A',
      'tvshows',
    );
    const paddedDuplicate = anonymousLibrary(
      '  anonymous-library-a  ',
      'Anonymous Padded Duplicate A',
      'music',
    );
    const secondLibrary = anonymousLibrary(
      '  anonymous-library-b  ',
      'Anonymous Original B',
      'tvshows',
    );
    const requests: Record<string, Deferred<MediaItem[]>> = {
      'anonymous-library-a': deferred<MediaItem[]>(),
      'anonymous-library-b': deferred<MediaItem[]>(),
    };
    const getLatestItems = vi.fn((options: LatestItemsOptions = {}) => {
      const parentId = options.parentId ?? '';
      return requests[parentId].promise;
    });
    const client: HomeCatalogClient = {
      getResumeItems: async () => [],
      getNextUpItems: async () => [],
      getViews: async () => [],
      getLatestItems,
    };
    const firstItems = [anonymousItem('anonymous-item-a')];
    const secondItems = [anonymousItem('anonymous-item-b')];

    const rowsPromise = loadLibraryLatestRows(client, [
      firstLibrary,
      exactDuplicate,
      paddedDuplicate,
      secondLibrary,
    ]);

    expect(getLatestItems.mock.calls.map(([options]) => options)).toEqual([
      { parentId: 'anonymous-library-a', includeItemTypes: 'Movie', limit: 24 },
      { parentId: 'anonymous-library-b', includeItemTypes: 'Series', limit: 24 },
    ]);

    firstLibrary.id = 'mutated-library-a';
    firstLibrary.name = 'Mutated Anonymous A';
    firstLibrary.collectionType = 'photos';
    secondLibrary.id = 'mutated-library-b';
    secondLibrary.name = 'Mutated Anonymous B';
    secondLibrary.collectionType = 'homevideos';

    requests['anonymous-library-b'].resolve(secondItems);
    requests['anonymous-library-a'].resolve(firstItems);

    await expect(rowsPromise).resolves.toEqual([
      {
        key: 'latest:anonymous-library-a',
        title: 'Latest in Anonymous Original A',
        kind: 'latest',
        items: firstItems,
      },
      {
        key: 'latest:anonymous-library-b',
        title: 'Latest in Anonymous Original B',
        kind: 'latest',
        items: secondItems,
      },
    ]);
  });

  it.each(latestItemTypeCases)(
    'maps $collectionType libraries to $includeItemTypes for latest requests',
    async ({ collectionType, includeItemTypes }) => {
      const getLatestItems = vi.fn(async () => [anonymousItem('anonymous-latest-item')]);
      const client: HomeCatalogClient = {
        getResumeItems: async () => [],
        getNextUpItems: async () => [],
        getViews: async () => [],
        getLatestItems,
      };

      const rows = await loadLibraryLatestRows(client, [
        anonymousLibrary('anonymous-stable-id', 'Anonymous Renamable Title', collectionType),
      ]);

      expect(getLatestItems).toHaveBeenCalledWith({
        parentId: 'anonymous-stable-id',
        includeItemTypes,
        limit: 24,
      });
      expect(rows[0]).toMatchObject({
        key: 'latest:anonymous-stable-id',
        title: 'Latest in Anonymous Renamable Title',
        kind: 'latest',
      });
    },
  );

  it('skips unknown supplemental types while keeping the library in the My Media core row', async () => {
    const unknownLibrary = anonymousLibrary(
      'anonymous-open-library',
      'Anonymous Open Library',
      'future-open-type',
    );
    const getSupplementalLatestItems = vi.fn(async () => [
      anonymousItem('anonymous-unknown-item'),
    ]);
    const supplementalClient: HomeCatalogClient = {
      getResumeItems: async () => [],
      getNextUpItems: async () => [],
      getViews: async () => [],
      getLatestItems: getSupplementalLatestItems,
    };

    await expect(loadLibraryLatestRows(supplementalClient, [unknownLibrary])).resolves.toEqual([]);
    expect(getSupplementalLatestItems).not.toHaveBeenCalled();

    const coreCatalog = await loadHomeCatalog({
      getResumeItems: async () => [],
      getNextUpItems: async () => [],
      getViews: async () => [unknownLibrary],
      getLatestItems: async () => [],
    });
    expect(coreCatalog.rows).toEqual([
      {
        key: 'libraries',
        title: 'My Media',
        kind: 'libraries',
        items: [unknownLibrary],
      },
    ]);
  });
});

function anonymousItem(id: string): MediaItem {
  return {
    id,
    name: 'Anonymous Item',
    type: 'Video',
    artwork: {},
  };
}

function anonymousLibrary(id: string, name: string, collectionType: string): LibraryView {
  return { id, name, collectionType };
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

interface Deferred<T> {
  promise: Promise<T>;
  resolve(value: T | PromiseLike<T>): void;
  reject(reason?: unknown): void;
}
