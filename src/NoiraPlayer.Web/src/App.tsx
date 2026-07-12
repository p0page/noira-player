import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import {
  postNativeBack,
  requestBridge,
  subscribeHostLifecycle,
} from './bridge';
import {
  loadHomeCatalog,
  loadLibraryLatestCatalog,
  type HomeCatalog,
  type HomeRow,
  type HomeRowKind,
  type LibraryLatestCatalog,
} from './catalog/homeCatalog';
import { EmbyRequestError, EmbyWebClient } from './emby';
import { BrowseShell, type BrowseShellProps } from './components/BrowseShell';
import { guideScopeKey } from './components/Guide';
import { useFocusNavigationPolicy } from './focus/FocusProvider';
import {
  createFocusRestoreRequest,
  type FocusRestoreRequest,
} from './navigation/focusRequests';
import {
  backRoute,
  pushRoute,
  replaceRoute,
  type BrowseRoute,
  type FocusTarget,
} from './navigation/routes';
import { HomePage } from './pages/HomePage';
import {
  DetailsPage,
  getDetailsActionsScopeKey,
  getDetailsPlayFocusKey,
} from './pages/DetailsPage';
import { LibraryPage } from './pages/LibraryPage';
import { FavoritesPage } from './pages/FavoritesPage';
import { SearchPage } from './pages/SearchPage';
import { createEmbyFetchTransport } from './transport';
import type {
  BootstrapResult,
  LibraryView,
  MediaItem,
  SessionBootstrap,
} from './types';

type AuthState = 'loading' | 'login' | 'browse';

const coreRowOrder: readonly { key: string; kind: HomeRowKind }[] = [
  { key: 'resume', kind: 'resume' },
  { key: 'nextUp', kind: 'nextUp' },
  { key: 'libraries', kind: 'libraries' },
  { key: 'latest', kind: 'latest' },
];

export function App() {
  const focusPolicy = useFocusNavigationPolicy();
  const [authState, setAuthState] = useState<AuthState>('loading');
  const [client, setClient] = useState<EmbyWebClient | null>(null);
  const [homeRows, setHomeRows] = useState<HomeRow[]>([]);
  const [routeStack, setRouteStack] = useState<readonly BrowseRoute[]>([
    { kind: 'home' },
  ]);
  const [activeLibrary, setActiveLibrary] = useState<LibraryView | null>(null);
  const [selectedItem, setSelectedItem] = useState<MediaItem | null>(null);
  const [restoreRequest, setRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const bootstrapStartedRef = useRef(false);
  const mountedRef = useRef(false);
  const operationGenerationRef = useRef(0);
  const logoutPendingRef = useRef(false);
  const playbackLaunchInFlightRef = useRef(false);
  const homeRowsRef = useRef<readonly HomeRow[]>([]);
  const currentRoute: BrowseRoute = routeStack[routeStack.length - 1] ?? {
    kind: 'home',
  };
  const guideActiveRoute = resolveGuideActiveRoute(routeStack);

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

  useEffect(() => {
    return subscribeHostLifecycle((event) => {
      if (event.event === 'activated-home') {
        const activationRestoreTarget =
          currentRoute.kind === 'home' ? null : currentRoute.origin;
        beginOperation();
        playbackLaunchInFlightRef.current = false;
        focusPolicy.resume();
        setBusy(false);
        setRouteStack([{ kind: 'home' }]);
        setActiveLibrary(null);
        setSelectedItem(null);
        setRestoreRequest(
          activationRestoreTarget
            ? createFocusRestoreRequest(activationRestoreTarget)
            : null,
        );
        return;
      }

      if (event.event !== 'playback-returned') {
        return;
      }

      playbackLaunchInFlightRef.current = false;
      if (currentRoute.kind !== 'details') {
        return;
      }

      setBusy(false);
      setRestoreRequest(
        createFocusRestoreRequest({
          scopeKey: getDetailsActionsScopeKey(),
          focusKey: getDetailsPlayFocusKey(),
        }),
      );
    });
  }, [currentRoute.kind, currentRoute.kind === 'details' ? currentRoute.itemId : '']);

  async function bootstrap() {
    const generation = beginOperation();
    setBusy(true);
    setError('');
    setAuthState('loading');

    try {
      const result = await requestBridge<BootstrapResult>('auth.bootstrap');
      if (!isCurrentOperation(generation)) {
        return;
      }

      if (!result.session) {
        resetAuthenticatedState();
        setAuthState('login');
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
    setRouteStack([{ kind: 'home' }]);
    setActiveLibrary(null);
    setSelectedItem(null);
    setRestoreRequest(null);
    setAuthState('browse');
    setError(describeCatalogFailures(catalog));
    setBusy(false);

    const libraries = extractLibraries(coreRows);
    const supplementalCatalog = await loadLibraryLatestCatalog(
      nextClient,
      libraries,
    );
    if (!isCurrentOperation(generation)) {
      return;
    }

    const supplementalRows = resolveSupplementalRows(
      libraries,
      supplementalCatalog,
      homeRowsRef.current.filter(isSupplementalRow),
    );
    replaceHomeRows(mergeRows(coreRows, supplementalRows));
    setError(
      describeCatalogFailures(catalog),
    );
  }

  async function reloadHome() {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setAuthState('login');
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

  function openLibrary(library: LibraryView, origin: FocusTarget) {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setAuthState('login');
      return;
    }

    const normalizedLibrary = {
      ...library,
      id: library.id.trim(),
      collectionType: library.collectionType.trim() || 'mixed',
    };
    const libraryOrigin =
      currentRoute.kind === 'library' ? currentRoute.origin : origin;
    const nextRoute: BrowseRoute = {
      kind: 'library',
      libraryId: normalizedLibrary.id,
      collectionType: normalizedLibrary.collectionType,
      origin: libraryOrigin,
    };
    const nextStack =
      currentRoute.kind === 'home'
        ? pushRoute(routeStack, nextRoute)
        : currentRoute.kind === 'library'
          ? replaceRoute(routeStack, nextRoute)
          : routeStack;
    if (nextStack === routeStack) {
      return;
    }

    beginOperation();
    setBusy(false);
    setError('');
    setActiveLibrary(normalizedLibrary);
    setSelectedItem(null);
    setRestoreRequest(null);
    setRouteStack(nextStack);
  }

  function openShellDestination(kind: 'search' | 'favorites') {
    if (logoutPendingRef.current || !client) {
      return;
    }

    const origin: FocusTarget = {
      scopeKey: guideScopeKey,
      focusKey: kind === 'search' ? 'guide:search' : 'guide:favorites',
    };
    const nextStack = pushRoute([{ kind: 'home' }], { kind, origin });
    beginOperation();
    setBusy(false);
    setError('');
    setActiveLibrary(null);
    setSelectedItem(null);
    setRestoreRequest(null);
    setRouteStack(nextStack);
  }

  async function loadDetails(itemId: string, origin: FocusTarget) {
    if (logoutPendingRef.current) {
      return;
    }
    if (!client) {
      setAuthState('login');
      return;
    }

    const sourceStack = routeStack;
    const generation = beginOperation();
    setBusy(true);
    setError('');
    try {
      const item = await client.getItem(itemId);
      if (!isCurrentOperation(generation)) {
        return;
      }

      const nextStack = pushRoute(sourceStack, {
        kind: 'details',
        itemId: item.id,
        origin,
      });
      if (nextStack === sourceStack) {
        return;
      }

      setSelectedItem(item);
      setRestoreRequest(null);
      setRouteStack(nextStack);
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

  function browseBack(restoreTarget?: FocusTarget) {
    const decision = focusPolicy.decideBack(routeStack);
    if (decision.kind !== 'navigate') {
      return;
    }

    const nextStack = backRoute(routeStack);
    if (nextStack === routeStack) {
      return;
    }

    beginOperation();
    setBusy(false);
    setError('');
    setRestoreRequest(
      createFocusRestoreRequest(restoreTarget ?? decision.restoreTarget),
    );
    setRouteStack(nextStack);
    setSelectedItem(null);
    if (decision.route.kind === 'home') {
      setActiveLibrary(null);
    }
  }

  async function playNatively(item: MediaItem) {
    if (logoutPendingRef.current || playbackLaunchInFlightRef.current) {
      return;
    }

    playbackLaunchInFlightRef.current = true;
    const generation = beginOperation();
    focusPolicy.pause();
    setBusy(true);
    setError('');
    setRestoreRequest(null);
    try {
      await requestBridge('playback.nativePlayItem', {
        itemId: item.id,
        itemName: item.name,
        startPositionTicks: item.startPositionTicks ?? 0,
        mediaSourceId: item.mediaSourceId ?? '',
        runtimeTicks: item.runtimeTicks ?? 0,
      });
    } catch (cause) {
      playbackLaunchInFlightRef.current = false;
      focusPolicy.resume();
      if (isCurrentOperation(generation)) {
        setError(describeError(cause));
        setRestoreRequest(
          createFocusRestoreRequest({
            scopeKey: getDetailsActionsScopeKey(),
            focusKey: getDetailsPlayFocusKey(),
          }),
        );
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
    setBusy(true);
    setError('');
    try {
      await requestBridge('auth.logout');
      if (!isCurrentOperation(generation)) {
        return;
      }

      focusPolicy.clear();
      resetAuthenticatedState();
      setAuthState('login');
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

  function requireAuthentication() {
    beginOperation();
    window.setTimeout(() => {
      if (!mountedRef.current) {
        return;
      }

      focusPolicy.clear();
      resetAuthenticatedState();
      setBusy(false);
      setError('');
      setAuthState('login');
    }, 0);
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
    playbackLaunchInFlightRef.current = false;
    setClient(null);
    replaceHomeRows([]);
    setRouteStack([{ kind: 'home' }]);
    setActiveLibrary(null);
    setSelectedItem(null);
    setRestoreRequest(null);
  }

  return (
    <div className="app-root">
      {error || busy ? (
        <div className="app-notices">
          {error ? <p role="alert">{error}</p> : null}
          {busy ? <p aria-live="polite">Working...</p> : null}
        </div>
      ) : null}

      {authState === 'loading' ? (
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

      {authState === 'login' ? (
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

      {authState === 'browse' ? (
        <BrowseShell
          activeRoute={guideActiveRoute}
          defaultGuideFocus={
            currentRoute.kind === 'home' && !hasFocusableHomeContent(homeRows)
          }
          guideOverlayOnly={currentRoute.kind === 'details'}
          onFavorites={() => {
            if (currentRoute.kind !== 'favorites') {
              openShellDestination('favorites');
            }
          }}
          onHome={() => void reloadHome()}
          onLogout={() => void logout()}
          onNavigateBack={(target) => browseBack(target)}
          onNativeBack={() => void postNativeBack()}
          onSearch={() => {
            if (currentRoute.kind !== 'search') {
              openShellDestination('search');
            }
          }}
          restoreRequest={restoreRequest}
          routeStack={routeStack}
        >
          {currentRoute.kind === 'home' ? (
            <HomePage
              busy={busy}
              restoreRequest={restoreRequest}
              rows={homeRows}
              onOpenLibrary={openLibrary}
              onOpenMedia={(item, origin) => void loadDetails(item.id, origin)}
            />
          ) : null}

          {currentRoute.kind === 'library' && client && activeLibrary ? (
            <LibraryPage
              busy={busy}
              client={client}
              library={activeLibrary}
              onAuthenticationRequired={requireAuthentication}
              restoreRequest={restoreRequest}
              onOpenMedia={(item, origin) => void loadDetails(item.id, origin)}
            />
          ) : null}

          {currentRoute.kind === 'search' && client ? (
          <SearchPage
            client={client}
            onOpenMedia={(item, origin) => void loadDetails(item.id, origin)}
          />
          ) : null}

          {currentRoute.kind === 'favorites' && client ? (
          <FavoritesPage
            client={client}
            onOpenMedia={(item, origin) => void loadDetails(item.id, origin)}
          />
          ) : null}

          {currentRoute.kind === 'details' && selectedItem ? (
            <DetailsPage
              busy={busy}
              item={selectedItem}
              restoreRequest={restoreRequest}
              onPlay={(item) => void playNatively(item)}
            />
          ) : null}
        </BrowseShell>
      ) : null}
    </div>
  );
}

function createClient(session: SessionBootstrap): EmbyWebClient {
  return new EmbyWebClient(session, createEmbyFetchTransport(session));
}

function resolveGuideActiveRoute(
  routeStack: readonly BrowseRoute[],
): BrowseShellProps['activeRoute'] {
  for (let index = routeStack.length - 1; index >= 0; index -= 1) {
    const route = routeStack[index];
    if (route.kind === 'search' || route.kind === 'favorites') {
      return { kind: route.kind };
    }
    if (route.kind === 'library') {
      return { kind: 'library', libraryId: route.libraryId };
    }
    if (route.kind === 'home') {
      return { kind: 'home' };
    }
  }
  return { kind: 'home' };
}

function hasFocusableHomeContent(rows: readonly HomeRow[]): boolean {
  return rows.some((row) =>
    row.items.some((item) => typeof item.id === 'string' && item.id.trim().length > 0),
  );
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
      libraries.push({ ...item, id });
    }
  }

  return libraries;
}

function resolveSupplementalRows(
  libraries: readonly LibraryView[],
  catalog: LibraryLatestCatalog,
  previousRows: readonly HomeRow[],
): HomeRow[] {
  const nextRowsByKey = new Map(catalog.rows.map((row) => [row.key.trim(), row]));
  const previousRowsByKey = new Map(
    previousRows.map((row) => [row.key.trim(), row]),
  );
  const failedRowKeys = new Set(catalog.failedRowKeys.map((key) => key.trim()));
  const result: HomeRow[] = [];

  for (const library of libraries) {
    const key = `latest:${library.id}`;
    const nextRow = nextRowsByKey.get(key);
    if (nextRow) {
      result.push(nextRow);
      continue;
    }

    if (failedRowKeys.has(key)) {
      const previousRow = previousRowsByKey.get(key);
      if (previousRow) {
        result.push(previousRow);
      }
    }
  }

  return result;
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
