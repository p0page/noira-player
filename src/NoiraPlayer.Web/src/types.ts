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

export interface MediaItem {
  id: string;
  name: string;
  type: string;
  overview?: string;
  imageUrl?: string;
  startPositionTicks?: number;
  mediaSourceId?: string;
  runtimeTicks?: number;
}
