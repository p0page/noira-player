import { useMemo, useState } from 'react';
import type { FocusEvent } from 'react';
import type { HomeRow } from '../catalog/homeCatalog';
import {
  getGuideLibraryFocusTarget,
  Guide,
} from '../components/Guide';
import { isLibraryView } from '../components/MediaCard';
import {
  getHomeRowScopeKey,
  MediaRow,
  normalizeHomeRow,
} from '../components/MediaRow';
import {
  createFocusRestoreRequest,
  type FocusRestoreRequest,
} from '../navigation/focusRequests';
import type { FocusTarget } from '../navigation/routes';
import type { LibraryView, MediaItem } from '../types';

export interface HomePageProps {
  busy?: boolean;
  onHome: () => void;
  onLogout: () => void;
  onOpenLibrary: (library: LibraryView, origin: FocusTarget) => void;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
  restoreRequest?: FocusRestoreRequest | null;
  rows: readonly HomeRow[];
}

export function HomePage({
  busy = false,
  onHome,
  onLogout,
  onOpenLibrary,
  onOpenMedia,
  restoreRequest: externalRestoreRequest,
  rows,
}: HomePageProps) {
  const visibleRows = useMemo(() => getVisibleRows(rows), [rows]);
  const libraries = useMemo(() => getLibraries(rows), [rows]);
  const [returnTarget, setReturnTarget] = useState<FocusTarget | null>(null);
  const [preferredRowTargets, setPreferredRowTargets] = useState<
    Readonly<Record<string, string>>
  >({});
  const [guideRestoreRequest, setGuideRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const restoreRequest = guideRestoreRequest ?? externalRestoreRequest ?? null;

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
    const card = event.target.closest<HTMLElement>('.media-card[data-focus-key]');
    if (card) {
      setPreferredRowTargets(resolveNearestRowTargets(card));
    }
    setReturnTarget((current) =>
      current?.focusKey === focusKey && current.scopeKey === scopeKey
        ? current
        : nextTarget,
    );
  }

  function restoreContentFocus(target: FocusTarget) {
    setGuideRestoreRequest(createFocusRestoreRequest(target));
  }

  return (
    <div className="tv-shell">
      <Guide
        activeRoute={{ kind: 'home' }}
        defaultFocus={visibleRows.length === 0}
        libraries={libraries}
        returnTarget={returnTarget}
        onHome={onHome}
        onLibrary={(library) =>
          onOpenLibrary(
            library,
            returnTarget ?? getGuideLibraryFocusTarget(library.id),
          )
        }
        onLogout={onLogout}
        onRestoreFocus={restoreContentFocus}
        restoreRequest={restoreRequest}
      />

      <main
        aria-busy={busy || undefined}
        className="home-page"
        onFocusCapture={handleContentFocus}
      >
        <header className="home-page__header">
          <h1 className="home-page__title">Home</h1>
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
                preferredFocusKey={preferredRowTargets[scopeKey]}
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

function resolveNearestRowTargets(origin: HTMLElement): Readonly<Record<string, string>> {
  const originRect = origin.getBoundingClientRect();
  const originCenterX = originRect.left + originRect.width / 2;
  const targets: Record<string, string> = {};

  for (const scope of document.querySelectorAll<HTMLElement>(
    '[data-focus-scope^="home-row:"]',
  )) {
    const scopeKey = scope.dataset.focusScope;
    if (!scopeKey) {
      continue;
    }

    let nearestKey: string | undefined;
    let nearestDistance = Number.POSITIVE_INFINITY;
    for (const candidate of scope.querySelectorAll<HTMLElement>(
      '.media-card[data-focus-key]',
    )) {
      const candidateKey = candidate.dataset.focusKey;
      if (!candidateKey) {
        continue;
      }

      const candidateRect = candidate.getBoundingClientRect();
      const candidateCenterX = candidateRect.left + candidateRect.width / 2;
      const distance = Math.abs(candidateCenterX - originCenterX);
      if (distance < nearestDistance) {
        nearestDistance = distance;
        nearestKey = candidateKey;
      }
    }

    if (nearestKey) {
      targets[scopeKey] = nearestKey;
    }
  }

  return targets;
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
