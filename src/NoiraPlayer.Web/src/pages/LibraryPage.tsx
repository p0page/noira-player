import { useEffect, useMemo, useRef, useState } from 'react';
import type { FocusEvent } from 'react';
import { MediaCard } from '../components/MediaCard';
import {
  useBrowseShellContentRestoreRequest,
  useBrowseShellGuideInteractionVersion,
} from '../components/BrowseShell';
import { EmbyRequestError, type EmbyWebClient } from '../emby';
import { Focusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import {
  createFocusRestoreRequest,
  type FocusRestoreRequest,
} from '../navigation/focusRequests';
import type { FocusTarget } from '../navigation/routes';
import type { ItemPage, LibraryView, MediaItem } from '../types';

export type LibraryPageClient = Pick<EmbyWebClient, 'getItemsPage'>;

export interface LibraryPageProps {
  busy?: boolean;
  client: LibraryPageClient;
  library: LibraryView;
  onAuthenticationRequired: () => void;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
  restoreRequest?: FocusRestoreRequest | null;
}

interface LibraryLoadContext {
  active: boolean;
  client: LibraryPageClient;
  collectionType: string;
  exhausted: boolean;
  failedStart: number | null;
  identity: string;
  guideInteractionVersion: number;
  inFlightStart: number | null;
  libraryId: string;
  nextStart: number;
  started: boolean;
}

interface LibraryEntry {
  focusKey: string;
  item: MediaItem;
}

interface ExternalRestoreCompletion {
  committedItemCount: number;
  requestId: string;
}

type LoadStatus = 'error' | 'loading' | 'ready';

const libraryPageSize = 50;

export function LibraryPage({
  busy = false,
  client,
  library,
  onAuthenticationRequired,
  onOpenMedia,
  restoreRequest: externalRestoreRequest,
}: LibraryPageProps) {
  const guideInteractionVersion = useBrowseShellGuideInteractionVersion();
  const shellRestoreRequest = useBrowseShellContentRestoreRequest();
  const guideInteractionVersionRef = useRef(guideInteractionVersion);
  guideInteractionVersionRef.current = guideInteractionVersion;
  const identity = createLibraryIdentity(library);
  const [renderedIdentity, setRenderedIdentity] = useState('');
  const [items, setItems] = useState<MediaItem[]>([]);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('loading');
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [externalRestoreCompletion, setExternalRestoreCompletion] =
    useState<ExternalRestoreCompletion | null>(null);
  const [releasedExternalRestoreRequestId, setReleasedExternalRestoreRequestId] =
    useState<string | null>(null);
  const [
    cancelledExternalRestoreRequestId,
    setCancelledExternalRestoreRequestId,
  ] = useState<string | null>(null);
  const [returnTarget, setReturnTarget] = useState<FocusTarget | null>(null);
  const [guideRestoreRequest, setGuideRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const [initialRestoreRequest, setInitialRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const contextRef = useRef<LibraryLoadContext | null>(null);
  const itemsRef = useRef<readonly MediaItem[]>([]);
  const cancelledExternalRestoreRequestIdRef = useRef<string | null>(null);
  const gridRef = useRef<HTMLDivElement | null>(null);
  const visibleItems = renderedIdentity === identity ? items : [];
  const entries = useMemo(
    () => createLibraryEntries(library.id, visibleItems),
    [library.id, visibleItems],
  );
  const scopeKey = getLibraryGridScopeKey(library.id);
  const recoveryScopeKey = getLibraryRecoveryScopeKey(library.id);
  const retryFocusKey = getLibraryRetryFocusKey(library.id);
  const orderedKeys = entries.map(({ focusKey }) => focusKey);
  const externalRestoreIsReleased =
    externalRestoreRequest !== null &&
    externalRestoreRequest !== undefined &&
    releasedExternalRestoreRequestId === externalRestoreRequest.requestId;
  const externalRestoreIsCancelled =
    externalRestoreRequest !== null &&
    externalRestoreRequest !== undefined &&
    cancelledExternalRestoreRequestId === externalRestoreRequest.requestId;
  const defersExternalRestore =
    externalRestoreRequest?.target.scopeKey === scopeKey &&
    !externalRestoreIsReleased &&
    !externalRestoreIsCancelled;
  const restoreRequest =
    guideRestoreRequest ??
    shellRestoreRequest ??
    (externalRestoreIsCancelled
      ? initialRestoreRequest
      : defersExternalRestore
      ? null
      : externalRestoreRequest ?? initialRestoreRequest);
  const currentLoadSettled =
    renderedIdentity === identity && loadStatus !== 'loading';

  useEffect(() => {
    if (
      externalRestoreRequest &&
      cancelledExternalRestoreRequestIdRef.current !==
        externalRestoreRequest.requestId &&
      externalRestoreCompletion?.requestId ===
        externalRestoreRequest.requestId &&
      entries.length >= externalRestoreCompletion.committedItemCount
    ) {
      setReleasedExternalRestoreRequestId(externalRestoreRequest.requestId);
    }
  }, [entries.length, externalRestoreCompletion, externalRestoreRequest]);

  useEffect(() => {
    const previous = contextRef.current;
    const reusesContext =
      previous !== null &&
      previous.client === client &&
      previous.identity === identity;
    const context = reusesContext
      ? previous
      : {
          active: true,
          client,
          collectionType: library.collectionType,
          exhausted: false,
          failedStart: null,
          guideInteractionVersion,
          identity,
          inFlightStart: null,
          libraryId: library.id,
          nextStart: 0,
          started: false,
        };

    if (!reusesContext) {
      if (previous) {
        previous.active = false;
      }
      contextRef.current = context;
      itemsRef.current = [];
      cancelledExternalRestoreRequestIdRef.current = null;
      setRenderedIdentity(identity);
      setItems([]);
      setLoadStatus('loading');
      setLoadingMore(false);
      setLoadError('');
      setExternalRestoreCompletion(null);
      setReleasedExternalRestoreRequestId(null);
      setCancelledExternalRestoreRequestId(null);
      setReturnTarget(null);
      setGuideRestoreRequest(null);
      setInitialRestoreRequest(null);
    }

    context.active = true;
    if (!context.started) {
      context.started = true;
      void requestPage(context, 0);
    }

    return () => {
      context.active = false;
    };
  }, [client, identity, library.collectionType, library.id]);

  async function requestPage(
    context: LibraryLoadContext,
    startIndex: number,
  ): Promise<void> {
    if (
      !isCurrentContext(context) ||
      context.exhausted ||
      context.inFlightStart !== null
    ) {
      return;
    }

    context.inFlightStart = startIndex;
    if (startIndex === 0) {
      setLoadStatus('loading');
    } else {
      setLoadingMore(true);
    }

    let continueWithoutFocus = false;
    try {
      const page = await context.client.getItemsPage(
        context.libraryId,
        startIndex,
        libraryPageSize,
        { collectionType: context.collectionType },
      );
      if (!isCurrentContext(context)) {
        return;
      }

      const previousItems = itemsRef.current;
      const mergedItems = appendUniqueItems(previousItems, page.items);
      const addedItemCount = mergedItems.length - previousItems.length;
      itemsRef.current = mergedItems;
      setItems(mergedItems);

      const returnedItemCount = page.items.length;
      const pageStartIndex = normalizePageStart(page, startIndex);
      const nextStart = pageStartIndex + returnedItemCount;
      const totalRecordCount = normalizeTotalRecordCount(
        page.totalRecordCount,
        nextStart,
      );
      context.nextStart = nextStart;
      context.exhausted =
        returnedItemCount === 0 ||
        nextStart <= startIndex ||
        nextStart >= totalRecordCount;
      context.failedStart = null;
      setLoadStatus('ready');
      setLoadError('');

      const pendingExternalRestoreRequest = externalRestoreRequest;
      const guideWasUsed =
        guideInteractionVersionRef.current !== context.guideInteractionVersion;
      if (guideWasUsed) {
        if (pendingExternalRestoreRequest) {
          cancelledExternalRestoreRequestIdRef.current =
            pendingExternalRestoreRequest.requestId;
          setCancelledExternalRestoreRequestId(
            pendingExternalRestoreRequest.requestId,
          );
        }
      }
      const externalTargetFound = pendingExternalRestoreRequest
        ? mergedItems.some(
            ({ id }) =>
              getLibraryGridFocusKey(context.libraryId, id) ===
              pendingExternalRestoreRequest.target.focusKey,
          )
        : false;
      if (
        pendingExternalRestoreRequest &&
        cancelledExternalRestoreRequestIdRef.current !==
          pendingExternalRestoreRequest.requestId &&
        externalRestoreTargetsContext(context) &&
        (externalTargetFound || context.exhausted)
      ) {
        setExternalRestoreCompletion({
          committedItemCount: mergedItems.length,
          requestId: pendingExternalRestoreRequest.requestId,
        });
      }

      const searchesForExternalTarget = isSearchingForExternalTarget(
        context,
        mergedItems,
      );
      const cancelledExternalSearch =
        cancelledExternalRestoreRequestIdRef.current ===
        externalRestoreRequest?.requestId;
      if (
        previousItems.length === 0 &&
        mergedItems.length > 0 &&
        !guideWasUsed &&
        !externalRestoreTargetsContext(context)
      ) {
        setInitialRestoreRequest(
          createFocusRestoreRequest({
            scopeKey: getLibraryGridScopeKey(context.libraryId),
            focusKey: getLibraryGridFocusKey(
              context.libraryId,
              mergedItems[0].id,
            ),
          }),
        );
      }

      continueWithoutFocus =
        !context.exhausted &&
        (searchesForExternalTarget ||
          (addedItemCount === 0 && !cancelledExternalSearch));
    } catch (cause) {
      if (!isCurrentContext(context)) {
        return;
      }

      if (
        cause instanceof EmbyRequestError &&
        (cause.status === 401 || cause.status === 403)
      ) {
        onAuthenticationRequired();
        return;
      }

      context.failedStart = startIndex;
      setLoadStatus('error');
      setLoadError('Unable to load this library from Emby.');
    } finally {
      if (context.inFlightStart === startIndex) {
        context.inFlightStart = null;
      }
      if (isCurrentContext(context)) {
        setLoadingMore(false);
      }
    }

    if (continueWithoutFocus && isCurrentContext(context)) {
      queueMicrotask(() => {
        if (isCurrentContext(context)) {
          void requestPage(context, context.nextStart);
        }
      });
    }
  }

  function isCurrentContext(context: LibraryLoadContext): boolean {
    return context.active && contextRef.current === context;
  }

  function externalRestoreTargetsContext(
    context: LibraryLoadContext,
  ): boolean {
    return (
      externalRestoreRequest?.target.scopeKey ===
      getLibraryGridScopeKey(context.libraryId)
    );
  }

  function isSearchingForExternalTarget(
    context: LibraryLoadContext,
    availableItems: readonly MediaItem[],
  ): boolean {
    if (
      !externalRestoreTargetsContext(context) ||
      context.exhausted ||
      cancelledExternalRestoreRequestIdRef.current ===
        externalRestoreRequest?.requestId
    ) {
      return false;
    }

    const targetFocusKey = externalRestoreRequest?.target.focusKey;
    return (
      targetFocusKey !== undefined &&
      !availableItems.some(
        ({ id }) =>
          getLibraryGridFocusKey(context.libraryId, id) === targetFocusKey,
      )
    );
  }

  function requestNextPage() {
    const context = contextRef.current;
    if (
      !context ||
      !isCurrentContext(context) ||
      context.failedStart !== null
    ) {
      return;
    }

    void requestPage(context, context.nextStart);
  }

  function retryFailedPage() {
    const context = contextRef.current;
    if (
      !context ||
      !isCurrentContext(context) ||
      context.failedStart === null ||
      context.inFlightStart !== null
    ) {
      return;
    }

    const failedStart = context.failedStart;
    context.failedStart = null;
    if (failedStart > 0 && returnTarget) {
      setGuideRestoreRequest(createFocusRestoreRequest(returnTarget));
    }
    setLoadError('');
    void requestPage(context, failedStart);
  }

  function handleContentFocus(event: FocusEvent<HTMLElement>) {
    if (!(event.target instanceof HTMLElement)) {
      return;
    }

    const card = event.target.closest<HTMLElement>('.media-card[data-focus-key]');
    const itemContainer = card?.closest<HTMLElement>('[data-library-grid-index]');
    const focusKey = card?.dataset.focusKey;
    const itemIndex = Number(itemContainer?.dataset.libraryGridIndex);
    if (!card || !focusKey || !Number.isSafeInteger(itemIndex) || itemIndex < 0) {
      return;
    }

    const target = { focusKey, scopeKey };
    setReturnTarget((current) =>
      current?.focusKey === focusKey && current.scopeKey === scopeKey
        ? current
        : target,
    );
    card.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });

    const grid = gridRef.current;
    if (
      grid &&
      isInFinalVisualRows(itemIndex, entries.length, getVisualColumnCount(grid))
    ) {
      requestNextPage();
    }
  }

  return (
    <main
      aria-busy={busy || loadStatus === 'loading' || loadingMore || undefined}
      className="library-page"
      onFocusCapture={handleContentFocus}
    >
        <header className="library-page__header">
          <h1>{library.name}</h1>
        </header>

        {entries.length > 0 ? (
          <div ref={gridRef} className="library-page__grid-shell" data-library-grid>
            <FocusScope
              key={scopeKey}
              boundaryDirections={loadError ? ['right'] : ['right', 'down']}
              className="library-page__grid"
              defaultFocusKey={orderedKeys[0]}
              orderedKeys={orderedKeys}
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
              scopeKey={scopeKey}
            >
              {entries.map(({ focusKey, item }, index) => (
                <div
                  key={focusKey}
                  className="library-page__item"
                  data-library-grid-index={index}
                >
                  <MediaCard
                    focusKey={focusKey}
                    item={item}
                    onSelect={() =>
                      onOpenMedia(item, { focusKey, scopeKey })
                    }
                    variant="poster"
                  />
                </div>
              ))}
            </FocusScope>
          </div>
        ) : null}

        {loadStatus === 'loading' && renderedIdentity === identity ? (
          <p className="library-page__status" role="status">
            Loading library...
          </p>
        ) : null}
        {loadError && renderedIdentity === identity ? (
          <div className="library-page__recovery">
            <p className="library-page__status" role="alert">
              {loadError}
            </p>
            <FocusScope
              className="library-page__recovery-scope"
              defaultFocusKey={retryFocusKey}
              orderedKeys={[retryFocusKey]}
              scopeKey={recoveryScopeKey}
            >
              <Focusable
                aria-label="Retry loading library"
                className="library-page__retry"
                focusKey={retryFocusKey}
                onSelect={retryFailedPage}
              >
                Retry
              </Focusable>
            </FocusScope>
          </div>
        ) : null}
        {currentLoadSettled && entries.length === 0 && !loadError ? (
          <p className="library-page__status" role="status">
            No media available in {library.name}.
          </p>
        ) : null}
        {loadingMore ? (
          <p className="library-page__loading-more" aria-live="polite">
            Loading more...
          </p>
        ) : null}
    </main>
  );
}

export function getLibraryGridScopeKey(libraryId: string): string {
  return `library-grid:${encodeFocusSegment(libraryId)}`;
}

export function getLibraryGridFocusKey(
  libraryId: string,
  itemId: string,
): string {
  return [
    'library-card',
    encodeFocusSegment(libraryId),
    'media',
    encodeFocusSegment(itemId),
  ].join(':');
}

function createLibraryEntries(
  libraryId: string,
  items: readonly MediaItem[],
): LibraryEntry[] {
  return items.map((item) => ({
    focusKey: getLibraryGridFocusKey(libraryId, item.id),
    item,
  }));
}

function appendUniqueItems(
  existing: readonly MediaItem[],
  incoming: readonly MediaItem[],
): MediaItem[] {
  const result = [...existing];
  const seenIds = new Set(existing.map(({ id }) => id));
  for (const item of incoming) {
    const id = item.id.trim();
    if (!id || seenIds.has(id)) {
      continue;
    }

    seenIds.add(id);
    result.push({ ...item, id });
  }

  return result;
}

function normalizePageStart(page: ItemPage, requestedStart: number): number {
  return Number.isSafeInteger(page.startIndex) && page.startIndex >= 0
    ? page.startIndex
    : requestedStart;
}

function normalizeTotalRecordCount(total: number, minimum: number): number {
  return Number.isSafeInteger(total) && total >= 0
    ? Math.max(total, minimum)
    : minimum;
}

function isInFinalVisualRows(
  itemIndex: number,
  itemCount: number,
  columnCount: number,
): boolean {
  const columns = Math.max(1, columnCount);
  const rowCount = Math.ceil(itemCount / columns);
  const itemRow = Math.floor(itemIndex / columns);
  return itemRow >= Math.max(0, rowCount - 2);
}

function getVisualColumnCount(grid: HTMLElement): number {
  const cards = Array.from(
    grid.querySelectorAll<HTMLElement>(
      '[data-library-grid-index] .media-card[data-focus-key]',
    ),
  );
  const firstRect = cards[0]?.getBoundingClientRect();
  if (firstRect && firstRect.width > 0 && firstRect.height > 0) {
    const rowTolerance = Math.max(2, firstRect.height * 0.05);
    let columns = 0;
    for (const card of cards) {
      const rect = card.getBoundingClientRect();
      if (
        rect.width <= 0 ||
        rect.height <= 0 ||
        Math.abs(rect.top - firstRect.top) > rowTolerance
      ) {
        break;
      }
      columns += 1;
    }

    if (columns > 0) {
      return columns;
    }
  }

  const styles = getComputedStyle(grid);
  const gridWidth = grid.getBoundingClientRect().width;
  const cardWidth = parseCssPixels(
    styles.getPropertyValue('--library-grid-card-width'),
  );
  const columnGap = parseCssPixels(
    styles.getPropertyValue('--library-grid-column-gap'),
  );
  if (gridWidth > 0 && cardWidth > 0) {
    return Math.max(1, Math.floor((gridWidth + columnGap) / (cardWidth + columnGap)));
  }

  return 1;
}

function parseCssPixels(value: string): number {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
}

function createLibraryIdentity(library: LibraryView): string {
  return `${library.id.trim()}\u0000${library.collectionType.trim()}`;
}

function getLibraryRecoveryScopeKey(libraryId: string): string {
  return `library-recovery:${encodeFocusSegment(libraryId)}`;
}

function getLibraryRetryFocusKey(libraryId: string): string {
  return `library-retry:${encodeFocusSegment(libraryId)}`;
}

function encodeFocusSegment(value: string): string {
  return encodeURIComponent(value.trim() || 'anonymous');
}
