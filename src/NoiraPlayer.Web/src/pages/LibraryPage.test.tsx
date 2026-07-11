// @vitest-environment jsdom

import {
  setFocus,
  updateAllLayouts,
} from '@noriginmedia/norigin-spatial-navigation';
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { EmbyWebClient } from '../emby';
import { FocusProvider } from '../focus/FocusProvider';
import { createFocusNavigationPolicy } from '../focus/focusPolicy';
import type { ItemPage } from '../types';
import { LibraryPage } from './LibraryPage';
import libraryPageSource from './LibraryPage.tsx?raw';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('LibraryPage paging', () => {
  it('requests page zero with the selected real library contract', async () => {
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValue({
        items: [
          {
            id: 'page-zero-item-anonymous',
            name: 'Page zero item anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 1,
      });

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'page-zero-library-anonymous',
            name: 'Page zero library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'page-zero-library-anonymous',
              name: 'Page zero library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:page-zero-anonymous',
            focusKey: 'home-card:page-zero-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    await screen.findByRole('button', { name: 'Open Page zero item anonymous' });

    expect(getItemsPage).toHaveBeenCalledTimes(1);
    expect(getItemsPage).toHaveBeenCalledWith(
      'page-zero-library-anonymous',
      0,
      50,
      { collectionType: 'movies' },
    );
    expect(
      within(screen.getByRole('navigation', { name: 'Guide' })).getByRole(
        'button',
        { name: 'Page zero library anonymous' },
      ).getAttribute('aria-current'),
    ).toBe('page');
  });

  it('appends by API position, rejects blank and duplicate IDs, and preserves existing DOM focus', async () => {
    let resolveNextPage!: (value: ItemPage) => void;
    const nextPage = new Promise<ItemPage>((resolve) => {
      resolveNextPage = resolve;
    });
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValueOnce({
        items: [
          {
            id: '  preserved-a-anonymous  ',
            name: 'Preserved A anonymous',
            type: 'Movie',
            artwork: {},
          },
          {
            id: 'preserved-b-anonymous',
            name: 'Preserved B anonymous',
            type: 'Movie',
            artwork: {},
          },
          {
            id: '   ',
            name: 'Blank item anonymous',
            type: 'Movie',
            artwork: {},
          },
          {
            id: 'preserved-b-anonymous',
            name: 'Duplicate B anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 6,
      })
      .mockReturnValueOnce(nextPage);

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'append-library-anonymous',
            name: 'Append library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'append-library-anonymous',
              name: 'Append library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:append-anonymous',
            focusKey: 'home-card:append-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const firstCard = await screen.findByRole('button', {
      name: 'Open Preserved A anonymous',
    });
    const focusedCard = screen.getByRole('button', {
      name: 'Open Preserved B anonymous',
    });
    await waitFor(() => expect(getItemsPage).toHaveBeenCalledTimes(2));
    expect(getItemsPage.mock.calls[1]?.[1]).toBe(4);
    expect(screen.queryByText('Blank item anonymous')).toBeNull();
    expect(screen.queryByText('Duplicate B anonymous')).toBeNull();

    await updateLayoutsAndFocus(
      focusedCard.getAttribute('data-focus-key') as string,
    );
    expect(document.activeElement).toBe(focusedCard);

    await act(async () => {
      resolveNextPage({
        items: [
          {
            id: 'preserved-a-anonymous',
            name: 'Duplicate A anonymous',
            type: 'Movie',
            artwork: {},
          },
          {
            id: 'appended-c-anonymous',
            name: 'Appended C anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 4,
        totalRecordCount: 6,
      });
      await nextPage;
    });

    await screen.findByRole('button', { name: 'Open Appended C anonymous' });
    expect(
      screen.getByRole('button', { name: 'Open Preserved A anonymous' }),
    ).toBe(firstCard);
    expect(document.activeElement).toBe(focusedCard);
    expect(screen.queryByText('Duplicate A anonymous')).toBeNull();
    expect(screen.getAllByRole('button', { name: /Open Preserved/ })).toHaveLength(2);
  });

  it('ignores a stale page after the selected library changes', async () => {
    let resolveStale!: (value: {
      items: Array<{
        id: string;
        name: string;
        type: string;
        artwork: Record<string, never>;
      }>;
      startIndex: number;
      totalRecordCount: number;
    }) => void;
    const stalePage = new Promise<{
      items: Array<{
        id: string;
        name: string;
        type: string;
        artwork: Record<string, never>;
      }>;
      startIndex: number;
      totalRecordCount: number;
    }>((resolve) => {
      resolveStale = resolve;
    });
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockImplementation(async (parentId) => {
        if (parentId === 'stale-library-a-anonymous') {
          return stalePage;
        }

        return {
          items: [
            {
              id: 'fresh-library-b-item-anonymous',
              name: 'Fresh library B item anonymous',
              type: 'Series',
              artwork: {},
            },
          ],
          startIndex: 0,
          totalRecordCount: 1,
        };
      });
    const view = render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'stale-library-a-anonymous',
            name: 'Stale library A anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'stale-library-a-anonymous',
              name: 'Stale library A anonymous',
              collectionType: 'movies',
            },
            {
              id: 'fresh-library-b-anonymous',
              name: 'Fresh library B anonymous',
              collectionType: 'tvshows',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:stale-anonymous',
            focusKey: 'home-card:stale-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    await waitFor(() => expect(getItemsPage).toHaveBeenCalledTimes(1));
    view.rerender(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'fresh-library-b-anonymous',
            name: 'Fresh library B anonymous',
            collectionType: 'tvshows',
          }}
          libraries={[
            {
              id: 'stale-library-a-anonymous',
              name: 'Stale library A anonymous',
              collectionType: 'movies',
            },
            {
              id: 'fresh-library-b-anonymous',
              name: 'Fresh library B anonymous',
              collectionType: 'tvshows',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:stale-anonymous',
            focusKey: 'home-card:stale-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    await screen.findByRole('button', {
      name: 'Open Fresh library B item anonymous',
    });
    await act(async () => {
      resolveStale({
        items: [
          {
            id: 'stale-library-a-item-anonymous',
            name: 'Stale library A item anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 1,
      });
      await stalePage;
    });

    expect(screen.queryByText('Stale library A item anonymous')).toBeNull();
    expect(screen.getByText('Fresh library B item anonymous')).toBeTruthy();
  });

  it('ignores a page that resolves after unmount', async () => {
    let resolvePage!: (value: {
      items: Array<{
        id: string;
        name: string;
        type: string;
        artwork: Record<string, never>;
      }>;
      startIndex: number;
      totalRecordCount: number;
    }) => void;
    const pendingPage = new Promise<{
      items: Array<{
        id: string;
        name: string;
        type: string;
        artwork: Record<string, never>;
      }>;
      startIndex: number;
      totalRecordCount: number;
    }>((resolve) => {
      resolvePage = resolve;
    });
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockReturnValue(pendingPage);
    const view = render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'unmount-library-anonymous',
            name: 'Unmount library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'unmount-library-anonymous',
              name: 'Unmount library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:unmount-anonymous',
            focusKey: 'home-card:unmount-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    await waitFor(() => expect(getItemsPage).toHaveBeenCalledTimes(1));
    view.unmount();
    await act(async () => {
      resolvePage({
        items: [
          {
            id: 'unmounted-result-anonymous',
            name: 'Unmounted result anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 1,
      });
      await pendingPage;
    });

    expect(document.body.textContent).not.toContain('Unmounted result anonymous');
  });

  it('preloads in the final two measured visual rows without duplicate concurrent requests', async () => {
    let resolveNext!: (value: {
      items: never[];
      startIndex: number;
      totalRecordCount: number;
    }) => void;
    const pendingNext = new Promise<{
      items: never[];
      startIndex: number;
      totalRecordCount: number;
    }>((resolve) => {
      resolveNext = resolve;
    });
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValueOnce({
        items: [
          { id: 'prefetch-01-anonymous', name: 'Prefetch 01 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-02-anonymous', name: 'Prefetch 02 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-03-anonymous', name: 'Prefetch 03 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-04-anonymous', name: 'Prefetch 04 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-05-anonymous', name: 'Prefetch 05 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-06-anonymous', name: 'Prefetch 06 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-07-anonymous', name: 'Prefetch 07 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-08-anonymous', name: 'Prefetch 08 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-09-anonymous', name: 'Prefetch 09 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-10-anonymous', name: 'Prefetch 10 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-11-anonymous', name: 'Prefetch 11 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-12-anonymous', name: 'Prefetch 12 anonymous', type: 'Movie', artwork: {} },
          { id: 'prefetch-13-anonymous', name: 'Prefetch 13 anonymous', type: 'Movie', artwork: {} },
        ],
        startIndex: 0,
        totalRecordCount: 20,
      })
      .mockReturnValueOnce(pendingNext);

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'prefetch-library-anonymous',
            name: 'Prefetch library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'prefetch-library-anonymous',
              name: 'Prefetch library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:prefetch-anonymous',
            focusKey: 'home-card:prefetch-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const cards = await screen.findAllByRole('button', { name: /Open Prefetch/ });
    const grid = getGridElement();
    mockRect(grid, 128, 80, 760, 1120);
    cards.forEach((card, index) => {
      mockRect(
        card,
        136 + (index % 4) * 184,
        96 + Math.floor(index / 4) * 288,
        168,
        272,
      );
    });

    await updateLayoutsAndFocus(cards[4].getAttribute('data-focus-key') as string);
    expect(getItemsPage).toHaveBeenCalledTimes(1);

    await updateLayoutsAndFocus(cards[8].getAttribute('data-focus-key') as string);
    await waitFor(() => expect(getItemsPage).toHaveBeenCalledTimes(2));
    expect(getItemsPage.mock.calls[1]?.[1]).toBe(13);

    await updateLayoutsAndFocus(cards[9].getAttribute('data-focus-key') as string);
    await updateLayoutsAndFocus(cards[12].getAttribute('data-focus-key') as string);
    expect(getItemsPage).toHaveBeenCalledTimes(2);

    await act(async () => {
      resolveNext({ items: [], startIndex: 13, totalRecordCount: 20 });
      await pendingNext;
    });
  });
});

describe('LibraryPage focus and Back behavior', () => {
  it('uses real geometry for boundaries, nearest-column movement, and first-column Guide entry', async () => {
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValue({
        items: [
          { id: 'grid-01-anonymous', name: 'Grid 01 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-02-anonymous', name: 'Grid 02 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-03-anonymous', name: 'Grid 03 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-04-anonymous', name: 'Grid 04 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-05-anonymous', name: 'Grid 05 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-06-anonymous', name: 'Grid 06 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-07-anonymous', name: 'Grid 07 anonymous', type: 'Movie', artwork: {} },
          { id: 'grid-08-anonymous', name: 'Grid 08 anonymous', type: 'Movie', artwork: {} },
        ],
        startIndex: 0,
        totalRecordCount: 8,
      });

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'geometry-library-anonymous',
            name: 'Geometry library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'geometry-library-anonymous',
              name: 'Geometry library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:geometry-library-anonymous',
            focusKey: 'home-card:geometry-library-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const cards = await screen.findAllByRole('button', { name: /Open Grid/ });
    const guide = screen.getByRole('navigation', { name: 'Guide' });
    const home = screen.getByRole('button', { name: 'Home' });
    const activeLibrary = screen.getByRole('button', {
      name: 'Geometry library anonymous',
    });
    const logout = screen.getByRole('button', { name: 'Log out' });
    const guideScope = getScopeElement('home-guide');
    const gridScope = document.querySelector<HTMLElement>(
      '[data-focus-scope^="library-grid:"]',
    );
    if (!gridScope) {
      throw new Error('Library grid focus scope was not rendered.');
    }

    mockRectResolver(guideScope, () =>
      createRect(56, 0, guide.getAttribute('aria-expanded') === 'true' ? 248 : 72, 980),
    );
    mockRectResolver(home, () =>
      createRect(64, 96, guide.getAttribute('aria-expanded') === 'true' ? 232 : 56, 52),
    );
    mockRectResolver(activeLibrary, () =>
      createRect(64, 396, guide.getAttribute('aria-expanded') === 'true' ? 232 : 56, 52),
    );
    mockRectResolver(logout, () =>
      createRect(64, 696, guide.getAttribute('aria-expanded') === 'true' ? 232 : 56, 52),
    );
    mockRect(gridScope, 128, 80, 560, 900);
    cards.forEach((card, index) => {
      mockRect(
        card,
        136 + (index % 3) * 184,
        96 + Math.floor(index / 3) * 300,
        168,
        272,
      );
    });

    await updateLayoutsAndFocus(cards[3].getAttribute('data-focus-key') as string);
    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(home);
      expect(guide.getAttribute('aria-expanded')).toBe('true');
    });

    await waitForThrottleWindow();
    dispatchKey(home, 'ArrowRight', 39);
    await waitFor(() => expect(document.activeElement).toBe(cards[3]));

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => expect(document.activeElement).toBe(home));
    dispatchKey(home, 'Escape', 27);
    await waitFor(() => expect(document.activeElement).toBe(cards[3]));

    await updateLayoutsAndFocus(cards[2].getAttribute('data-focus-key') as string);
    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowRight', 39);
    await waitFor(() => expect(document.activeElement).toBe(cards[2]));

    await updateLayoutsAndFocus(cards[5].getAttribute('data-focus-key') as string);
    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowDown', 40);
    await waitFor(() => expect(document.activeElement).toBe(cards[7]));

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowDown', 40);
    await waitFor(() => expect(document.activeElement).toBe(cards[7]));

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowUp', 38);
    await waitFor(() => expect(document.activeElement).toBe(cards[4]));
  });

  it('reports the exact Home route origin when a grid card handles Escape', async () => {
    const onBack = vi.fn();
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValue({
        items: [
          {
            id: 'escape-item-anonymous',
            name: 'Escape item anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 1,
      });

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'escape-library-anonymous',
            name: 'Escape library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'escape-library-anonymous',
              name: 'Escape library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:escape-anonymous',
            focusKey: 'home-card:escape-anonymous',
          }}
          onBack={onBack}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const card = await screen.findByRole('button', {
      name: 'Open Escape item anonymous',
    });
    fireEvent.keyDown(card, { key: 'Escape' });

    expect(onBack).toHaveBeenCalledWith({
      scopeKey: 'home-row:escape-anonymous',
      focusKey: 'home-card:escape-anonymous',
    });
  });

  it('restores the same grid item and falls back within that grid if it is removed', async () => {
    const policy = createFocusNavigationPolicy();
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValueOnce({
        items: [
          { id: 'restore-a-anonymous', name: 'Restore A anonymous', type: 'Movie', artwork: {} },
          { id: 'restore-b-anonymous', name: 'Restore B anonymous', type: 'Movie', artwork: {} },
          { id: 'restore-c-anonymous', name: 'Restore C anonymous', type: 'Movie', artwork: {} },
        ],
        startIndex: 0,
        totalRecordCount: 3,
      })
      .mockResolvedValueOnce({
        items: [
          { id: 'restore-a-anonymous', name: 'Restore A anonymous', type: 'Movie', artwork: {} },
          { id: 'restore-b-anonymous', name: 'Restore B anonymous', type: 'Movie', artwork: {} },
          { id: 'restore-c-anonymous', name: 'Restore C anonymous', type: 'Movie', artwork: {} },
        ],
        startIndex: 0,
        totalRecordCount: 3,
      })
      .mockResolvedValueOnce({
        items: [
          { id: 'restore-a-anonymous', name: 'Restore A anonymous', type: 'Movie', artwork: {} },
          { id: 'restore-c-anonymous', name: 'Restore C anonymous', type: 'Movie', artwork: {} },
        ],
        startIndex: 0,
        totalRecordCount: 2,
      });
    const page = (
      restoreRequest?: {
        requestId: string;
        target: { scopeKey: string; focusKey: string };
      },
    ) => (
      <LibraryPage
        client={{ getItemsPage }}
        library={{
          id: 'restore-library-anonymous',
          name: 'Restore library anonymous',
          collectionType: 'movies',
        }}
        libraries={[
          {
            id: 'restore-library-anonymous',
            name: 'Restore library anonymous',
            collectionType: 'movies',
          },
        ]}
        restoreRequest={restoreRequest}
        routeOrigin={{
          scopeKey: 'home-row:restore-library-anonymous',
          focusKey: 'home-card:restore-library-anonymous',
        }}
        onBack={() => undefined}
        onLogout={() => undefined}
        onOpenLibrary={() => undefined}
        onOpenMedia={() => undefined}
      />
    );
    const view = render(
      <FocusProvider policy={policy}>{page()}</FocusProvider>,
    );
    const original = await screen.findByRole('button', {
      name: 'Open Restore B anonymous',
    });
    await updateLayoutsAndFocus(original.getAttribute('data-focus-key') as string);
    const target = {
      scopeKey: original.closest<HTMLElement>('[data-focus-scope]')?.dataset
        .focusScope as string,
      focusKey: original.getAttribute('data-focus-key') as string,
    };

    view.rerender(
      <FocusProvider policy={policy}>
        <p>Legacy details anonymous</p>
      </FocusProvider>,
    );
    view.rerender(
      <FocusProvider policy={policy}>
        {page({ requestId: 'restore-exact-event-anonymous', target })}
      </FocusProvider>,
    );
    const exact = await screen.findByRole('button', {
      name: 'Open Restore B anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(exact));

    view.rerender(
      <FocusProvider policy={policy}>
        <p>Legacy details after removal anonymous</p>
      </FocusProvider>,
    );
    view.rerender(
      <FocusProvider policy={policy}>
        {page({ requestId: 'restore-fallback-event-anonymous', target })}
      </FocusProvider>,
    );
    const fallback = await screen.findByRole('button', {
      name: 'Open Restore C anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(fallback));
    expect(screen.queryByText('Restore B anonymous')).toBeNull();
  });

  it('keeps empty normalized results reachable through the real Guide', async () => {
    const getItemsPage = vi
      .fn<EmbyWebClient['getItemsPage']>()
      .mockResolvedValue({
        items: [
          {
            id: '   ',
            name: 'Blank only item anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
        startIndex: 0,
        totalRecordCount: 1,
      });

    render(
      <FocusProvider>
        <LibraryPage
          client={{ getItemsPage }}
          library={{
            id: 'empty-library-anonymous',
            name: 'Empty library anonymous',
            collectionType: 'movies',
          }}
          libraries={[
            {
              id: 'empty-library-anonymous',
              name: 'Empty library anonymous',
              collectionType: 'movies',
            },
          ]}
          routeOrigin={{
            scopeKey: 'home-row:empty-library-anonymous',
            focusKey: 'home-card:empty-library-anonymous',
          }}
          onBack={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const empty = await screen.findByText(
      'No media available in Empty library anonymous.',
    );
    expect(empty.getAttribute('role')).toBe('status');
    await waitFor(() => {
      expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Home' }));
    });
    expect(screen.queryByText('Blank only item anonymous')).toBeNull();
  });

  it('keeps page code behind Noira and uses the poster-grid CSS contract', async () => {
    expect(libraryPageSource).not.toContain(
      '@noriginmedia/norigin-spatial-navigation',
    );
    expect(libraryPageSource).not.toMatch(
      /\b(?:navigateByDirection|setFocus|useFocusable)\b/,
    );

    const { readFileSync } = await vi.importActual<{
      readFileSync(path: string, encoding: 'utf8'): string;
    }>('node:fs');
    const { resolve } = await vi.importActual<{
      resolve(...paths: string[]): string;
    }>('node:path');
    const { cwd } = await vi.importActual<{ cwd(): string }>('node:process');
    const stylesSource = readFileSync(resolve(cwd(), 'src/styles.css'), 'utf8');

    expect(stylesSource).toMatch(
      /--library-grid-card-width:\s*\d+px/,
    );
    expect(stylesSource).toMatch(
      /\.library-page__grid\s*{[^}]*display:\s*grid[^}]*grid-template-columns:\s*repeat\(auto-fill,\s*minmax\(var\(--library-grid-card-width\),\s*1fr\)\)/s,
    );
    expect(stylesSource).toMatch(
      /\.library-page__grid\s*{[^}]*scroll-padding-block:\s*var\(--tv-safe\)/s,
    );
    expect(stylesSource).toMatch(
      /\.library-page__item\s*{[^}]*scroll-margin-block:\s*var\(--tv-safe\)/s,
    );
  });
});

function getGridElement(): HTMLElement {
  const grid = document.querySelector<HTMLElement>('[data-library-grid]');
  if (!grid) {
    throw new Error('Library grid was not rendered.');
  }

  return grid;
}

function getScopeElement(scopeKey: string): HTMLElement {
  const scope = document.querySelector<HTMLElement>(
    `[data-focus-scope="${scopeKey}"]`,
  );
  if (!scope) {
    throw new Error(`Focus scope ${scopeKey} was not rendered.`);
  }

  return scope;
}

function mockRect(
  element: HTMLElement,
  left: number,
  top: number,
  width: number,
  height: number,
): void {
  mockRectResolver(element, () => createRect(left, top, width, height));
}

function mockRectResolver(element: HTMLElement, resolve: () => DOMRect): void {
  vi.spyOn(element, 'getBoundingClientRect').mockImplementation(resolve);
}

function createRect(
  left: number,
  top: number,
  width: number,
  height: number,
): DOMRect {
  return {
    bottom: top + height,
    height,
    left,
    right: left + width,
    toJSON: () => ({}),
    top,
    width,
    x: left,
    y: top,
  };
}

async function updateLayoutsAndFocus(focusKey: string): Promise<void> {
  await act(async () => {
    await updateAllLayouts();
    await setFocus(focusKey);
  });
}

async function waitForThrottleWindow(): Promise<void> {
  await act(async () => {
    await new Promise((resolve) => window.setTimeout(resolve, 110));
  });
}

function dispatchKey(target: Window | HTMLElement, key: string, keyCode: number): void {
  fireEvent.keyDown(target, { code: key, key, keyCode, which: keyCode });
  fireEvent.keyUp(target, { code: key, key, keyCode, which: keyCode });
}
