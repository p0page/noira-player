import { useMemo, useState } from 'react';
import type { FocusEvent } from 'react';
import type { HomeRow } from '../catalog/homeCatalog';
import { Guide } from '../components/Guide';
import { isLibraryView } from '../components/MediaCard';
import {
  getHomeRowScopeKey,
  MediaRow,
  normalizeHomeRow,
} from '../components/MediaRow';
import type { FocusTarget } from '../navigation/routes';
import type { LibraryView, MediaItem } from '../types';

export interface HomePageProps {
  busy?: boolean;
  onHome: () => void;
  onLogout: () => void;
  onOpenLibrary: (library: LibraryView) => void;
  onOpenMedia: (item: MediaItem) => void;
  rows: readonly HomeRow[];
}

interface RestoreRequest {
  requestId: string;
  target: FocusTarget;
}

const restoreRequestFallbackTimestamp = Date.now().toString(36);
let restoreRequestFallbackCounter = 0;

export function HomePage({
  busy = false,
  onHome,
  onLogout,
  onOpenLibrary,
  onOpenMedia,
  rows,
}: HomePageProps) {
  const visibleRows = useMemo(() => getVisibleRows(rows), [rows]);
  const libraries = useMemo(() => getLibraries(rows), [rows]);
  const [returnTarget, setReturnTarget] = useState<FocusTarget | null>(null);
  const [restoreRequest, setRestoreRequest] = useState<RestoreRequest | null>(null);

  function handleContentFocus(event: FocusEvent<HTMLElement>) {
    if (!(event.target instanceof HTMLElement)) {
      return;
    }

    const focusKey = event.target.dataset.focusKey;
    const scope = event.target.closest<HTMLElement>('[data-focus-scope]');
    const scopeKey = scope?.dataset.focusScope;
    if (!focusKey || !scopeKey || scopeKey === 'home-guide') {
      return;
    }

    const nextTarget = { focusKey, scopeKey };
    setReturnTarget((current) =>
      current?.focusKey === focusKey && current.scopeKey === scopeKey
        ? current
        : nextTarget,
    );
  }

  function restoreContentFocus(target: FocusTarget) {
    setRestoreRequest({
      requestId: createRestoreRequestId(),
      target,
    });
  }

  return (
    <div className="tv-shell">
      <Guide
        activeRoute={{ kind: 'home' }}
        defaultFocus={visibleRows.length === 0}
        libraries={libraries}
        returnTarget={returnTarget}
        onHome={onHome}
        onLibrary={onOpenLibrary}
        onLogout={onLogout}
        onRestoreFocus={restoreContentFocus}
      />

      <main
        aria-busy={busy || undefined}
        className="home-page"
        onFocusCapture={handleContentFocus}
      >
        <header className="home-page__header">
          <h1>Home</h1>
        </header>

        <div className="home-page__rows">
          {visibleRows.map((row, index) => {
            const scopeKey = getHomeRowScopeKey(row.key);
            return (
              <MediaRow
                key={row.key}
                defaultFocus={index === 0}
                onOpenLibrary={onOpenLibrary}
                onOpenMedia={onOpenMedia}
                restoreFocusKey={
                  restoreRequest?.target.scopeKey === scopeKey
                    ? restoreRequest.target.focusKey
                    : undefined
                }
                restoreRequestId={
                  restoreRequest?.target.scopeKey === scopeKey
                    ? restoreRequest.requestId
                    : undefined
                }
                row={row}
              />
            );
          })}
        </div>

        {visibleRows.length === 0 ? (
          <p className="home-page__empty" role="status">
            No media available.
          </p>
        ) : null}
      </main>
    </div>
  );
}

function getVisibleRows(rows: readonly HomeRow[]): HomeRow[] {
  const seenKeys = new Set<string>();
  const visibleRows: HomeRow[] = [];

  for (const row of rows) {
    const normalizedRow = normalizeHomeRow(row);
    if (
      !normalizedRow ||
      normalizedRow.items.length === 0 ||
      seenKeys.has(normalizedRow.key)
    ) {
      continue;
    }

    seenKeys.add(normalizedRow.key);
    visibleRows.push(normalizedRow);
  }

  return visibleRows;
}

function getLibraries(rows: readonly HomeRow[]): LibraryView[] {
  const libraries: LibraryView[] = [];

  for (const row of rows) {
    if (row.kind !== 'libraries') {
      continue;
    }

    for (const item of row.items) {
      if (!isLibraryView(item)) {
        continue;
      }

      libraries.push(item);
    }
  }

  return libraries;
}

function createRestoreRequestId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  restoreRequestFallbackCounter += 1;
  return `${restoreRequestFallbackTimestamp}:${restoreRequestFallbackCounter.toString(36)}`;
}
