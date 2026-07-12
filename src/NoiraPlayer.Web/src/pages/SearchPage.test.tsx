// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BrowseShell } from '../components/BrowseShell';
import { FocusProvider } from '../focus/FocusProvider';
import type { MediaItem } from '../types';
import {
  getSearchInputFocusKey,
  getSearchResultsFocusKey,
  getSearchResultsScopeKey,
  SearchPage,
  type SearchPageClient,
} from './SearchPage';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('SearchPage', () => {
  it('focuses the search input and submits a trimmed query', async () => {
    let resolveSearch!: (items: readonly MediaItem[]) => void;
    const searchItems = vi.fn<SearchPageClient['searchItems']>().mockReturnValue(
      new Promise((resolve) => {
        resolveSearch = resolve;
      }),
    );

    renderSearch({ searchItems });

    const input = await screen.findByRole('searchbox', { name: 'Search media' });
    await waitFor(() => expect(document.activeElement).toBe(input));
    expect(input).toHaveProperty('readOnly', true);
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(input).toHaveProperty('readOnly', false);
    expect(searchItems).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '  anonymous title  ' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(searchItems).toHaveBeenCalledWith('anonymous title');
    expect(screen.getByRole('status').textContent).toBe('Searching...');

    resolveSearch([createItem()]);
    expect(
      await screen.findByRole('button', { name: 'Open Anonymous search result' }),
    ).toBeTruthy();
  });

  it('opens a result with its stable focus target', async () => {
    const item = createItem();
    const onOpenMedia = vi.fn();
    renderSearch({ searchItems: async () => [item] }, onOpenMedia);

    const input = await screen.findByRole('searchbox', { name: 'Search media' });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.change(input, { target: { value: 'anonymous' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.click(
      await screen.findByRole('button', { name: 'Open Anonymous search result' }),
    );

    expect(onOpenMedia).toHaveBeenCalledWith(item, {
      scopeKey: getSearchResultsScopeKey(),
      focusKey: getSearchResultsFocusKey(item.id),
    });
  });

  it('shows an empty result state without submitting blank input', async () => {
    const searchItems = vi.fn<SearchPageClient['searchItems']>().mockResolvedValue([]);
    renderSearch({ searchItems });

    const input = await screen.findByRole('searchbox', { name: 'Search media' });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(searchItems).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'missing anonymous' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(await screen.findByText('No results found.')).toBeTruthy();
  });

  it('shows a focusable retry action after an error', async () => {
    const searchItems = vi
      .fn<SearchPageClient['searchItems']>()
      .mockRejectedValueOnce(new Error('private server detail'))
      .mockResolvedValueOnce([createItem()]);
    renderSearch({ searchItems });

    const input = await screen.findByRole('searchbox', { name: 'Search media' });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.change(input, { target: { value: 'anonymous' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect((await screen.findByRole('alert')).textContent).toBe(
      'Search could not be completed.',
    );
    expect(screen.queryByText('private server detail')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Retry search' }));
    expect(
      await screen.findByRole('button', { name: 'Open Anonymous search result' }),
    ).toBeTruthy();
    expect(searchItems).toHaveBeenCalledTimes(2);
  });

  it('registers the input and result cards in focus scopes', async () => {
    renderSearch({ searchItems: async () => [createItem()] });

    const input = await screen.findByRole('searchbox', { name: 'Search media' });
    expect(input.getAttribute('data-focus-key')).toBe(getSearchInputFocusKey());
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.change(input, { target: { value: 'anonymous' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    const card = await screen.findByRole('button', {
      name: 'Open Anonymous search result',
    });
    expect(card.closest('[data-focus-scope]')?.getAttribute('data-focus-scope')).toBe(
      getSearchResultsScopeKey(),
    );
  });
});

function renderSearch(
  client: SearchPageClient,
  onOpenMedia: (item: MediaItem, origin: { scopeKey: string; focusKey: string }) => void =
    () => undefined,
) {
  return render(
    <FocusProvider>
      <BrowseShell
        activeRoute={{ kind: 'search' }}
        onFavorites={() => undefined}
        onHome={() => undefined}
        onLogout={() => undefined}
        onNavigateBack={() => undefined}
        onNativeBack={() => undefined}
        onSearch={() => undefined}
        routeStack={[
          { kind: 'home' },
          {
            kind: 'search',
            origin: { scopeKey: 'home-guide', focusKey: 'guide:search' },
          },
        ]}
      >
        <SearchPage client={client} onOpenMedia={onOpenMedia} />
      </BrowseShell>
    </FocusProvider>,
  );
}

function createItem(): MediaItem {
  return {
    id: 'search-result-anonymous',
    name: 'Anonymous search result',
    type: 'Movie',
    artwork: {},
  };
}
