import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { requestBridge } from './bridge';
import {
  loadHomeCatalog,
  loadLibraryLatestRows,
  type HomeCatalog,
  type HomeRow,
  type HomeRowKind,
} from './catalog/homeCatalog';
import { EmbyRequestError, EmbyWebClient } from './emby';
import { useFocusNavigationPolicy } from './focus/FocusProvider';
import { HomePage } from './pages/HomePage';
import { createEmbyFetchTransport } from './transport';
import type {
  BootstrapResult,
  LibraryView,
  MediaItem,
  SessionBootstrap,
} from './types';

type View = 'loading' | 'login' | 'home' | 'items' | 'details';
type DetailsOrigin = 'home' | 'items';

const coreRowOrder: readonly { key: string; kind: HomeRowKind }[] = [
  { key: 'resume', kind: 'resume' },
  { key: 'nextUp', kind: 'nextUp' },
  { key: 'libraries', kind: 'libraries' },
  { key: 'latest', kind: 'latest' },
];

export function App() {
  const focusPolicy = useFocusNavigationPolicy();
  const [view, setView] = useState<View>('loading');
  const [client, setClient] = useState<EmbyWebClient | null>(null);
  const [homeRows, setHomeRows] = useState<HomeRow[]>([]);
  const [items, setItems] = useState<MediaItem[]>([]);
  const [activeLibrary, setActiveLibrary] = useState<LibraryView | null>(null);
  const [selectedItem, setSelectedItem] = useState<MediaItem | null>(null);
  const [detailsOrigin, setDetailsOrigin] = useState<DetailsOrigin>('home');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const bootstrapStartedRef = useRef(false);
  const mountedRef = useRef(false);
  const operationGenerationRef = useRef(0);
  const logoutPendingRef = useRef(false);
  const homeRowsRef = useRef<readonly HomeRow[]>([]);

  useEffect(() => {
    mountedRef.current = true;
    if (!bootstrapStartedRef.current) {
      bootstrapStartedRef.current = true;
      void bootstrap();
    }

    return () => {
      mountedRef.current = false;
    };
  }, []);

  async function bootstrap() {
    const generation = beginOperation();
    setBusy(true);
    setError('');
    setView('loading');

    try {
      const result = await requestBridge<BootstrapResult>('auth.bootstrap');
      if (!isCurrentOperation(generation)) {
        return;
      }

      if (!result.session) {
        resetAuthenticatedState();
        setView('login');
        return;
      }

      const nextClient = createClient(result.session);
      setClient(nextClient);
      await loadHome(nextClient, generation);
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const generation = beginOperation();
    setBusy(true);
    setError('');

    try {
      const result = await requestBridge<BootstrapResult>(
        'auth.login',
        {
          serverUrl: String(data.get('serverUrl') || ''),
          username: String(data.get('username') || ''),
          password: String(data.get('password') || ''),
        },
        { timeoutMs: 45000 },
      );
      if (!isCurrentOperation(generation)) {
        return;
      }
      if (!result.session) {
        throw new Error('Native login completed without a saved Emby session.');
      }

      const nextClient = createClient(result.session);
      setClient(nextClient);
      await loadHome(nextClient, generation);
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function loadHome(nextClient: EmbyWebClient, generation: number) {
    const catalog = await loadHomeCatalog(nextClient);
    if (!isCurrentOperation(generation)) {
      return;
    }

    const coreRows = resolveCoreRows(catalog, homeRowsRef.current);
    const retainedSupplementalRows = homeRowsRef.current.filter(isSupplementalRow);
    replaceHomeRows(mergeRows(coreRows, retainedSupplementalRows));
    setItems([]);
    setActiveLibrary(null);
    setSelectedItem(null);
    setView('home');
    setError(describeCatalogFailures(catalog));
    setBusy(false);

    const supplementalRows = await loadLibraryLatestRows(
      nextClient,
      extractLibraries(coreRows),
    );
    if (!isCurrentOperation(generation)) {
      return;
    }

    replaceHomeRows(mergeRows(coreRows, supplementalRows));
  }

  async function reloadHome() {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setView('login');
      return;
    }

    const generation = beginOperation();
    setBusy(true);
    setError('');
    try {
      await loadHome(client, generation);
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function loadItems(library: LibraryView) {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setView('login');
      return;
    }

    const generation = beginOperation();
    setBusy(true);
    setError('');
    try {
      const nextItems = await client.getItems(library.id);
      if (!isCurrentOperation(generation)) {
        return;
      }

      setItems(nextItems);
      setActiveLibrary(library);
      setSelectedItem(null);
      setView('items');
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function loadDetails(itemId: string, origin: DetailsOrigin) {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setView('login');
      return;
    }

    const generation = beginOperation();
    setBusy(true);
    setError('');
    try {
      const item = await client.getItem(itemId);
      if (!isCurrentOperation(generation)) {
        return;
      }

      setSelectedItem(item);
      setDetailsOrigin(origin);
      setView('details');
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function playNatively(item: MediaItem) {
    if (logoutPendingRef.current) {
      return;
    }

    const generation = beginOperation();
    setBusy(true);
    setError('');
    try {
      await requestBridge('playback.nativePlayItem', {
        itemId: item.id,
        itemName: item.name,
        startPositionTicks: item.startPositionTicks || 0,
        mediaSourceId: item.mediaSourceId || '',
        runtimeTicks: item.runtimeTicks || 0,
      });
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  async function logout() {
    if (logoutPendingRef.current) {
      return;
    }

    logoutPendingRef.current = true;
    const generation = beginOperation();
    focusPolicy.clear();
    setBusy(true);
    setError('');
    try {
      await requestBridge('auth.logout');
      if (!isCurrentOperation(generation)) {
        return;
      }

      resetAuthenticatedState();
      setView('login');
    } catch (cause) {
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
      }
    } finally {
      logoutPendingRef.current = false;
      if (isCurrentOperation(generation)) {
        setBusy(false);
      }
    }
  }

  function beginOperation(): number {
    operationGenerationRef.current += 1;
    return operationGenerationRef.current;
  }

  function isCurrentOperation(generation: number): boolean {
    return mountedRef.current && operationGenerationRef.current === generation;
  }

  function replaceHomeRows(rows: readonly HomeRow[]) {
    const snapshot = [...rows];
    homeRowsRef.current = snapshot;
    setHomeRows(snapshot);
  }

  function resetAuthenticatedState() {
    setClient(null);
    replaceHomeRows([]);
    setItems([]);
    setActiveLibrary(null);
    setSelectedItem(null);
    setDetailsOrigin('home');
  }

  return (
    <div className="app-root">
      {error || busy ? (
        <div className="app-notices">
          {error ? <p role="alert">{error}</p> : null}
          {busy ? <p aria-live="polite">Working...</p> : null}
        </div>
      ) : null}

      {view === 'loading' ? (
        <main className="app-page app-page--centered">
          <section className="status-view" aria-labelledby="loading-title">
            <h1 id="loading-title">Noira</h1>
            <p>Loading session...</p>
            {error ? (
              <button type="button" disabled={busy} onClick={() => void bootstrap()}>
                Retry
              </button>
            ) : null}
          </section>
        </main>
      ) : null}

      {view === 'login' ? (
        <main className="app-page app-page--centered">
          <form className="login-form" onSubmit={login}>
            <h1>Noira</h1>
            <label>
              Server URL
              <input name="serverUrl" type="url" required />
            </label>
            <label>
              Username
              <input name="username" autoComplete="username" required />
            </label>
            <label>
              Password
              <input
                name="password"
                type="password"
                autoComplete="current-password"
                required
              />
            </label>
            <button type="submit" disabled={busy}>
              Log in
            </button>
          </form>
        </main>
      ) : null}

      {view === 'home' ? (
        <HomePage
          rows={homeRows}
          onHome={() => void reloadHome()}
          onLogout={() => void logout()}
          onOpenLibrary={(library) => void loadItems(library)}
          onOpenMedia={(item) => void loadDetails(item.id, 'home')}
        />
      ) : null}

      {view === 'items' ? (
        <main className="app-page legacy-page">
          <header className="legacy-page__header">
            <button type="button" disabled={busy} onClick={() => setView('home')}>
              Back
            </button>
            <h1>{activeLibrary?.name || 'Items'}</h1>
          </header>
          {items.length === 0 ? <p>No playable videos were returned.</p> : null}
          <ul className="legacy-media-list">
            {items.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void loadDetails(item.id, 'items')}
                >
                  {item.imageUrl ? <img src={item.imageUrl} alt="" /> : null}
                  <span>
                    <strong>{item.name}</strong>
                    <small>{item.type}</small>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </main>
      ) : null}

      {view === 'details' && selectedItem ? (
        <main className="app-page legacy-page legacy-details">
          <button
            type="button"
            disabled={busy}
            onClick={() => setView(detailsOrigin)}
          >
            Back
          </button>
          <section>
            <h1>{selectedItem.name}</h1>
            {selectedItem.imageUrl ? <img src={selectedItem.imageUrl} alt="" /> : null}
            <p>{selectedItem.type}</p>
            {selectedItem.overview ? <p>{selectedItem.overview}</p> : null}
            <button
              type="button"
              disabled={busy}
              onClick={() => void playNatively(selectedItem)}
            >
              Play
            </button>
          </section>
        </main>
      ) : null}
    </div>
  );
}

function createClient(session: SessionBootstrap): EmbyWebClient {
  return new EmbyWebClient(session, createEmbyFetchTransport(session));
}

function resolveCoreRows(
  catalog: HomeCatalog,
  previousRows: readonly HomeRow[],
): HomeRow[] {
  const nextRowsByKey = new Map(catalog.rows.map((row) => [row.key, row]));
  const previousRowsByKey = new Map(previousRows.map((row) => [row.key, row]));
  const failedKinds = new Set(catalog.failedKinds);
  const result: HomeRow[] = [];

  for (const identity of coreRowOrder) {
    const nextRow = nextRowsByKey.get(identity.key);
    if (nextRow) {
      result.push(nextRow);
      continue;
    }

    if (failedKinds.has(identity.kind)) {
      const previousRow = previousRowsByKey.get(identity.key);
      if (previousRow) {
        result.push(previousRow);
      }
    }
  }

  return result;
}

function mergeRows(
  primaryRows: readonly HomeRow[],
  appendedRows: readonly HomeRow[],
): HomeRow[] {
  const seenKeys = new Set<string>();
  const result: HomeRow[] = [];

  for (const row of [...primaryRows, ...appendedRows]) {
    const key = row.key.trim();
    if (!key || row.items.length === 0 || seenKeys.has(key)) {
      continue;
    }

    seenKeys.add(key);
    result.push(row);
  }

  return result;
}

function isSupplementalRow(row: HomeRow): boolean {
  return row.key.startsWith('latest:');
}

function extractLibraries(rows: readonly HomeRow[]): LibraryView[] {
  const seenIds = new Set<string>();
  const libraries: LibraryView[] = [];

  for (const row of rows) {
    if (row.kind !== 'libraries') {
      continue;
    }

    for (const item of row.items) {
      if (!('collectionType' in item)) {
        continue;
      }

      const id = item.id.trim();
      if (!id || seenIds.has(id)) {
        continue;
      }

      seenIds.add(id);
      libraries.push(item);
    }
  }

  return libraries;
}

function describeCatalogFailures(catalog: HomeCatalog): string {
  if (catalog.failedKinds.length === 0) {
    return '';
  }

  return catalog.failedKinds.length === coreRowOrder.length
    ? 'Unable to load Home from Emby.'
    : 'Some Home rows could not be loaded.';
}

function describeError(cause: unknown): string {
  if (cause instanceof EmbyRequestError) {
    if (cause.status === 401 || cause.status === 403) {
      return `${cause.message}. Log out and authenticate again.`;
    }

    return cause.message;
  }

  if (cause instanceof TypeError) {
    return `Unable to reach Emby from WebView. Check CORS, the server certificate, and network access. ${cause.message}`;
  }

  return cause instanceof Error ? cause.message : 'The request failed.';
}
