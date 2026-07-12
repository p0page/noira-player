import { Search } from 'lucide-react';
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { ChangeEvent, KeyboardEvent } from 'react';
import { MediaCard } from '../components/MediaCard';
import {
  useBrowseShellContentRestoreRequest,
  useBrowseShellTextInputEngagement,
} from '../components/BrowseShell';
import {
  useFocusNavigationPolicy,
  useNoiraFocusRegistry,
  useNoiraFocusScope,
} from '../focus/FocusProvider';
import { Focusable, useNoiraFocusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import type { FocusTarget } from '../navigation/routes';
import type { MediaItem } from '../types';

export interface SearchPageClient {
  searchItems(query: string): Promise<readonly MediaItem[]>;
}

export interface SearchPageProps {
  client: SearchPageClient;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
}

type SearchStatus = 'error' | 'idle' | 'loading' | 'ready';

const inputScopeKey = 'search-input-scope';
const inputFocusKey = 'search-input';
const resultsScopeKey = 'search-results';
const retryScopeKey = 'search-recovery';
const retryFocusKey = 'search-retry';

export function SearchPage({ client, onOpenMedia }: SearchPageProps) {
  const restoreRequest = useBrowseShellContentRestoreRequest();
  const [query, setQuery] = useState('');
  const [submittedQuery, setSubmittedQuery] = useState('');
  const [items, setItems] = useState<MediaItem[]>([]);
  const [status, setStatus] = useState<SearchStatus>('idle');
  const requestIdRef = useRef(0);
  const entries = useMemo(() => createEntries(items), [items]);
  const resultKeys = entries.map(({ focusKey }) => focusKey);

  useEffect(
    () => () => {
      requestIdRef.current += 1;
    },
    [],
  );

  async function runSearch(candidate: string) {
    const normalizedQuery = candidate.trim();
    if (!normalizedQuery) {
      return;
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setSubmittedQuery(normalizedQuery);
    setStatus('loading');
    try {
      const result = await client.searchItems(normalizedQuery);
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
  }

  return (
    <main className="library-page search-page" aria-busy={status === 'loading' || undefined}>
      <header className="library-page__header search-page__header">
        <h1>Search</h1>
        <FocusScope
          className="search-page__input-scope"
          defaultFocusKey={inputFocusKey}
          orderedKeys={[inputFocusKey]}
          restoreFocusKey={
            restoreRequest?.target.scopeKey === inputScopeKey
              ? restoreRequest.target.focusKey
              : undefined
          }
          restoreRequestId={
            restoreRequest?.target.scopeKey === inputScopeKey
              ? restoreRequest.requestId
              : undefined
          }
          scopeKey={inputScopeKey}
        >
          <FocusableSearchInput
            focusKey={inputFocusKey}
            onChange={(event) => setQuery(event.currentTarget.value)}
            onSubmit={() => void runSearch(query)}
            value={query}
          />
        </FocusScope>
      </header>

      {entries.length > 0 ? (
        <div className="library-page__grid-shell" data-search-results>
          <FocusScope
            boundaryDirections={['right', 'down']}
            className="library-page__grid search-page__grid"
            defaultFocusKey={resultKeys[0]}
            orderedKeys={resultKeys}
            restoreFocusKey={
              restoreRequest?.target.scopeKey === resultsScopeKey
                ? restoreRequest.target.focusKey
                : undefined
            }
            restoreRequestId={
              restoreRequest?.target.scopeKey === resultsScopeKey
                ? restoreRequest.requestId
                : undefined
            }
            scopeKey={resultsScopeKey}
          >
            {entries.map(({ focusKey, item }) => (
              <div key={focusKey} className="library-page__item">
                <MediaCard
                  focusKey={focusKey}
                  item={item}
                  onSelect={() =>
                    onOpenMedia(item, { focusKey, scopeKey: resultsScopeKey })
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
          Searching...
        </p>
      ) : null}
      {status === 'ready' && entries.length === 0 ? (
        <p className="library-page__status" role="status">
          No results found.
        </p>
      ) : null}
      {status === 'error' ? (
        <div className="library-page__recovery">
          <p className="library-page__status" role="alert">
            Search could not be completed.
          </p>
          <FocusScope
            className="library-page__recovery-scope"
            defaultFocusKey={retryFocusKey}
            orderedKeys={[retryFocusKey]}
            scopeKey={retryScopeKey}
          >
            <Focusable
              aria-label="Retry search"
              className="library-page__retry"
              focusKey={retryFocusKey}
              onSelect={() => void runSearch(submittedQuery)}
            >
              Retry
            </Focusable>
          </FocusScope>
        </div>
      ) : null}
    </main>
  );
}

function FocusableSearchInput({
  focusKey,
  onChange,
  onSubmit,
  value,
}: {
  focusKey: string;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onSubmit: () => void;
  value: string;
}) {
  const policy = useFocusNavigationPolicy();
  const registry = useNoiraFocusRegistry();
  const scope = useNoiraFocusScope();
  const { engageTextInput, releaseTextInput } =
    useBrowseShellTextInputEngagement();
  const ownerRef = useRef<object | null>(null);
  if (ownerRef.current === null) {
    ownerRef.current = {};
  }
  const owner = ownerRef.current;
  const [ownershipReady, setOwnershipReady] = useState(false);
  const [engaged, setEngaged] = useState(false);
  const inputTarget = useMemo(
    () => ({ focusKey, scopeKey: scope.scopeKey }),
    [focusKey, scope.scopeKey],
  );
  const registration = useNoiraFocusable<HTMLInputElement>({
    focusKey,
    onFocus: () => policy.remember(scope.scopeKey, focusKey, scope.orderedKeys),
  });

  useEffect(
    () => () => releaseTextInput(inputTarget),
    [inputTarget, releaseTextInput],
  );

  useLayoutEffect(() => {
    const releaseKey = registry.claimFocusKey(focusKey, owner);
    let releaseChild: (() => void) | null = null;
    try {
      releaseChild = scope.controller.registerChild(focusKey, true);
    } catch (error) {
      releaseKey();
      throw error;
    }

    setOwnershipReady(true);
    return () => {
      releaseChild?.();
      releaseKey();
    };
  }, [focusKey, owner, registry, scope.controller]);

  useEffect(() => {
    if (!ownershipReady) {
      return;
    }

    let cancelled = false;
    queueMicrotask(() => {
      const input = registration.ref.current;
      if (
        !cancelled &&
        input &&
        document.activeElement !== input &&
        registry.canHandoffDomFocus(focusKey, owner)
      ) {
        input.focus();
      }
    });
    return () => {
      cancelled = true;
    };
  }, [focusKey, owner, ownershipReady, registration.ref, registry]);

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter') {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (!engaged) {
      engageTextInput(inputTarget, () => setEngaged(false));
      setEngaged(true);
      return;
    }

    onSubmit();
  }

  return ownershipReady ? (
    <label className="search-page__input-wrap">
      <Search aria-hidden="true" size={24} />
      <input
        ref={registration.ref}
        aria-label="Search media"
        className="search-page__input"
        data-focus-key={registration.focusKey}
        enterKeyHint="search"
        onChange={onChange}
        onKeyDown={handleKeyDown}
        placeholder="Search"
        readOnly={!engaged}
        type="search"
        value={value}
      />
    </label>
  ) : null;
}

export function getSearchInputFocusKey(): string {
  return inputFocusKey;
}

export function getSearchResultsScopeKey(): string {
  return resultsScopeKey;
}

export function getSearchResultsFocusKey(itemId: string): string {
  return `search-card:${encodeURIComponent(itemId.trim())}`;
}

function createEntries(items: readonly MediaItem[]) {
  return items.map((item) => ({
    focusKey: getSearchResultsFocusKey(item.id),
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
