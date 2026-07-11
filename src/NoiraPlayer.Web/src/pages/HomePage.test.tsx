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

  it('omits empty rows and defaults an empty Home page to the Guide', async () => {
    render(
      <FocusProvider>
        <HomePage
          rows={[
            {
              key: 'omitted-anonymous',
              title: 'Omitted anonymous row',
              kind: 'nextUp',
              items: [],
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

    mockRect(guideScope, 0, 0, 72, 420);
    mockRect(home, 0, 0, 72, 52);
    mockRect(logout, 0, 320, 72, 52);
    mockRect(rowScope, 280, 0, 600, 240);
    mockRect(card, 300, 0, 240, 135);

    await updateLayoutsAndFocus(card.getAttribute('data-focus-key') as string);
    expect(document.activeElement).toBe(card);

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowLeft', 37);
    await waitFor(() => {
      expect(document.activeElement).toBe(home);
      expect(guide.getAttribute('aria-expanded')).toBe('true');
    });

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowRight', 39);
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
    );
    expect(onOpenMedia).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'media-item-anonymous' }),
    );
    expect(onLogout).toHaveBeenCalledTimes(1);
  });

  it('keeps page code on the Noira focus API boundary', () => {
    expect(homePageSource).not.toContain('@noriginmedia/norigin-spatial-navigation');
    expect(homePageSource).not.toMatch(
      /\b(?:navigateByDirection|setFocus|useFocusable)\b/,
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
  vi.spyOn(element, 'getBoundingClientRect').mockReturnValue({
    bottom: top + height,
    height,
    left,
    right: left + width,
    toJSON: () => ({}),
    top,
    width,
    x: left,
    y: top,
  });
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
