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
import { createEmbyFetchTransport } from './transport';
import { App } from './App';
import appSource from './App.tsx?raw';

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

vi.mock('./transport', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./transport')>();
  return {
    ...actual,
    createEmbyFetchTransport: vi.fn(),
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

describe('App browse route integration', () => {
  it('uses the shared route graph and policy Back decision without a parallel details origin', () => {
    expect(appSource).toMatch(/\bpushRoute\b/);
    expect(appSource).toMatch(/\breplaceRoute\b/);
    expect(appSource).toMatch(/\bbackRoute\b/);
    expect(appSource).toMatch(/\.decideBack\(/);
    expect(appSource).not.toMatch(/\bDetailsOrigin\b|\bdetailsOrigin\b/);
  });

  it.each([401, 403])(
    'returns to login and clears browse focus state when a library page receives %s',
    async (status) => {
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
            title: 'Expired libraries anonymous',
            kind: 'libraries',
            items: [
              {
                id: 'expired-library-anonymous',
                name: 'Expired library anonymous',
                collectionType: 'movies',
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
      const statusText = status === 401 ? 'Unauthorized' : 'Forbidden';
      vi.mocked(createEmbyFetchTransport).mockReturnValue(
        vi.fn(async () => new Response(null, { status, statusText })),
      );
      const policy = createFocusNavigationPolicy();
      const clear = vi.spyOn(policy, 'clear');

      render(
        <FocusProvider policy={policy}>
          <App />
        </FocusProvider>,
      );

      fireEvent.click(
        await screen.findByRole('button', {
          name: 'Open Expired library anonymous',
        }),
      );

      await screen.findByRole('textbox', { name: 'Server URL' });
      expect(clear).toHaveBeenCalledTimes(1);
      expect(screen.queryByText('Expired library anonymous')).toBeNull();
      expect(screen.queryByRole('alert')).toBeNull();
      expect(document.body.textContent).not.toContain(String(status));
      expect(document.body.textContent).not.toContain(statusText);
    },
  );

  it('opens a mapped blank collection type as mixed through the real client', async () => {
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
          title: 'Mapped libraries anonymous',
          kind: 'libraries',
          items: [
            {
              id: 'mapped-mixed-library-anonymous',
              name: 'Mapped mixed library anonymous',
              collectionType: '   ',
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
    const transport = vi.fn(async (_input: RequestInfo | URL) =>
      new Response(
        JSON.stringify({
          Items: [
            {
              Id: 'mapped-mixed-item-anonymous',
              Name: 'Mapped mixed item anonymous',
              Type: 'Video',
            },
          ],
          StartIndex: 0,
          TotalRecordCount: 1,
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );
    vi.mocked(createEmbyFetchTransport).mockReturnValue(transport);

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Open Mapped mixed library anonymous',
      }),
    );

    await screen.findByRole('heading', {
      name: 'Mapped mixed library anonymous',
    });
    await screen.findByRole('button', {
      name: 'Open Mapped mixed item anonymous',
    });
    const requestUrl = new URL(String(transport.mock.calls[0]?.[0]));
    expect(requestUrl.searchParams.get('ParentId')).toBe(
      'mapped-mixed-library-anonymous',
    );
    expect(requestUrl.searchParams.get('IncludeItemTypes')).toBe(
      'Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,MusicAlbum,Audio,Photo',
    );
  });

  it('backs Home to library to legacy details with exact source restoration through the real client', async () => {
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
          title: 'Route libraries anonymous',
          kind: 'libraries',
          items: [
            {
              id: 'route-library-anonymous',
              name: 'Route library anonymous',
              collectionType: 'movies',
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
    const transport = vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(
        typeof input === 'string' || input instanceof URL
          ? String(input)
          : input.url,
      );
      if (url.pathname.endsWith('/Items/route-library-item-anonymous')) {
        return new Response(
          JSON.stringify({
            Id: 'route-library-item-anonymous',
            Name: 'Route library details anonymous',
            Type: 'Movie',
            Overview: 'Route library overview anonymous',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      if (
        url.pathname.endsWith('/Items') &&
        url.searchParams.get('ParentId') === 'route-library-anonymous'
      ) {
        return new Response(
          JSON.stringify({
            Items: [
              {
                Id: 'route-library-item-anonymous',
                Name: 'Route library item anonymous',
                Type: 'Movie',
              },
            ],
            StartIndex: 0,
            TotalRecordCount: 1,
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      throw new Error(`Unexpected anonymous route request: ${url.pathname}`);
    });
    vi.mocked(createEmbyFetchTransport).mockReturnValue(transport);

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    const homeLibrary = await screen.findByRole('button', {
      name: 'Open Route library anonymous',
    });
    fireEvent.click(homeLibrary);

    const gridItem = await screen.findByRole('button', {
      name: 'Open Route library item anonymous',
    });
    expect(
      screen.getByRole('button', { name: 'Route library anonymous' })
        .getAttribute('aria-current'),
    ).toBe('page');
    fireEvent.click(gridItem);

    await screen.findByRole('heading', {
      name: 'Route library details anonymous',
    });
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));

    const restoredGridItem = await screen.findByRole('button', {
      name: 'Open Route library item anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(restoredGridItem));
    fireEvent.keyDown(restoredGridItem, { key: 'Escape' });

    const restoredHomeLibrary = await screen.findByRole('button', {
      name: 'Open Route library anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(restoredHomeLibrary));
    expect(restoredHomeLibrary).not.toBe(homeLibrary);

    const pageRequests = transport.mock.calls
      .map(([input]) =>
        new URL(
          typeof input === 'string' || input instanceof URL
            ? String(input)
            : input.url,
        ),
      )
      .filter((url) => url.searchParams.has('StartIndex'));
    expect(pageRequests).toHaveLength(2);
    expect(pageRequests[0].searchParams.get('StartIndex')).toBe('0');
    expect(pageRequests[0].searchParams.get('Limit')).toBe('50');
    expect(pageRequests[0].searchParams.get('ParentId')).toBe(
      'route-library-anonymous',
    );
  });

  it('backs direct Home details to the exact originating row card', async () => {
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
          title: 'Direct details row anonymous',
          kind: 'latest',
          items: [
            {
              id: 'direct-details-item-anonymous',
              name: 'Direct details item anonymous',
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
    const transport = vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(
        typeof input === 'string' || input instanceof URL
          ? String(input)
          : input.url,
      );
      if (url.pathname.endsWith('/Items/direct-details-item-anonymous')) {
        return new Response(
          JSON.stringify({
            Id: 'direct-details-item-anonymous',
            Name: 'Direct details result anonymous',
            Type: 'Movie',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      throw new Error(`Unexpected anonymous direct request: ${url.pathname}`);
    });
    vi.mocked(createEmbyFetchTransport).mockReturnValue(transport);

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    const source = await screen.findByRole('button', {
      name: 'Open Direct details item anonymous',
    });
    fireEvent.click(source);
    await screen.findByRole('heading', { name: 'Direct details result anonymous' });
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));

    const restored = await screen.findByRole('button', {
      name: 'Open Direct details item anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(restored));
    expect(restored).not.toBe(source);
  });

  it('clears superseded Home busy state when a library route wins', async () => {
    let resolveStaleHome!: (value: HomeCatalog) => void;
    const staleHome = new Promise<HomeCatalog>((resolve) => {
      resolveStaleHome = resolve;
    });
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
            title: 'Busy libraries anonymous',
            kind: 'libraries',
            items: [
              {
                id: 'busy-route-library-anonymous',
                name: 'Busy route library anonymous',
                collectionType: 'movies',
              },
            ],
          },
        ],
        failedKinds: [],
      })
      .mockReturnValueOnce(staleHome);
    vi.mocked(loadLibraryLatestCatalog).mockResolvedValue({
      rows: [],
      failedRowKeys: [],
    });
    vi.mocked(createEmbyFetchTransport).mockReturnValue(
      vi.fn(async () =>
        new Response(
          JSON.stringify({ Items: [], StartIndex: 0, TotalRecordCount: 0 }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      ),
    );

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    const library = await screen.findByRole('button', {
      name: 'Open Busy route library anonymous',
    });
    fireEvent.click(screen.getByRole('button', { name: 'Home' }));
    expect(screen.getByText('Working...')).toBeTruthy();
    fireEvent.click(library);

    await screen.findByRole('heading', { name: 'Busy route library anonymous' });
    await waitFor(() => expect(screen.queryByText('Working...')).toBeNull());

    await act(async () => {
      resolveStaleHome({ rows: [], failedKinds: [] });
      await staleHome;
    });
    expect(screen.getByRole('heading', { name: 'Busy route library anonymous' })).toBeTruthy();
  });

  it('clears pending Details busy state when Library Escape wins Back', async () => {
    let resolveDetails!: (value: Response) => void;
    const pendingDetails = new Promise<Response>((resolve) => {
      resolveDetails = resolve;
    });
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
          title: 'Pending details libraries anonymous',
          kind: 'libraries',
          items: [
            {
              id: 'pending-details-library-anonymous',
              name: 'Pending details library anonymous',
              collectionType: 'movies',
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
    vi.mocked(createEmbyFetchTransport).mockReturnValue(
      vi.fn(async (input: RequestInfo | URL) => {
        const url = new URL(
          typeof input === 'string' || input instanceof URL
            ? String(input)
            : input.url,
        );
        if (url.pathname.endsWith('/Items/pending-details-item-anonymous')) {
          return pendingDetails;
        }

        return new Response(
          JSON.stringify({
            Items: [
              {
                Id: 'pending-details-item-anonymous',
                Name: 'Pending details item anonymous',
                Type: 'Movie',
              },
            ],
            StartIndex: 0,
            TotalRecordCount: 1,
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }),
    );

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );

    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Open Pending details library anonymous',
      }),
    );
    const item = await screen.findByRole('button', {
      name: 'Open Pending details item anonymous',
    });
    fireEvent.click(item);
    expect(screen.getByText('Working...')).toBeTruthy();
    fireEvent.keyDown(item, { key: 'Escape' });

    await screen.findByRole('button', {
      name: 'Open Pending details library anonymous',
    });
    await waitFor(() => expect(screen.queryByText('Working...')).toBeNull());

    await act(async () => {
      resolveDetails(
        new Response(
          JSON.stringify({
            Id: 'pending-details-item-anonymous',
            Name: 'Stale pending details anonymous',
            Type: 'Movie',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      );
      await pendingDetails;
    });
    expect(screen.queryByText('Stale pending details anonymous')).toBeNull();
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
