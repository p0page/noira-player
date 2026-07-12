import { useMemo, useState } from 'react';
import type { FocusEvent } from 'react';
import type { HomeRow } from '../catalog/homeCatalog';
import { useBrowseShellContentRestoreRequest } from '../components/BrowseShell';
import {
  getHomeRowScopeKey,
  MediaRow,
  normalizeHomeRow,
} from '../components/MediaRow';
import type { FocusRestoreRequest } from '../navigation/focusRequests';
import type { FocusTarget } from '../navigation/routes';
import type { LibraryView, MediaItem } from '../types';

export interface HomePageProps {
  busy?: boolean;
  onOpenLibrary: (library: LibraryView, origin: FocusTarget) => void;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
  restoreRequest?: FocusRestoreRequest | null;
  rows: readonly HomeRow[];
}

export function HomePage({
  busy = false,
  onOpenLibrary,
  onOpenMedia,
  restoreRequest,
  rows,
}: HomePageProps) {
  const shellRestoreRequest = useBrowseShellContentRestoreRequest();
  const effectiveRestoreRequest = shellRestoreRequest ?? restoreRequest;
  const visibleRows = useMemo(() => getVisibleRows(rows), [rows]);
  const [preferredRowTargets, setPreferredRowTargets] = useState<
    Readonly<Record<string, string>>
  >({});

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

    const card = event.target.closest<HTMLElement>('.media-card[data-focus-key]');
    if (card) {
      setPreferredRowTargets(resolveNearestRowTargets(card));
    }
  }

  return (
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
                  effectiveRestoreRequest?.target.scopeKey === scopeKey
                    ? effectiveRestoreRequest.target.focusKey
                    : undefined
                }
                restoreRequestId={
                  effectiveRestoreRequest?.target.scopeKey === scopeKey
                    ? effectiveRestoreRequest.requestId
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
