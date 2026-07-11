import type { LibraryView, MediaItem, SessionBootstrap } from './types';

const itemFields = 'Overview,RunTimeTicks,MediaSources,UserData,ImageTags';

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

interface EmbyItemsResponse {
  Items?: EmbyItemDto[];
}

interface EmbyItemDto {
  Id?: string;
  Name?: string;
  Type?: string;
  CollectionType?: string;
  Overview?: string;
  RunTimeTicks?: number;
  UserData?: {
    PlaybackPositionTicks?: number;
  };
  MediaSources?: Array<{
    Id?: string;
  }>;
  ImageTags?: {
    Primary?: string;
  };
}

export class EmbyRequestError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'EmbyRequestError';
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

  async getItems(parentId: string): Promise<MediaItem[]> {
    const query = new URLSearchParams({
      ParentId: parentId,
      IncludeItemTypes: 'Movie,Episode,Video',
      Recursive: 'true',
      SortBy: 'SortName',
      SortOrder: 'Ascending',
      Limit: '50',
      Fields: itemFields,
    });
    const response = await this.getJson<EmbyItemsResponse>(
      `Users/${encodeURIComponent(this.session.userId)}/Items?${query}`,
    );

    return (response.Items ?? []).map((item) => this.mapItem(item));
  }

  async getItem(itemId: string): Promise<MediaItem> {
    const query = new URLSearchParams({ Fields: itemFields });
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
      throw new EmbyRequestError(
        response.status,
        `Emby request failed: ${response.status}${statusText}`,
      );
    }

    return (await response.json()) as TResult;
  }

  private mapItem(item: EmbyItemDto): MediaItem {
    const id = item.Id ?? '';
    return {
      id,
      name: item.Name || id || 'Untitled',
      type: item.Type ?? 'Video',
      overview: item.Overview,
      runtimeTicks: item.RunTimeTicks,
      startPositionTicks: item.UserData?.PlaybackPositionTicks,
      mediaSourceId: item.MediaSources?.[0]?.Id,
      imageUrl: item.ImageTags?.Primary ? this.getImageUrl(id, 'Primary', 480) : undefined,
    };
  }

  private createUrl(path: string): string {
    return `${this.serverUrl}/${path.replace(/^\/+/, '')}`;
  }
}
