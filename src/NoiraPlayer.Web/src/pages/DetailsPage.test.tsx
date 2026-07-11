// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FocusProvider } from '../focus/FocusProvider';
import type { MediaItem } from '../types';
import {
  DetailsPage,
  getDetailsActionsScopeKey,
  getDetailsPlayFocusKey,
  resolveDetailsAtmosphereUrl,
} from './DetailsPage';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('DetailsPage focus and actions', () => {
  it('gives default DOM focus to the Play action in a stable scope', async () => {
    renderDetails(createItem());

    const play = await screen.findByRole('button', { name: 'Play' });
    await waitFor(() => expect(document.activeElement).toBe(play));

    expect(play.getAttribute('data-focus-key')).toBe(getDetailsPlayFocusKey());
    expect(play.closest('[data-focus-scope]')?.getAttribute('data-focus-scope')).toBe(
      getDetailsActionsScopeKey(),
    );
  });

  it('shows Resume when playback has started', async () => {
    renderDetails(createItem({ startPositionTicks: 120_000_000 }));

    expect(await screen.findByRole('button', { name: 'Resume' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Play' })).toBeNull();
  });

  it('explicitly restores Play focus for each matching restore request', async () => {
    const item = createItem();
    const view = renderDetails(item);
    const play = await screen.findByRole('button', { name: 'Play' });
    await waitFor(() => expect(document.activeElement).toBe(play));

    const outside = document.createElement('button');
    document.body.append(outside);
    outside.focus();
    expect(document.activeElement).toBe(outside);

    view.rerender(page(item, 'details-restore-one-anonymous'));
    await waitFor(() => expect(document.activeElement).toBe(play));

    outside.focus();
    view.rerender(page(item, 'details-restore-two-anonymous'));
    await waitFor(() => expect(document.activeElement).toBe(play));
    outside.remove();
  });

  it('handles Escape at page level and calls onBack once', async () => {
    const onBack = vi.fn();
    renderDetails(createItem(), { onBack });
    const play = await screen.findByRole('button', { name: 'Play' });

    fireEvent.keyDown(play, { key: 'Escape' });

    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('passes the original item object to onPlay', async () => {
    const item = createItem();
    const onPlay = vi.fn();
    renderDetails(item, { onPlay });

    fireEvent.click(await screen.findByRole('button', { name: 'Play' }));

    expect(onPlay).toHaveBeenCalledTimes(1);
    expect(onPlay.mock.calls[0]?.[0]).toBe(item);
  });
});

describe('DetailsPage content and atmosphere', () => {
  it('renders only available media facts and formats runtime', async () => {
    renderDetails(
      createItem({
        productionYear: 2031,
        runtimeTicks: 75_000_000_000,
        overview: 'Anonymous overview used only by this unit test.',
      }),
    );

    expect(await screen.findByRole('heading', { name: 'Anonymous detail title' })).toBeTruthy();
    expect(screen.getByText('2031')).toBeTruthy();
    expect(screen.getByText('Movie')).toBeTruthy();
    expect(screen.getByText('2h 5m')).toBeTruthy();
    expect(screen.getByText('Anonymous overview used only by this unit test.')).toBeTruthy();
  });

  it('does not invent missing year, runtime, or overview content', async () => {
    renderDetails(createItem());

    const metadata = await screen.findByLabelText('Media details');
    expect(metadata.textContent).toBe('Movie');
    expect(document.querySelector('.details-page__overview')).toBeNull();
  });

  it('resolves atmosphere in backdrop, thumb, banner, primary order', () => {
    expect(
      resolveDetailsAtmosphereUrl({
        backdrop: 'backdrop-anonymous.jpg',
        thumb: 'thumb-anonymous.jpg',
        banner: 'banner-anonymous.jpg',
        primary: 'primary-anonymous.jpg',
      }),
    ).toBe('backdrop-anonymous.jpg');
    expect(
      resolveDetailsAtmosphereUrl({
        thumb: 'thumb-anonymous.jpg',
        banner: 'banner-anonymous.jpg',
        primary: 'primary-anonymous.jpg',
      }),
    ).toBe('thumb-anonymous.jpg');
    expect(
      resolveDetailsAtmosphereUrl({
        banner: 'banner-anonymous.jpg',
        primary: 'primary-anonymous.jpg',
      }),
    ).toBe('banner-anonymous.jpg');
    expect(resolveDetailsAtmosphereUrl({ primary: 'primary-anonymous.jpg' })).toBe(
      'primary-anonymous.jpg',
    );
  });

  it('uses a matte fallback without an image when artwork is unavailable', async () => {
    renderDetails(createItem({ artwork: {} }));

    const atmosphere = await screen.findByTestId('details-atmosphere');
    expect(atmosphere.getAttribute('data-has-artwork')).toBe('false');
    expect(atmosphere.style.backgroundImage).toBe('');
    expect(atmosphere.querySelector('img')).toBeNull();
    expect(resolveDetailsAtmosphereUrl({})).toBeUndefined();
  });
});

function createItem(overrides: Partial<MediaItem> = {}): MediaItem {
  return {
    id: 'details-item-anonymous',
    name: 'Anonymous detail title',
    type: 'Movie',
    artwork: { backdrop: 'details-backdrop-anonymous.jpg' },
    ...overrides,
  };
}

function page(item: MediaItem, restoreRequestId?: string) {
  return (
    <FocusProvider>
      <DetailsPage
        item={item}
        restoreRequest={
          restoreRequestId
            ? {
                requestId: restoreRequestId,
                target: {
                  scopeKey: getDetailsActionsScopeKey(),
                  focusKey: getDetailsPlayFocusKey(),
                },
              }
            : null
        }
        onBack={() => undefined}
        onPlay={() => undefined}
      />
    </FocusProvider>
  );
}

function renderDetails(
  item: MediaItem,
  callbacks: {
    onBack?: () => void;
    onPlay?: (item: MediaItem) => void;
  } = {},
) {
  return render(
    <FocusProvider>
      <DetailsPage
        item={item}
        onBack={callbacks.onBack ?? (() => undefined)}
        onPlay={callbacks.onPlay ?? (() => undefined)}
      />
    </FocusProvider>,
  );
}
