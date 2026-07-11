// @vitest-environment jsdom

import { StrictMode } from 'react';
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { requestBridge } from './bridge';
import {
  loadHomeCatalog,
  loadLibraryLatestCatalog,
  type HomeCatalog,
  type LibraryLatestCatalog,
} from './catalog/homeCatalog';
import { FocusProvider } from './focus/FocusProvider';
import { createFocusNavigationPolicy } from './focus/focusPolicy';
import { App } from './App';

vi.mock('./bridge', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./bridge')>();
  return {
    ...actual,
    requestBridge: vi.fn(),
  };
});

vi.mock('./catalog/homeCatalog', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./catalog/homeCatalog')>();
  return {
    ...actual,
    loadHomeCatalog: vi.fn(),
    loadLibraryLatestCatalog: vi.fn(),
  };
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('App Home orchestration', () => {
  it('bootstraps once in StrictMode and defaults focus to media rendered after the Guide', async () => {
    vi.mocked(requestBridge).mockResolvedValue({
      session: {
        serverUrl: 'https://anonymous.invalid',
        userId: 'anonymous-user',
        userName: 'Anonymous User',
        accessToken: 'anonymous-token',
        authorization: 'Anonymous authorization',
      },
    });
    vi.mocked(loadHomeCatalog).mockResolvedValue({
      rows: [
        {
          key: 'latest',
          title: 'Latest',
          kind: 'latest',
          items: [
            {
              id: 'strict-item-anonymous',
              name: 'Strict item anonymous',
              type: 'Movie',
              artwork: {},
            },
          ],
        },
      ],
      failedKinds: [],
    });
    vi.mocked(loadLibraryLatestCatalog).mockResolvedValue({
      rows: [],
      failedRowKeys: [],
    });

    render(
      <StrictMode>
        <FocusProvider>
          <App />
        </FocusProvider>
      </StrictMode>,
    );

    const card = await screen.findByRole('button', {
      name: 'Open Strict item anonymous',
    });
    await waitFor(() => {
      expect(document.activeElement).toBe(card);
    });
    expect(document.activeElement).not.toBe(screen.getByRole('button', { name: 'Home' }));
    expect(vi.mocked(requestBridge)).toHaveBeenCalledTimes(1);
    expect(vi.mocked(requestBridge)).toHaveBeenCalledWith('auth.bootstrap');
    expect(vi.mocked(loadHomeCatalog)).toHaveBeenCalledTimes(1);
  });

  it('ignores stale supplemental results after a newer Home generation completes', async () => {
    const staleSupplemental = deferred<LibraryLatestCatalog>();
    vi.mocked(requestBridge).mockResolvedValue({
      session: {
        serverUrl: 'https://anonymous.invalid',
        userId: 'anonymous-user',
        userName: 'Anonymous User',
        accessToken: 'anonymous-token',
        authorization: 'Anonymous authorization',
      },
    });
    vi.mocked(loadHomeCatalog)
      .mockResolvedValueOnce({
        rows: [
          {
            key: 'libraries',
            title: 'My Media',
            kind: 'libraries',
            items: [
              {
                id: 'library-anonymous',
                name: 'Library anonymous',
                collectionType: 'movies',
              },
            ],
          },
        ],
        failedKinds: [],
      })
      .mockResolvedValueOnce({
        rows: [
          {
            key: 'latest',
            title: 'Latest',
            kind: 'latest',
            items: [
              {
                id: 'fresh-item-anonymous',
                name: 'Fresh item anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
        ],
        failedKinds: [],
      });
    vi.mocked(loadLibraryLatestCatalog)
      .mockReturnValueOnce(staleSupplemental.promise)
      .mockResolvedValueOnce({ rows: [], failedRowKeys: [] });

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    await screen.findByRole('button', { name: 'Library anonymous' });
    fireEvent.click(screen.getByRole('button', { name: 'Home' }));
    await screen.findByRole('button', { name: 'Open Fresh item anonymous' });

    await act(async () => {
      staleSupplemental.resolve({
        rows: [
          {
            key: 'latest:library-anonymous',
            title: 'Latest in Library anonymous',
            kind: 'latest',
            items: [
              {
                id: 'stale-item-anonymous',
                name: 'Stale item anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
        ],
        failedRowKeys: [],
      });
      await staleSupplemental.promise;
    });

    expect(screen.queryByText('Stale item anonymous')).toBeNull();
    expect(screen.getByText('Fresh item anonymous')).toBeTruthy();
  });

  it('retains only rejected supplemental rows, removes successful empty rows, and preserves library order', async () => {
    vi.mocked(requestBridge).mockResolvedValue({
      session: {
        serverUrl: 'https://anonymous.invalid',
        userId: 'anonymous-user',
        userName: 'Anonymous User',
        accessToken: 'anonymous-token',
        authorization: 'Anonymous authorization',
      },
    });
    vi.mocked(loadHomeCatalog).mockResolvedValue({
      rows: [
        {
          key: 'libraries',
          title: 'My Media',
          kind: 'libraries',
          items: [
            {
              id: '  library-a-anonymous  ',
              name: 'Library A anonymous',
              collectionType: 'movies',
            },
            {
              id: 'library-a-anonymous',
              name: 'Duplicate A anonymous',
              collectionType: 'tvshows',
            },
            {
              id: ' ',
              name: 'Blank library anonymous',
              collectionType: 'movies',
            },
            {
              id: ' library-b-anonymous ',
              name: 'Library B anonymous',
              collectionType: 'tvshows',
            },
            {
              id: 'library-c-anonymous',
              name: 'Library C anonymous',
              collectionType: 'music',
            },
          ],
        },
      ],
      failedKinds: [],
    });
    vi.mocked(loadLibraryLatestCatalog)
      .mockResolvedValueOnce({
        rows: [
          {
            key: 'latest:library-a-anonymous',
            title: 'Latest in Library A anonymous',
            kind: 'latest',
            items: [
              {
                id: 'old-a-anonymous',
                name: 'Old A anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
          {
            key: 'latest:library-b-anonymous',
            title: 'Latest in Library B anonymous',
            kind: 'latest',
            items: [
              {
                id: 'old-b-anonymous',
                name: 'Old B anonymous',
                type: 'Series',
                artwork: {},
              },
            ],
          },
          {
            key: 'latest:library-c-anonymous',
            title: 'Latest in Library C anonymous',
            kind: 'latest',
            items: [
              {
                id: 'old-c-anonymous',
                name: 'Old C anonymous',
                type: 'Audio',
                artwork: {},
              },
            ],
          },
        ],
        failedRowKeys: [],
      })
      .mockResolvedValueOnce({
        rows: [
          {
            key: 'latest:library-b-anonymous',
            title: 'Latest in Library B anonymous',
            kind: 'latest',
            items: [
              {
                id: 'new-b-anonymous',
                name: 'New B anonymous',
                type: 'Series',
                artwork: {},
              },
            ],
          },
        ],
        failedRowKeys: ['latest:library-a-anonymous'],
      });

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    await screen.findByText('Old C anonymous');
    fireEvent.click(screen.getByRole('button', { name: 'Home' }));
    await screen.findByText('New B anonymous');

    expect(screen.getByText('Old A anonymous')).toBeTruthy();
    expect(screen.queryByText('Old B anonymous')).toBeNull();
    expect(screen.queryByText('Old C anonymous')).toBeNull();
    expect(screen.getByRole('alert').textContent).toContain(
      'Some Home rows could not be loaded.',
    );
    expect(
      Array.from(document.querySelectorAll<HTMLElement>('[data-row-key^="latest:"]')).map(
        (row) => row.dataset.rowKey,
      ),
    ).toEqual(['latest:library-a-anonymous', 'latest:library-b-anonymous']);
    expect(
      vi.mocked(loadLibraryLatestCatalog).mock.calls[1]?.[1].map((library) => ({
        id: library.id,
        name: library.name,
      })),
    ).toEqual([
      { id: 'library-a-anonymous', name: 'Library A anonymous' },
      { id: 'library-b-anonymous', name: 'Library B anonymous' },
      { id: 'library-c-anonymous', name: 'Library C anonymous' },
    ]);
  });

  it('keeps Home controls focusable while a newer Home action supersedes stale work', async () => {
    const staleCore = deferred<HomeCatalog>();
    const currentCore = deferred<HomeCatalog>();
    vi.mocked(requestBridge).mockResolvedValue({
      session: {
        serverUrl: 'https://anonymous.invalid',
        userId: 'anonymous-user',
        userName: 'Anonymous User',
        accessToken: 'anonymous-token',
        authorization: 'Anonymous authorization',
      },
    });
    vi.mocked(loadHomeCatalog)
      .mockResolvedValueOnce({
        rows: [
          {
            key: 'latest',
            title: 'Latest',
            kind: 'latest',
            items: [
              {
                id: 'initial-item-anonymous',
                name: 'Initial item anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
        ],
        failedKinds: [],
      })
      .mockReturnValueOnce(staleCore.promise)
      .mockReturnValueOnce(currentCore.promise);
    vi.mocked(loadLibraryLatestCatalog).mockResolvedValue({
      rows: [],
      failedRowKeys: [],
    });

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    const initialCard = await screen.findByRole('button', {
      name: 'Open Initial item anonymous',
    });
    const home = screen.getByRole('button', { name: 'Home' });
    fireEvent.click(home);
    expect(screen.getByRole('main').getAttribute('aria-busy')).toBe('true');
    expect((home as HTMLButtonElement).disabled).toBe(false);
    expect((initialCard as HTMLButtonElement).disabled).toBe(false);

    fireEvent.click(home);
    await act(async () => {
      currentCore.resolve({
        rows: [
          {
            key: 'latest',
            title: 'Latest',
            kind: 'latest',
            items: [
              {
                id: 'current-item-anonymous',
                name: 'Current item anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
        ],
        failedKinds: [],
      });
      await currentCore.promise;
    });
    await screen.findByText('Current item anonymous');

    await act(async () => {
      staleCore.resolve({
        rows: [
          {
            key: 'latest',
            title: 'Latest',
            kind: 'latest',
            items: [
              {
                id: 'stale-core-item-anonymous',
                name: 'Stale core item anonymous',
                type: 'Movie',
                artwork: {},
              },
            ],
          },
        ],
        failedKinds: [],
      });
      await staleCore.promise;
    });

    expect(screen.queryByText('Stale core item anonymous')).toBeNull();
    expect(screen.getByText('Current item anonymous')).toBeTruthy();
  });

  it('retains Home content and focus policy memory when logout fails', async () => {
    vi.mocked(requestBridge).mockImplementation(async (type) => {
      if (type === 'auth.bootstrap') {
        return {
          session: {
            serverUrl: 'https://anonymous.invalid',
            userId: 'anonymous-user',
            userName: 'Anonymous User',
            accessToken: 'anonymous-token',
            authorization: 'Anonymous authorization',
          },
        };
      }

      throw new Error('Anonymous logout failure');
    });
    vi.mocked(loadHomeCatalog).mockResolvedValue({
      rows: [
        {
          key: 'latest',
          title: 'Latest',
          kind: 'latest',
          items: [
            {
              id: 'logout-item-anonymous',
              name: 'Logout item anonymous',
              type: 'Movie',
              artwork: {},
            },
          ],
        },
      ],
      failedKinds: [],
    });
    vi.mocked(loadLibraryLatestCatalog).mockResolvedValue({
      rows: [],
      failedRowKeys: [],
    });
    const policy = createFocusNavigationPolicy();
    const clear = vi.spyOn(policy, 'clear');

    render(
      <FocusProvider policy={policy}>
        <App />
      </FocusProvider>,
    );

    const card = await screen.findByRole('button', {
      name: 'Open Logout item anonymous',
    });
    await act(async () => {
      card.focus();
    });
    expect(document.activeElement).toBe(card);
    const focusKey = card.getAttribute('data-focus-key') as string;
    policy.remember('home-row:latest', focusKey, [focusKey]);

    fireEvent.click(screen.getByRole('button', { name: 'Log out' }));
    await screen.findByRole('alert');

    expect(screen.getByText('Logout item anonymous')).toBeTruthy();
    expect(document.activeElement).toBe(card);
    expect(policy.resolve('home-row:latest', [focusKey])).toBe(focusKey);
    expect(clear).not.toHaveBeenCalled();
  });

  it('does not continue bootstrap work after unmount', async () => {
    const bootstrapRequest = deferred<{
      session: {
        serverUrl: string;
        userId: string;
        userName: string;
        accessToken: string;
        authorization: string;
      };
    }>();
    vi.mocked(requestBridge).mockReturnValue(bootstrapRequest.promise);

    const view = render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );
    view.unmount();

    await act(async () => {
      bootstrapRequest.resolve({
        session: {
          serverUrl: 'https://anonymous.invalid',
          userId: 'anonymous-user',
          userName: 'Anonymous User',
          accessToken: 'anonymous-token',
          authorization: 'Anonymous authorization',
        },
      });
      await bootstrapRequest.promise;
    });

    expect(vi.mocked(loadHomeCatalog)).not.toHaveBeenCalled();
  });
});

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, reject, resolve };
}

interface Deferred<T> {
  promise: Promise<T>;
  reject(reason?: unknown): void;
  resolve(value: T | PromiseLike<T>): void;
}
