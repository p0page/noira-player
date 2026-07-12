import type {
  ItemPage,
  LatestItemsOptions,
  LibraryItemsOptions,
  LibraryView,
  MediaArtwork,
  MediaItem,
  SessionBootstrap,
} from './types';

const itemFields = [
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
].join(',');
const imageTypes = 'Primary,Backdrop,Thumb,Banner,Logo';
const fallbackLibraryItemTypes =
  'Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,MusicAlbum,Audio,Photo';
const maxCatalogLimit = 100;
const maxStartIndex = 1_000_000;

interface LibraryItemsQueryStrategy {
  includeItemTypes: string;
  mediaTypes?: string;
  isFolder?: boolean;
}

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

interface EmbyItemsResponse {
  Items?: EmbyItemDto[];
  StartIndex?: number | null;
  TotalRecordCount?: number | null;
}

interface EmbyItemDto {
  Id?: string;
  Name?: string;
  Type?: string;
  CollectionType?: string;
  Overview?: string;
  ProductionYear?: number;
  RunTimeTicks?: number;
  SeriesName?: string;
  IndexNumber?: number;
  ParentIndexNumber?: number;
  UserData?: {
    PlaybackPositionTicks?: number;
  };
  MediaSources?: Array<{
    Id?: string;
  }>;
  ImageTags?: {
    Primary?: string;
    Thumb?: string;
    Banner?: string;
  };
  BackdropImageTags?: string[];
  PrimaryImageItemId?: string;
  ParentThumbItemId?: string;
  ParentBannerItemId?: string;
  ParentBackdropItemId?: string;
  ParentThumbImageTag?: string;
}

export class EmbyRequestError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly serverError = '',
  ) {
    super(message);
    this.name = 'EmbyRequestError';
  }
}

export class EmbyProtocolError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'EmbyProtocolError';
  }
}

export class EmbyWebClient {
  private readonly serverUrl: string;
  private readonly fetcher: FetchLike;

  constructor(
    private readonly session: SessionBootstrap,
    fetcher: FetchLike = fetch,
  ) {
    this.serverUrl = session.serverUrl.trim().replace(/\/+$/, '');
    this.fetcher = (input, init) => fetcher(input, init);
  }

  async getViews(): Promise<LibraryView[]> {
    const query = new URLSearchParams({
      Fields: 'PrimaryImageAspectRatio,ImageTags',
    });
    const response = await this.getJson<EmbyItemsResponse>(
      `Users/${encodeURIComponent(this.session.userId)}/Views?${query}`,
    );

    return (response.Items ?? []).map((item) => ({
      id: item.Id ?? '',
      name: item.Name || item.Id || 'Library',
      collectionType: item.CollectionType ?? '',
      imageUrl: item.ImageTags?.Primary
        ? this.getImageUrl(item.Id ?? '', 'Primary', 480)
        : undefined,
    }));
  }

  async getResumeItems(limit = 20): Promise<MediaItem[]> {
    const query = new URLSearchParams({
      IncludeItemTypes: 'Movie,Episode',
      Fields: itemFields,
      Limit: String(normalizeLimit(limit, 20)),
    });
    this.addImageQueryParameters(query);
    const response = await this.getJson<EmbyItemsResponse>(
      `Users/${encodeURIComponent(this.session.userId)}/Items/Resume?${query}`,
    );

    return this.mapItems(response.Items);
  }

  async getNextUpItems(limit: number): Promise<MediaItem[]> {
    const query = new URLSearchParams({
      UserId: this.session.userId,
      Fields: itemFields,
      Limit: String(normalizeLimit(limit, 20)),
    });
    this.addImageQueryParameters(query);
    let response: EmbyItemsResponse;
    try {
      response = await this.getJson<EmbyItemsResponse>(`Shows/NextUp?${query}`);
    } catch (cause) {
      if (isUnsupportedGlobalNextUp(cause)) {
        return [];
      }

      throw cause;
    }

    return this.mapItems(response.Items);
  }

  async getLatestItems(options: LatestItemsOptions = {}): Promise<MediaItem[]> {
    const query = new URLSearchParams();
    if (options.parentId?.trim()) {
      query.set('ParentId', options.parentId);
    }
    query.set('IncludeItemTypes', options.includeItemTypes ?? 'Movie,Series,Episode');
    query.set('Fields', itemFields);
    query.set('Limit', String(normalizeLimit(options.limit ?? 50, 50)));
    this.addImageQueryParameters(query);
    const response = await this.getJson<EmbyItemDto[] | null>(
      `Users/${encodeURIComponent(this.session.userId)}/Items/Latest?${query}`,
    );

    return (response ?? []).map((item) => this.mapItem(item));
  }

  async getItemsPage(
    parentId: string,
    startIndex: number,
    limit: number,
    options: LibraryItemsOptions = {},
  ): Promise<ItemPage> {
    const normalizedStartIndex = normalizeStartIndex(startIndex);
    const strategy = getLibraryItemsQueryStrategy(options);
    const query = new URLSearchParams({
      IncludeItemTypes: strategy.includeItemTypes,
      Recursive: 'true',
      SortBy: 'SortName',
      SortOrder: 'Ascending',
      StartIndex: String(normalizedStartIndex),
      Limit: String(normalizeLimit(limit, 50)),
      Fields: itemFields,
    });
    if (parentId.trim()) {
      query.set('ParentId', parentId.trim());
    }
    if (options.searchTerm?.trim()) {
      query.set('SearchTerm', options.searchTerm.trim());
    }
    if (options.filters?.trim()) {
      query.set('Filters', options.filters.trim());
    }
    if (strategy.mediaTypes) {
      query.set('MediaTypes', strategy.mediaTypes);
    }
    if (strategy.isFolder !== undefined) {
      query.set('IsFolder', strategy.isFolder ? 'true' : 'false');
    }
    this.addImageQueryParameters(query);
    const response = await this.getJson<EmbyItemsResponse>(
      `Users/${encodeURIComponent(this.session.userId)}/Items?${query}`,
    );
    const items = this.mapItems(response.Items);
    const pageStartIndex = resolvePageStartIndex(response.StartIndex, normalizedStartIndex);
    const totalRecordCount = resolveTotalRecordCount(
      response.TotalRecordCount,
      pageStartIndex,
      items.length,
    );

    return {
      items,
      startIndex: pageStartIndex,
      totalRecordCount,
    };
  }

  async searchItems(query: string): Promise<MediaItem[]> {
    return (
      await this.getItemsPage('', 0, maxCatalogLimit, {
        includeItemTypes: fallbackLibraryItemTypes,
        searchTerm: query,
      })
    ).items;
  }

  async getFavoriteItems(): Promise<MediaItem[]> {
    return (
      await this.getItemsPage('', 0, maxCatalogLimit, {
        filters: 'IsFavorite',
        includeItemTypes: fallbackLibraryItemTypes,
      })
    ).items;
  }

  // Temporary compatibility for the pre-Task 8 App.tsx call site.
  async getItems(parentId: string): Promise<MediaItem[]> {
    return (
      await this.getItemsPage(parentId, 0, 50, {
        includeItemTypes: 'Movie,Episode,Video',
      })
    ).items;
  }

  async getItem(itemId: string): Promise<MediaItem> {
    const query = new URLSearchParams({ Fields: itemFields });
    this.addImageQueryParameters(query);
    const response = await this.getJson<EmbyItemDto>(
      `Users/${encodeURIComponent(this.session.userId)}/Items/${encodeURIComponent(itemId)}?${query}`,
    );
    return this.mapItem(response);
  }

  getImageUrl(itemId: string, imageType = 'Primary', maxWidth = 480): string {
    const query = new URLSearchParams({
      maxWidth: String(maxWidth),
      quality: '90',
      api_key: this.session.accessToken,
    });
    return this.createUrl(
      `Items/${encodeURIComponent(itemId)}/Images/${encodeURIComponent(imageType)}?${query}`,
    );
  }

  private addImageQueryParameters(query: URLSearchParams): void {
    query.set('EnableImages', 'true');
    query.set('EnableImageTypes', imageTypes);
    query.set('ImageTypeLimit', '1');
  }

  private async getJson<TResult>(path: string): Promise<TResult> {
    const response = await this.fetcher(this.createUrl(path), {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        Authorization: this.session.authorization,
        'X-Emby-Token': this.session.accessToken,
      },
    });

    if (!response.ok) {
      const statusText = response.statusText ? ` ${response.statusText}` : '';
      const serverError = await readEmbyServerError(response);
      throw new EmbyRequestError(
        response.status,
        `Emby request failed: ${response.status}${statusText}`,
        serverError,
      );
    }

    return (await response.json()) as TResult;
  }

  private mapItems(items: EmbyItemDto[] | undefined): MediaItem[] {
    return (items ?? []).map((item) => this.mapItem(item));
  }

  private mapItem(item: EmbyItemDto): MediaItem {
    const id = item.Id ?? '';
    const artwork: MediaArtwork = {
      primary: this.createArtworkUrl(
        id,
        item.PrimaryImageItemId,
        'Primary',
        item.ImageTags?.Primary,
      ),
      thumb: this.createArtworkUrl(
        id,
        item.ParentThumbItemId,
        'Thumb',
        item.ImageTags?.Thumb || item.ParentThumbImageTag,
      ),
      banner: this.createArtworkUrl(
        id,
        item.ParentBannerItemId,
        'Banner',
        item.ImageTags?.Banner,
      ),
      backdrop: this.createArtworkUrl(
        id,
        item.ParentBackdropItemId,
        'Backdrop',
        item.BackdropImageTags?.[0],
      ),
    };

    return {
      id,
      name: item.Name || id || 'Untitled',
      type: item.Type ?? 'Video',
      productionYear: item.ProductionYear,
      runtimeTicks: item.RunTimeTicks,
      seriesName: item.SeriesName,
      indexNumber: item.IndexNumber,
      parentIndexNumber: item.ParentIndexNumber,
      overview: item.Overview,
      startPositionTicks: item.UserData?.PlaybackPositionTicks,
      mediaSourceId: item.MediaSources?.[0]?.Id,
      artwork,
      // Temporary App.tsx compatibility; Task 8 reads artwork.primary.
      imageUrl: artwork.primary,
    };
  }

  private createArtworkUrl(
    itemId: string,
    imageItemId: string | undefined,
    imageType: string,
    imageTag: string | undefined,
  ): string | undefined {
    if (!imageTag?.trim()) {
      return undefined;
    }

    const resolvedItemId = imageItemId?.trim() ? imageItemId : itemId;
    return resolvedItemId.trim()
      ? this.getImageUrl(resolvedItemId, imageType, 480)
      : undefined;
  }

  private createUrl(path: string): string {
    return `${this.serverUrl}/${path.replace(/^\/+/, '')}`;
  }
}

function isUnsupportedGlobalNextUp(cause: unknown): boolean {
  return (
    cause instanceof EmbyRequestError &&
    cause.status === 400 &&
    cause.serverError.trim().toLowerCase() === 'seriesid is required'
  );
}

async function readEmbyServerError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as unknown;
    if (
      typeof body === 'object' &&
      body !== null &&
      !Array.isArray(body) &&
      typeof (body as Record<string, unknown>).error === 'string'
    ) {
      return (body as Record<string, string>).error.slice(0, 256);
    }
  } catch {
  }

  return '';
}

function getLibraryItemsQueryStrategy(options: LibraryItemsOptions): LibraryItemsQueryStrategy {
  let strategy: LibraryItemsQueryStrategy;
  switch (options.collectionType?.trim().toLowerCase()) {
    case 'movies':
      strategy = { includeItemTypes: 'Movie' };
      break;
    case 'tvshows':
      strategy = { includeItemTypes: 'Series' };
      break;
    case 'boxsets':
      strategy = { includeItemTypes: 'BoxSet', isFolder: false };
      break;
    case 'playlists':
      strategy = { includeItemTypes: 'Playlist', isFolder: false };
      break;
    case 'music':
      strategy = { includeItemTypes: 'MusicAlbum,Audio' };
      break;
    case 'photos':
      strategy = { includeItemTypes: 'Photo', mediaTypes: 'Photo' };
      break;
    case 'homevideos':
      strategy = { includeItemTypes: 'Video' };
      break;
    default:
      strategy = { includeItemTypes: fallbackLibraryItemTypes };
      break;
  }

  const includeItemTypes = options.includeItemTypes?.trim();
  return includeItemTypes ? { ...strategy, includeItemTypes } : strategy;
}

function normalizeLimit(value: number, fallback: number): number {
  return normalizeBoundedInteger(value, 1, maxCatalogLimit, fallback);
}

function normalizeStartIndex(value: number): number {
  return normalizeBoundedInteger(value, 0, maxStartIndex, 0);
}

function normalizeBoundedInteger(
  value: number,
  minimum: number,
  maximum: number,
  fallback: number,
): number {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(maximum, Math.max(minimum, Math.trunc(value)));
}

function resolvePageStartIndex(
  responseStartIndex: number | null | undefined,
  requestedStartIndex: number,
): number {
  if (responseStartIndex === undefined) {
    return requestedStartIndex;
  }

  if (
    typeof responseStartIndex !== 'number' ||
    !Number.isSafeInteger(responseStartIndex) ||
    responseStartIndex < 0 ||
    responseStartIndex > maxStartIndex
  ) {
    throw new EmbyProtocolError(
      `Emby Items response protocol error: StartIndex must be an integer from 0 through ${maxStartIndex}.`,
    );
  }

  return responseStartIndex;
}

function resolveTotalRecordCount(
  totalRecordCount: number | null | undefined,
  startIndex: number,
  returnedItemCount: number,
): number {
  if (
    typeof totalRecordCount !== 'number' ||
    !Number.isSafeInteger(totalRecordCount) ||
    totalRecordCount < 0
  ) {
    throw new EmbyProtocolError(
      'Emby Items response protocol error: TotalRecordCount must be a finite non-negative integer.',
    );
  }

  const minimumTotalRecordCount = startIndex + returnedItemCount;
  if (returnedItemCount > 0 && totalRecordCount < minimumTotalRecordCount) {
    throw new EmbyProtocolError(
      `Emby Items response protocol error: TotalRecordCount ${totalRecordCount} is less than the returned page end ${minimumTotalRecordCount}.`,
    );
  }

  return totalRecordCount;
}
