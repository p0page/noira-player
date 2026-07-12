import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { MediaCard } from '../components/MediaCard';
import { useBrowseShellContentRestoreRequest } from '../components/BrowseShell';
import { Focusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import type { FocusTarget } from '../navigation/routes';
import type { MediaItem } from '../types';

export interface FavoritesPageClient {
  getFavoriteItems(): Promise<readonly MediaItem[]>;
}

export interface FavoritesPageProps {
  client: FavoritesPageClient;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
}

type FavoritesStatus = 'error' | 'loading' | 'ready';

const favoritesScopeKey = 'favorites-results';
const recoveryScopeKey = 'favorites-recovery';
const retryFocusKey = 'favorites-retry';

export function FavoritesPage({ client, onOpenMedia }: FavoritesPageProps) {
  const restoreRequest = useBrowseShellContentRestoreRequest();
  const [items, setItems] = useState<MediaItem[]>([]);
  const [status, setStatus] = useState<FavoritesStatus>('loading');
  const requestIdRef = useRef(0);
  const entries = useMemo(() => createEntries(items), [items]);
  const orderedKeys = entries.map(({ focusKey }) => focusKey);

  const loadFavorites = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setStatus('loading');
    try {
      const result = await client.getFavoriteItems();
      if (requestIdRef.current !== requestId) {
        return;
      }

      setItems(normalizeItems(result));
      setStatus('ready');
    } catch {
      if (requestIdRef.current !== requestId) {
        return;
      }

      setItems([]);
      setStatus('error');
    }
  }, [client]);

  useEffect(() => {
    void loadFavorites();
    return () => {
      requestIdRef.current += 1;
    };
  }, [loadFavorites]);

  return (
    <main
      className="library-page favorites-page"
      aria-busy={status === 'loading' || undefined}
    >
      <header className="library-page__header">
        <h1>Favorites</h1>
      </header>

      {entries.length > 0 ? (
        <div className="library-page__grid-shell" data-favorites-grid>
          <FocusScope
            boundaryDirections={['right', 'down']}
            className="library-page__grid favorites-page__grid"
            defaultFocusKey={orderedKeys[0]}
            orderedKeys={orderedKeys}
            restoreFocusKey={
              restoreRequest?.target.scopeKey === favoritesScopeKey
                ? restoreRequest.target.focusKey
                : undefined
            }
            restoreRequestId={
              restoreRequest?.target.scopeKey === favoritesScopeKey
                ? restoreRequest.requestId
                : undefined
            }
            scopeKey={favoritesScopeKey}
          >
            {entries.map(({ focusKey, item }) => (
              <div key={focusKey} className="library-page__item">
                <MediaCard
                  focusKey={focusKey}
                  item={item}
                  onSelect={() =>
                    onOpenMedia(item, { focusKey, scopeKey: favoritesScopeKey })
                  }
                  variant="poster"
                />
              </div>
            ))}
          </FocusScope>
        </div>
      ) : null}

      {status === 'loading' ? (
        <p className="library-page__status" role="status">
          Loading favorites...
        </p>
      ) : null}
      {status === 'ready' && entries.length === 0 ? (
        <p className="library-page__status" role="status">
          No favorites yet.
        </p>
      ) : null}
      {status === 'error' ? (
        <div className="library-page__recovery">
          <p className="library-page__status" role="alert">
            Favorites could not be loaded.
          </p>
          <FocusScope
            className="library-page__recovery-scope"
            defaultFocusKey={retryFocusKey}
            orderedKeys={[retryFocusKey]}
            scopeKey={recoveryScopeKey}
          >
            <Focusable
              aria-label="Retry favorites"
              className="library-page__retry"
              focusKey={retryFocusKey}
              onSelect={() => void loadFavorites()}
            >
              Retry
            </Focusable>
          </FocusScope>
        </div>
      ) : null}
    </main>
  );
}

export function getFavoritesScopeKey(): string {
  return favoritesScopeKey;
}

export function getFavoritesFocusKey(itemId: string): string {
  return `favorite-card:${encodeURIComponent(itemId.trim())}`;
}

function createEntries(items: readonly MediaItem[]) {
  return items.map((item) => ({
    focusKey: getFavoritesFocusKey(item.id),
    item,
  }));
}

function normalizeItems(items: readonly MediaItem[]): MediaItem[] {
  const normalized: MediaItem[] = [];
  const seenIds = new Set<string>();
  for (const item of items) {
    const id = item.id.trim();
    if (!id || seenIds.has(id)) {
      continue;
    }

    seenIds.add(id);
    normalized.push(id === item.id ? item : { ...item, id });
  }
  return normalized;
}
