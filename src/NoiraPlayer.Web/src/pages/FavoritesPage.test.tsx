// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BrowseShell } from '../components/BrowseShell';
import { FocusProvider } from '../focus/FocusProvider';
import type { MediaItem } from '../types';
import {
  FavoritesPage,
  type FavoritesPageClient,
  getFavoritesFocusKey,
  getFavoritesScopeKey,
} from './FavoritesPage';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('FavoritesPage', () => {
  it('loads real favorites through the injected client and renders a focus grid', async () => {
    const item = createItem();
    const getFavoriteItems = vi
      .fn<FavoritesPageClient['getFavoriteItems']>()
      .mockResolvedValue([item]);
    renderFavorites({ getFavoriteItems });

    expect(screen.getByRole('status').textContent).toBe('Loading favorites...');
    const card = await screen.findByRole('button', {
      name: 'Open Anonymous favorite',
    });

    expect(getFavoriteItems).toHaveBeenCalledTimes(1);
    expect(card.closest('[data-focus-scope]')?.getAttribute('data-focus-scope')).toBe(
      getFavoritesScopeKey(),
    );
  });

  it('opens a favorite with its stable focus target', async () => {
    const item = createItem();
    const onOpenMedia = vi.fn();
    renderFavorites({ getFavoriteItems: async () => [item] }, onOpenMedia);

    fireEvent.click(
      await screen.findByRole('button', { name: 'Open Anonymous favorite' }),
    );

    expect(onOpenMedia).toHaveBeenCalledWith(item, {
      scopeKey: getFavoritesScopeKey(),
      focusKey: getFavoritesFocusKey(item.id),
    });
  });

  it('shows an empty state when the account has no favorite media', async () => {
    renderFavorites({ getFavoriteItems: async () => [] });

    expect(await screen.findByText('No favorites yet.')).toBeTruthy();
  });

  it('shows a focusable retry action after an error', async () => {
    const getFavoriteItems = vi
      .fn<FavoritesPageClient['getFavoriteItems']>()
      .mockRejectedValueOnce(new Error('private server detail'))
      .mockResolvedValueOnce([createItem()]);
    renderFavorites({ getFavoriteItems });

    expect((await screen.findByRole('alert')).textContent).toBe(
      'Favorites could not be loaded.',
    );
    expect(screen.queryByText('private server detail')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Retry favorites' }));
    expect(
      await screen.findByRole('button', { name: 'Open Anonymous favorite' }),
    ).toBeTruthy();
    expect(getFavoriteItems).toHaveBeenCalledTimes(2);
  });
});

function renderFavorites(
  client: FavoritesPageClient,
  onOpenMedia: (item: MediaItem, origin: { scopeKey: string; focusKey: string }) => void =
    () => undefined,
) {
  return render(
    <FocusProvider>
      <BrowseShell
        activeRoute={{ kind: 'favorites' }}
        onFavorites={() => undefined}
        onHome={() => undefined}
        onLogout={() => undefined}
        onNavigateBack={() => undefined}
        onNativeBack={() => undefined}
        onSearch={() => undefined}
        routeStack={[
          { kind: 'home' },
          {
            kind: 'favorites',
            origin: { scopeKey: 'home-guide', focusKey: 'guide:favorites' },
          },
        ]}
      >
        <FavoritesPage client={client} onOpenMedia={onOpenMedia} />
      </BrowseShell>
    </FocusProvider>,
  );
}

function createItem(): MediaItem {
  return {
    id: 'favorite-anonymous',
    name: 'Anonymous favorite',
    type: 'Movie',
    artwork: {},
  };
}
