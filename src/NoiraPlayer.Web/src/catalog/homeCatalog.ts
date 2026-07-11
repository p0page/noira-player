import type { LatestItemsOptions, LibraryView, MediaItem } from '../types';

export type HomeRowKind = 'resume' | 'nextUp' | 'libraries' | 'latest';

export interface HomeRow {
  key: string;
  title: string;
  kind: HomeRowKind;
  items: readonly (MediaItem | LibraryView)[];
}

export interface HomeCatalog {
  rows: HomeRow[];
  failedKinds: HomeRowKind[];
}

export interface LibraryLatestCatalog {
  rows: HomeRow[];
  failedRowKeys: string[];
}

export interface HomeCatalogClient {
  getResumeItems(limit: number): Promise<MediaItem[]>;
  getNextUpItems(limit: number): Promise<MediaItem[]>;
  getViews(): Promise<LibraryView[]>;
  getLatestItems(options?: LatestItemsOptions): Promise<MediaItem[]>;
}

interface CoreRowLoad {
  key: string;
  title: string;
  kind: HomeRowKind;
  request: Promise<readonly (MediaItem | LibraryView)[]>;
}

interface LibraryRowLoad {
  library: LibrarySnapshot;
  request: Promise<MediaItem[]>;
}

interface LibrarySnapshot {
  id: string;
  name: string;
  collectionType: string;
}

const rowLimit = 24;

export async function loadHomeCatalog(client: HomeCatalogClient): Promise<HomeCatalog> {
  const loads: CoreRowLoad[] = [
    {
      key: 'resume',
      title: 'Continue Watching',
      kind: 'resume',
      request: startRequest(() => client.getResumeItems(rowLimit)),
    },
    {
      key: 'nextUp',
      title: 'Next Up',
      kind: 'nextUp',
      request: startRequest(() => client.getNextUpItems(rowLimit)),
    },
    {
      key: 'libraries',
      title: 'My Media',
      kind: 'libraries',
      request: startRequest(() => client.getViews()),
    },
    {
      key: 'latest',
      title: 'Latest',
      kind: 'latest',
      request: startRequest(() => client.getLatestItems({ limit: rowLimit })),
    },
  ];
  const results = await Promise.allSettled(loads.map((load) => load.request));
  const rows: HomeRow[] = [];
  const failedKinds: HomeRowKind[] = [];

  for (let index = 0; index < loads.length; index += 1) {
    const load = loads[index];
    const result = results[index];
    if (result.status === 'rejected') {
      failedKinds.push(load.kind);
    } else if (result.value.length > 0) {
      rows.push({
        key: load.key,
        title: load.title,
        kind: load.kind,
        items: result.value,
      });
    }
  }

  return { rows, failedKinds };
}

export async function loadLibraryLatestRows(
  client: HomeCatalogClient,
  libraries: readonly LibraryView[],
): Promise<HomeRow[]> {
  const catalog = await loadLibraryLatestCatalog(client, libraries);
  return catalog.rows;
}

export async function loadLibraryLatestCatalog(
  client: HomeCatalogClient,
  libraries: readonly LibraryView[],
): Promise<LibraryLatestCatalog> {
  const snapshots: LibrarySnapshot[] = libraries.map(({ id, name, collectionType }) => ({
    id: id.trim(),
    name,
    collectionType,
  }));
  const loads: LibraryRowLoad[] = [];
  const seenIds = new Set<string>();
  for (const library of snapshots) {
    if (!library.id || seenIds.has(library.id)) {
      continue;
    }
    seenIds.add(library.id);
    const includeItemTypes = getLatestItemTypes(library.collectionType);
    if (!includeItemTypes) {
      continue;
    }

    loads.push({
      library,
      request: startRequest(() =>
        client.getLatestItems({
          parentId: library.id,
          includeItemTypes,
          limit: rowLimit,
        }),
      ),
    });
  }

  const results = await Promise.allSettled(loads.map((load) => load.request));
  const rows: HomeRow[] = [];
  const failedRowKeys: string[] = [];
  for (let index = 0; index < loads.length; index += 1) {
    const result = results[index];
    const library = loads[index].library;
    if (result.status === 'rejected') {
      failedRowKeys.push(`latest:${library.id}`);
      continue;
    }
    if (result.value.length === 0) {
      continue;
    }

    rows.push({
      key: `latest:${library.id}`,
      title: `Latest in ${library.name}`,
      kind: 'latest',
      items: result.value,
    });
  }

  return { rows, failedRowKeys };
}

function getLatestItemTypes(collectionType: string): string | undefined {
  switch (collectionType.trim().toLowerCase()) {
    case 'movies':
      return 'Movie';
    case 'tvshows':
      return 'Series';
    case 'boxsets':
      return 'BoxSet';
    case 'playlists':
      return 'Playlist';
    case 'music':
      return 'MusicAlbum,Audio';
    case 'photos':
      return 'Photo';
    case 'homevideos':
      return 'Video';
    default:
      return undefined;
  }
}

function startRequest<T>(request: () => Promise<T>): Promise<T> {
  try {
    return request();
  } catch (error) {
    return Promise.reject(error);
  }
}
