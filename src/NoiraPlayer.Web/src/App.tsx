import { FormEvent, useEffect, useState } from 'react';
import { requestBridge } from './bridge';
import { EmbyRequestError, EmbyWebClient } from './emby';
import { createEmbyFetchTransport } from './transport';
import type { BootstrapResult, LibraryView, MediaItem, SessionBootstrap } from './types';

type View = 'loading' | 'login' | 'home' | 'items' | 'details';

export function App() {
  const [view, setView] = useState<View>('loading');
  const [session, setSession] = useState<SessionBootstrap | null>(null);
  const [client, setClient] = useState<EmbyWebClient | null>(null);
  const [libraries, setLibraries] = useState<LibraryView[]>([]);
  const [items, setItems] = useState<MediaItem[]>([]);
  const [selectedItem, setSelectedItem] = useState<MediaItem | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    void bootstrap();
  }, []);

  async function bootstrap() {
    setBusy(true);
    setError('');
    setView('loading');
    try {
      const result = await requestBridge<BootstrapResult>('auth.bootstrap');
      if (!result.session) {
        setSession(null);
        setView('login');
        return;
      }

      const nextClient = createClient(result.session);
      setSession(result.session);
      setClient(nextClient);
      await loadHome(nextClient);
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      const data = new FormData(event.currentTarget);
      const result = await requestBridge<BootstrapResult>(
        'auth.login',
        {
          serverUrl: String(data.get('serverUrl') || ''),
          username: String(data.get('username') || ''),
          password: String(data.get('password') || ''),
        },
        { timeoutMs: 45000 },
      );
      if (!result.session) {
        throw new Error('Native login completed without a saved Emby session.');
      }

      const nextClient = createClient(result.session);
      setSession(result.session);
      setClient(nextClient);
      await loadHome(nextClient);
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function loadHome(client: EmbyWebClient) {
    const nextLibraries = await client.getViews();
    setLibraries(nextLibraries);
    setItems([]);
    setSelectedItem(null);
    setView('home');
  }

  async function reloadHome() {
    if (!client) {
      setView('login');
      return;
    }

    setBusy(true);
    setError('');
    try {
      await loadHome(client);
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function loadItems(libraryId: string) {
    if (!client) {
      setView('login');
      return;
    }

    setBusy(true);
    setError('');
    try {
      const nextItems = await client.getItems(libraryId);
      setItems(nextItems);
      setSelectedItem(null);
      setView('items');
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function loadDetails(itemId: string) {
    if (!client) {
      setView('login');
      return;
    }

    setBusy(true);
    setError('');
    try {
      const item = await client.getItem(itemId);
      setSelectedItem(item);
      setView('details');
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function playNatively(item: MediaItem) {
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
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  async function logout() {
    setBusy(true);
    setError('');
    try {
      await requestBridge('auth.logout');
      setSession(null);
      setClient(null);
      setLibraries([]);
      setItems([]);
      setSelectedItem(null);
      setView('login');
    } catch (cause) {
      setError(describeError(cause));
    } finally {
      setBusy(false);
    }
  }

  return (
    <main>
      <header>
        <h1>Noira</h1>
        {session ? (
          <p>
            {session.userName || session.userId} @ {session.serverUrl}
          </p>
        ) : null}
      </header>

      {error ? <p role="alert">{error}</p> : null}
      {busy ? <p aria-live="polite">Working...</p> : null}

      {view === 'loading' ? (
        <section>
          <p>Loading session...</p>
          {error ? (
            <button type="button" disabled={busy} onClick={() => void bootstrap()}>
              Retry
            </button>
          ) : null}
        </section>
      ) : null}

      {view === 'login' ? (
        <form onSubmit={login}>
          <label>
            Server URL
            <input name="serverUrl" type="url" placeholder="https://emby.example" required />
          </label>
          <label>
            Username
            <input name="username" autoComplete="username" required />
          </label>
          <label>
            Password
            <input name="password" type="password" autoComplete="current-password" required />
          </label>
          <button type="submit" disabled={busy}>
            Log in
          </button>
        </form>
      ) : null}

      {view === 'home' ? (
        <section>
          <button type="button" disabled={busy} onClick={() => void logout()}>
            Log out
          </button>
          <button type="button" disabled={busy} onClick={() => void reloadHome()}>
            Refresh
          </button>
          <h2>Libraries</h2>
          {libraries.length === 0 ? <p>No video libraries were returned.</p> : null}
          <ul>
            {libraries.map((library) => (
              <li key={library.id}>
                <button type="button" disabled={busy} onClick={() => void loadItems(library.id)}>
                  {library.imageUrl ? <img src={library.imageUrl} alt="" /> : null}
                  {library.name}
                  {library.collectionType ? ` (${library.collectionType})` : ''}
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {view === 'items' ? (
        <section>
          <button type="button" disabled={busy} onClick={() => void reloadHome()}>
            Back
          </button>
          <h2>Items</h2>
          {items.length === 0 ? <p>No playable videos were returned.</p> : null}
          <ul>
            {items.map((item) => (
              <li key={item.id}>
                <button type="button" disabled={busy} onClick={() => void loadDetails(item.id)}>
                  {item.imageUrl ? <img src={item.imageUrl} alt="" /> : null}
                  {item.name} ({item.type})
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {view === 'details' && selectedItem ? (
        <section>
          <button type="button" disabled={busy} onClick={() => setView('items')}>
            Back
          </button>
          <h2>{selectedItem.name}</h2>
          {selectedItem.imageUrl ? <img src={selectedItem.imageUrl} alt="" /> : null}
          <p>{selectedItem.type}</p>
          {selectedItem.overview ? <p>{selectedItem.overview}</p> : null}
          <button type="button" disabled={busy} onClick={() => void playNatively(selectedItem)}>
            Play
          </button>
        </section>
      ) : null}
    </main>
  );
}

function createClient(session: SessionBootstrap): EmbyWebClient {
  return new EmbyWebClient(session, createEmbyFetchTransport(session));
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
