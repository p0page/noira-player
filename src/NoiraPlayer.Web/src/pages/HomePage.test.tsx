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
import { FocusProvider } from '../focus/FocusProvider';
import { HomePage } from './HomePage';
import homePageSource from './HomePage.tsx?raw';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('HomePage', () => {
  it('gives default focus to the first card in the first non-empty row', async () => {
    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'empty-anonymous',
              title: 'Empty anonymous row',
              kind: 'resume',
              items: [],
            },
            {
              key: 'first-visible-anonymous',
              title: 'First visible anonymous row',
              kind: 'latest',
              items: [
                {
                  id: 'first-visible-item-anonymous',
                  name: 'First visible item anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const firstCard = screen.getByRole('button', {
      name: 'Open First visible item anonymous',
    });

    await waitFor(() => {
      expect(document.activeElement).toBe(firstCard);
    });
  });

  it('keeps title and metadata below artwork while resume progress stays inside it', () => {
    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'resume-anonymous',
              title: 'Resume anonymous row',
              kind: 'resume',
              items: [
                {
                  id: 'resume-item-anonymous',
                  name: 'Resume item anonymous',
                  type: 'Movie',
                  productionYear: 2031,
                  runtimeTicks: 100,
                  startPositionTicks: 25,
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const card = screen.getByRole('button', { name: 'Open Resume item anonymous' });
    const artwork = card.querySelector<HTMLElement>('[data-media-artwork]');
    const title = within(card).getByText('Resume item anonymous');
    const metadata = within(card).getByText(/2031/);
    if (!artwork) {
      throw new Error('Media artwork was not rendered.');
    }
    const progress = within(artwork).getByRole('progressbar');

    expect(artwork.contains(title)).toBe(false);
    expect(artwork.contains(metadata)).toBe(false);
    expect(artwork.contains(progress)).toBe(true);
    expect(artwork.compareDocumentPosition(title) & Node.DOCUMENT_POSITION_FOLLOWING)
      .not.toBe(0);
    expect(progress.getAttribute('aria-valuenow')).toBe('25');
    expect(card.getAttribute('data-card-variant')).toBe('wide');
  });

  it('replaces a failed image with the matte fallback and retries when its URL changes', () => {
    const view = render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'image-row-anonymous',
              title: 'Image row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'image-item-anonymous',
                  name: 'Image item anonymous',
                  type: 'Movie',
                  artwork: { primary: '/anonymous-one.jpg' },
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const card = screen.getByRole('button', { name: 'Open Image item anonymous' });
    const firstImage = card.querySelector('img');
    expect(firstImage?.getAttribute('src')).toBe('/anonymous-one.jpg');
    fireEvent.error(firstImage as HTMLImageElement);
    expect(card.querySelector('img')).toBeNull();
    expect(card.querySelector('.media-card__fallback')).not.toBeNull();

    view.rerender(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'image-row-anonymous',
              title: 'Image row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'image-item-anonymous',
                  name: 'Image item anonymous',
                  type: 'Movie',
                  artwork: { primary: '/anonymous-two.jpg' },
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    expect(
      screen
        .getByRole('button', { name: 'Open Image item anonymous' })
        .querySelector('img')
        ?.getAttribute('src'),
    ).toBe('/anonymous-two.jpg');
  });

  it('omits empty rows and defaults an empty Home page to the Guide', async () => {
    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'omitted-anonymous',
              title: 'Omitted anonymous row',
              kind: 'nextUp',
              items: [
                {
                  id: '   ',
                  name: 'Invalid anonymous item',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    expect(screen.queryByRole('heading', { name: 'Omitted anonymous row' })).toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Home' }));
    });
  });

  it('normalizes blank and colliding item identities before focus and callbacks', () => {
    const onOpenLibrary = vi.fn();
    const onOpenMedia = vi.fn();

    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'normalized-media-row-anonymous',
              title: 'Normalized media row anonymous',
              kind: 'latest',
              items: [
                {
                  id: '  shared-anonymous-id  ',
                  name: 'Original media anonymous',
                  type: 'Movie',
                  artwork: {},
                },
                {
                  id: 'shared-anonymous-id',
                  name: 'Duplicate media anonymous',
                  type: 'Movie',
                  artwork: {},
                },
                {
                  id: ' ',
                  name: 'Blank media anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'normalized-library-row-anonymous',
              title: 'Normalized library row anonymous',
              kind: 'libraries',
              items: [
                {
                  id: '  shared-anonymous-id  ',
                  name: 'Original library anonymous',
                  collectionType: 'movies',
                },
                {
                  id: 'shared-anonymous-id',
                  name: 'Duplicate library anonymous',
                  collectionType: 'tvshows',
                },
                {
                  id: '\t',
                  name: 'Blank library anonymous',
                  collectionType: 'music',
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={onOpenLibrary}
          onOpenMedia={onOpenMedia}
        />
      </FocusProvider>,
    );

    const mediaCard = screen.getByRole('button', {
      name: 'Open Original media anonymous',
    });
    const libraryCard = screen.getByRole('button', {
      name: 'Open Original library anonymous',
    });
    const guide = screen.getByRole('navigation', { name: 'Guide' });
    const guideLibrary = within(guide).getByRole('button', {
      name: 'Original library anonymous',
    });

    expect(screen.queryByText('Duplicate media anonymous')).toBeNull();
    expect(screen.queryByText('Blank media anonymous')).toBeNull();
    expect(screen.queryByText('Duplicate library anonymous')).toBeNull();
    expect(screen.queryByText('Blank library anonymous')).toBeNull();
    expect(mediaCard.getAttribute('data-item-id')).toBe('shared-anonymous-id');
    expect(libraryCard.getAttribute('data-item-id')).toBe('shared-anonymous-id');
    expect(mediaCard.getAttribute('data-focus-key')).not.toBe(
      libraryCard.getAttribute('data-focus-key'),
    );

    fireEvent.click(mediaCard);
    fireEvent.click(guideLibrary);

    expect(onOpenMedia).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'shared-anonymous-id' }),
      {
        scopeKey: 'home-row:normalized-media-row-anonymous',
        focusKey:
          'home-card:normalized-media-row-anonymous:media:shared-anonymous-id',
      },
    );
    expect(onOpenLibrary).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'shared-anonymous-id' }),
      {
        scopeKey: 'home-guide',
        focusKey: 'guide:library:shared-anonymous-id',
      },
    );
  });

  it('preserves row-scoped card keys, DOM identity, and focus when supplemental rows append', async () => {
    const { rerender } = render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'core-row-anonymous',
              title: 'Core row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'shared-item-anonymous',
                  name: 'Core item anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const coreCard = screen.getByRole('button', { name: 'Open Core item anonymous' });
    const coreFocusKey = coreCard.getAttribute('data-focus-key');
    expect(coreFocusKey).toContain('core-row-anonymous');

    await updateLayoutsAndFocus(coreFocusKey as string);
    expect(document.activeElement).toBe(coreCard);

    rerender(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'core-row-anonymous',
              title: 'Core row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'shared-item-anonymous',
                  name: 'Core item anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'supplemental-row-anonymous',
              title: 'Supplemental row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'shared-item-anonymous',
                  name: 'Supplemental item anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const supplementalCard = screen.getByRole('button', {
      name: 'Open Supplemental item anonymous',
    });

    expect(screen.getByRole('button', { name: 'Open Core item anonymous' })).toBe(coreCard);
    expect(document.activeElement).toBe(coreCard);
    expect(supplementalCard.getAttribute('data-focus-key')).toContain(
      'supplemental-row-anonymous',
    );
    expect(supplementalCard.getAttribute('data-focus-key')).not.toBe(coreFocusKey);
  });

  it('enters the Guide from the first card and restores that exact target by Right or Escape', async () => {
    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'geometry-row-anonymous',
              title: 'Geometry row anonymous',
              kind: 'resume',
              items: [
                {
                  id: 'geometry-item-anonymous',
                  name: 'Geometry item anonymous',
                  type: 'Episode',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const card = screen.getByRole('button', { name: 'Open Geometry item anonymous' });
    const home = screen.getByRole('button', { name: 'Home' });
    const logout = screen.getByRole('button', { name: 'Log out' });
    const guide = screen.getByRole('navigation', { name: 'Guide' });
    const guideScope = getScopeElement('home-guide');
    const rowScope = getScopeElement('home-row:geometry-row-anonymous');

    mockRectResolver(guideScope, () =>
      createRect(56, 0, guide.getAttribute('aria-expanded') === 'true' ? 248 : 72, 420),
    );
    mockRectResolver(home, () =>
      createRect(64, 0, guide.getAttribute('aria-expanded') === 'true' ? 232 : 56, 52),
    );
    mockRectResolver(logout, () =>
      createRect(64, 320, guide.getAttribute('aria-expanded') === 'true' ? 232 : 56, 52),
    );
    mockRect(rowScope, 128, 0, 600, 240);
    mockRect(card, 136, 0, 240, 135);

    await updateLayoutsAndFocus(card.getAttribute('data-focus-key') as string);
    expect(document.activeElement).toBe(card);

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(home);
      expect(guide.getAttribute('aria-expanded')).toBe('true');
    });
    expect(guideScope.getBoundingClientRect()).toMatchObject({ left: 56, width: 248 });
    expect(home.getBoundingClientRect().left).toBeGreaterThanOrEqual(64);
    expect(rowScope.getBoundingClientRect().left).toBe(128);

    await waitForThrottleWindow();
    dispatchKey(home, 'ArrowRight', 39);
    await waitFor(() => {
      expect(document.activeElement).toBe(card);
      expect(guide.getAttribute('aria-expanded')).toBe('false');
    });

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(home);
    });

    dispatchKey(home, 'Escape', 27);
    await waitFor(() => {
      expect(document.activeElement).toBe(card);
      expect(guide.getAttribute('aria-expanded')).toBe('false');
    });

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(home);
    });

    dispatchKey(home, 'Escape', 27);
    await waitFor(() => {
      expect(document.activeElement).toBe(card);
      expect(guide.getAttribute('aria-expanded')).toBe('false');
    });
  });

  it('restores the same target after Home remounts under the same Provider', async () => {
    const view = render(
      <FocusProvider>
        <HomePage
          key="first-home-mount-anonymous"
          rows={[
            {
              key: 'remount-row-anonymous',
              title: 'Remount row anonymous',
              kind: 'resume',
              items: [
                {
                  id: 'remount-item-anonymous',
                  name: 'Remount item anonymous',
                  type: 'Episode',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const firstCard = screen.getByRole('button', { name: 'Open Remount item anonymous' });
    const firstHome = screen.getByRole('button', { name: 'Home' });
    const firstLogout = screen.getByRole('button', { name: 'Log out' });
    const firstGuide = screen.getByRole('navigation', { name: 'Guide' });
    mockRectResolver(getScopeElement('home-guide'), () =>
      createRect(
        56,
        0,
        firstGuide.getAttribute('aria-expanded') === 'true' ? 248 : 72,
        420,
      ),
    );
    mockRectResolver(firstHome, () =>
      createRect(
        64,
        0,
        firstGuide.getAttribute('aria-expanded') === 'true' ? 232 : 56,
        52,
      ),
    );
    mockRectResolver(firstLogout, () =>
      createRect(
        64,
        320,
        firstGuide.getAttribute('aria-expanded') === 'true' ? 232 : 56,
        52,
      ),
    );
    mockRect(getScopeElement('home-row:remount-row-anonymous'), 128, 0, 600, 240);
    mockRect(firstCard, 136, 0, 240, 135);

    await updateLayoutsAndFocus(firstCard.getAttribute('data-focus-key') as string);
    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(firstHome);
    });

    dispatchKey(firstHome, 'Escape', 27);
    await waitFor(() => {
      expect(document.activeElement).toBe(firstCard);
    });

    view.rerender(
      <FocusProvider>
        <HomePage
          key="second-home-mount-anonymous"
          rows={[
            {
              key: 'remount-row-anonymous',
              title: 'Remount row anonymous',
              kind: 'resume',
              items: [
                {
                  id: 'remount-item-anonymous',
                  name: 'Remount item anonymous',
                  type: 'Episode',
                  artwork: {},
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const remountedCard = screen.getByRole('button', {
      name: 'Open Remount item anonymous',
    });
    const remountedHome = screen.getByRole('button', { name: 'Home' });
    const remountedLogout = screen.getByRole('button', { name: 'Log out' });
    const remountedGuide = screen.getByRole('navigation', { name: 'Guide' });
    expect(remountedCard).not.toBe(firstCard);
    mockRectResolver(getScopeElement('home-guide'), () =>
      createRect(
        56,
        0,
        remountedGuide.getAttribute('aria-expanded') === 'true' ? 248 : 72,
        420,
      ),
    );
    mockRectResolver(remountedHome, () =>
      createRect(
        64,
        0,
        remountedGuide.getAttribute('aria-expanded') === 'true' ? 232 : 56,
        52,
      ),
    );
    mockRectResolver(remountedLogout, () =>
      createRect(
        64,
        320,
        remountedGuide.getAttribute('aria-expanded') === 'true' ? 232 : 56,
        52,
      ),
    );
    mockRect(getScopeElement('home-row:remount-row-anonymous'), 128, 0, 600, 240);
    mockRect(remountedCard, 136, 0, 240, 135);

    await updateLayoutsAndFocus(remountedCard.getAttribute('data-focus-key') as string);
    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(remountedHome);
    });

    dispatchKey(remountedHome, 'Escape', 27);
    await waitFor(() => {
      expect(document.activeElement).toBe(remountedCard);
    });
  });

  it('accepts external restore requests for exact Home-card and Guide origins', async () => {
    const view = render(
      <FocusProvider>
        <HomePage
          key="external-home-card-anonymous"
          restoreRequest={{
            requestId: 'external-home-card-event-anonymous',
            target: {
              scopeKey: 'home-row:external-row-anonymous',
              focusKey:
                'home-card:external-row-anonymous:media:external-b-anonymous',
            },
          }}
          rows={[
            {
              key: 'external-row-anonymous',
              title: 'External row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'external-a-anonymous',
                  name: 'External A anonymous',
                  type: 'Movie',
                  artwork: {},
                },
                {
                  id: 'external-b-anonymous',
                  name: 'External B anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'external-libraries-anonymous',
              title: 'External libraries anonymous',
              kind: 'libraries',
              items: [
                {
                  id: 'external-library-anonymous',
                  name: 'External library anonymous',
                  collectionType: 'movies',
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const card = await screen.findByRole('button', {
      name: 'Open External B anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(card));

    view.rerender(
      <FocusProvider>
        <HomePage
          key="external-guide-anonymous"
          restoreRequest={{
            requestId: 'external-guide-event-anonymous',
            target: {
              scopeKey: 'home-guide',
              focusKey: 'guide:library:external-library-anonymous',
            },
          }}
          rows={[
            {
              key: 'external-row-anonymous',
              title: 'External row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'external-a-anonymous',
                  name: 'External A anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'external-libraries-anonymous',
              title: 'External libraries anonymous',
              kind: 'libraries',
              items: [
                {
                  id: 'external-library-anonymous',
                  name: 'External library anonymous',
                  collectionType: 'movies',
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={() => undefined}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const guideLibrary = screen.getByRole('button', {
      name: 'External library anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(guideLibrary));
  });

  it('wires Home, real library, media, and logout selections to callbacks', () => {
    const onHome = vi.fn();
    const onLogout = vi.fn();
    const onOpenLibrary = vi.fn();
    const onOpenMedia = vi.fn();

    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'media-row-anonymous',
              title: 'Media row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'media-item-anonymous',
                  name: 'Media item anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'library-row-anonymous',
              title: 'Library row anonymous',
              kind: 'libraries',
              items: [
                {
                  id: 'library-item-anonymous',
                  name: 'Library item anonymous',
                  collectionType: 'movies',
                },
              ],
            },
          ]}
          onHome={onHome}
          onLogout={onLogout}
          onOpenLibrary={onOpenLibrary}
          onOpenMedia={onOpenMedia}
        />
      </FocusProvider>,
    );

    const home = screen.getByRole('button', { name: 'Home' });
    const guide = screen.getByRole('navigation', { name: 'Guide' });

    expect(home.getAttribute('aria-current')).toBe('page');
    expect(document.activeElement).not.toBe(home);
    expect(within(guide).getAllByRole('button')).toHaveLength(3);

    fireEvent.click(home);
    fireEvent.click(screen.getByRole('button', { name: 'Library item anonymous' }));
    fireEvent.click(screen.getByRole('button', { name: 'Open Media item anonymous' }));
    fireEvent.click(screen.getByRole('button', { name: 'Log out' }));

    expect(onHome).toHaveBeenCalledTimes(1);
    expect(onOpenLibrary).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'library-item-anonymous' }),
      {
        scopeKey: 'home-guide',
        focusKey: 'guide:library:library-item-anonymous',
      },
    );
    expect(onOpenMedia).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'media-item-anonymous' }),
      {
        scopeKey: 'home-row:media-row-anonymous',
        focusKey: 'home-card:media-row-anonymous:media:media-item-anonymous',
      },
    );
    expect(onLogout).toHaveBeenCalledTimes(1);
  });

  it('uses the pre-Guide content target when a Guide library is selected', async () => {
    const onOpenLibrary = vi.fn();

    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'guide-origin-media-row-anonymous',
              title: 'Guide origin media row anonymous',
              kind: 'latest',
              items: [
                {
                  id: 'guide-origin-media-anonymous',
                  name: 'Guide origin media anonymous',
                  type: 'Movie',
                  artwork: {},
                },
              ],
            },
            {
              key: 'guide-origin-library-row-anonymous',
              title: 'Guide origin library row anonymous',
              kind: 'libraries',
              items: [
                {
                  id: 'guide-origin-library-anonymous',
                  name: 'Guide origin library anonymous',
                  collectionType: 'movies',
                },
              ],
            },
          ]}
          onHome={() => undefined}
          onLogout={() => undefined}
          onOpenLibrary={onOpenLibrary}
          onOpenMedia={() => undefined}
        />
      </FocusProvider>,
    );

    const media = screen.getByRole('button', {
      name: 'Open Guide origin media anonymous',
    });
    await act(async () => {
      media.focus();
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Guide origin library anonymous' }),
    );

    expect(onOpenLibrary).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'guide-origin-library-anonymous' }),
      {
        scopeKey: 'home-row:guide-origin-media-row-anonymous',
        focusKey:
          'home-card:guide-origin-media-row-anonymous:media:guide-origin-media-anonymous',
      },
    );
  });

  it('keeps page code on the Noira focus API boundary', () => {
    expect(homePageSource).not.toContain('@noriginmedia/norigin-spatial-navigation');
    expect(homePageSource).not.toMatch(
      /\b(?:navigateByDirection|setFocus|useFocusable)\b/,
    );
  });

  it('keeps focused Home targets inside the TV safe area by CSS contract', async () => {
    const { readFileSync } = await vi.importActual<{
      readFileSync(path: string, encoding: 'utf8'): string;
    }>('node:fs');
    const { resolve } = await vi.importActual<{
      resolve(...paths: string[]): string;
    }>('node:path');
    const { cwd } = await vi.importActual<{ cwd(): string }>('node:process');
    const stylesSource = readFileSync(resolve(cwd(), 'src/styles.css'), 'utf8');

    expect(stylesSource).toMatch(
      /html,\s*body,\s*#root\s*{[^}]*scroll-padding-block:\s*var\(--tv-safe\)/s,
    );
    expect(stylesSource).toMatch(
      /\.media-row\s*{[^}]*scroll-margin-block:\s*var\(--tv-safe\)/s,
    );
    expect(stylesSource).toMatch(
      /\.media-card\s*{[^}]*scroll-margin-block:\s*var\(--tv-safe\)/s,
    );
    expect(stylesSource).toMatch(
      /\.guide\s*{[^}]*left:\s*var\(--tv-safe\)[^}]*width:\s*var\(--guide-collapsed\)/s,
    );
    expect(stylesSource).toMatch(
      /\.guide::before\s*{[^}]*right:\s*100%[^}]*width:\s*var\(--tv-safe\)[^}]*pointer-events:\s*none/s,
    );
    expect(stylesSource).toMatch(
      /\.home-page\s*{[^}]*margin-left:\s*calc\(var\(--tv-safe\) \+ var\(--guide-collapsed\)\)/s,
    );
  });
});

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

function mockRectResolver(
  element: HTMLElement,
  resolve: () => DOMRect,
): void {
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
