export interface SessionBootstrap {
  serverUrl: string;
  userId: string;
  userName: string;
  accessToken: string;
  authorization: string;
}

export interface BootstrapResult {
  session: SessionBootstrap | null;
}

export interface LibraryView {
  id: string;
  name: string;
  collectionType: string;
  imageUrl?: string;
}

export interface MediaArtwork {
  primary?: string;
  thumb?: string;
  banner?: string;
  backdrop?: string;
}

export interface MediaItem {
  id: string;
  name: string;
  type: string;
  productionYear?: number;
  seriesName?: string;
  indexNumber?: number;
  parentIndexNumber?: number;
  overview?: string;
  runtimeTicks?: number;
  startPositionTicks?: number;
  mediaSourceId?: string;
  artwork: MediaArtwork;
  // Temporary App.tsx compatibility; Task 8 consumes artwork directly.
  imageUrl?: string;
}

export interface ItemPage {
  items: MediaItem[];
  startIndex: number;
  totalRecordCount: number;
}

export interface LibraryItemsOptions {
  collectionType?: string;
  filters?: string;
  includeItemTypes?: string;
  searchTerm?: string;
}

export interface LatestItemsOptions {
  parentId?: string;
  includeItemTypes?: string;
  limit?: number;
}
