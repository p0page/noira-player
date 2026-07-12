// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useEffect, useMemo, useState } from 'react';
import type { ComponentProps } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Focusable } from '../focus/Focusable';
import { FocusProvider } from '../focus/FocusProvider';
import { FocusScope } from '../focus/FocusScope';
import {
  BrowseShell,
  useBrowseShellContentRestoreRequest,
  useBrowseShellTextInputEngagement,
} from './BrowseShell';

afterEach(() => {
  cleanup();
  Reflect.deleteProperty(window, 'chrome');
  vi.restoreAllMocks();
});

describe('BrowseShell', () => {
  it('keeps an overlay-only Guide out of the focus graph until Menu opens it', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    installWebView((handler) => {
      messageHandler = handler;
    });

    renderShell({ guideOverlayOnly: true });
    expect(screen.queryByRole('navigation', { name: 'Guide' })).toBeNull();
    const content = screen.getByRole('button', { name: 'Content target' });
    await waitFor(() => expect(document.activeElement).toBe(content));

    messageHandler?.({ data: input(1, 'menu') });

    const guide = await screen.findByRole('navigation', { name: 'Guide' });
    expect(guide.getAttribute('aria-expanded')).toBe('true');

    messageHandler?.({ data: input(2, 'menu') });
    await waitFor(() => {
      expect(screen.queryByRole('navigation', { name: 'Guide' })).toBeNull();
      expect(document.activeElement).toBe(content);
    });
  });

  it('toggles one controlled Guide and restores the exact content target', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    Object.defineProperty(window, 'chrome', {
      configurable: true,
      value: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    });

    renderShell();
    const content = screen.getByRole('button', { name: 'Content target' });
    await waitFor(() => expect(document.activeElement).toBe(content));

    messageHandler?.({ data: input(1, 'menu') });
    const guide = screen.getByRole('navigation', { name: 'Guide' });
    await waitFor(() => {
      expect(guide.getAttribute('aria-expanded')).toBe('true');
      expect(document.activeElement).toBe(
        screen.getByRole('button', { name: 'Home' }),
      );
    });

    messageHandler?.({ data: input(2, 'menu') });
    await waitFor(() => {
      expect(guide.getAttribute('aria-expanded')).toBe('false');
      expect(document.activeElement).toBe(content);
    });
    expect(screen.getAllByRole('navigation', { name: 'Guide' })).toHaveLength(1);
  });

  it('routes Back before native Back and preserves the route origin', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    Object.defineProperty(window, 'chrome', {
      configurable: true,
      value: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    });
    const onNavigateBack = vi.fn();
    const onNativeBack = vi.fn();

    renderShell({
      onNavigateBack,
      onNativeBack,
      routeStack: [
        { kind: 'home' },
        {
          kind: 'search',
          origin: { scopeKey: 'home-guide', focusKey: 'guide:search' },
        },
      ],
    });

    messageHandler?.({ data: input(1, 'back') });

    expect(onNavigateBack).toHaveBeenCalledWith({
      scopeKey: 'home-guide',
      focusKey: 'guide:search',
    });
    expect(onNativeBack).not.toHaveBeenCalled();
  });

  it('delegates Back from Home to the native host', () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    Object.defineProperty(window, 'chrome', {
      configurable: true,
      value: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    });
    const onNativeBack = vi.fn();

    renderShell({ onNativeBack });
    messageHandler?.({ data: input(1, 'back') });

    expect(onNativeBack).toHaveBeenCalledOnce();
  });

  it('does not treat a merely focused input as an engaged text-input layer', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    Object.defineProperty(window, 'chrome', {
      configurable: true,
      value: {
        webview: {
          addEventListener: vi.fn(
            (_type: 'message', handler: (event: { data: unknown }) => void) => {
              messageHandler = handler;
            },
          ),
          removeEventListener: vi.fn(),
          postMessage: vi.fn(),
        },
      },
    });
    const onNavigateBack = vi.fn();
    renderTextInputShell(onNavigateBack);
    const inputElement = screen.getByRole('textbox', { name: 'Search query' });
    inputElement.focus();

    messageHandler?.({ data: input(1, 'back') });

    expect(onNavigateBack).toHaveBeenCalledOnce();
  });

  it('exits explicit text engagement and restores the pre-engagement target', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    installWebView((handler) => {
      messageHandler = handler;
    });
    const onNavigateBack = vi.fn();
    renderTextInputShell(onNavigateBack);
    const beforeInput = screen.getByRole('button', { name: 'Before search' });
    const inputElement = screen.getByRole('textbox', { name: 'Search query' });
    await waitFor(() => expect(document.activeElement).toBe(beforeInput));
    inputElement.focus();
    fireEvent.keyDown(inputElement, { key: 'Enter' });
    expect(inputElement).toHaveProperty('readOnly', false);

    messageHandler?.({ data: input(1, 'back') });

    await waitFor(() => expect(document.activeElement).toBe(beforeInput));
    expect(inputElement).toHaveProperty('readOnly', true);
    expect(onNavigateBack).not.toHaveBeenCalled();
  });

  it('preserves text while Menu exits engagement and opens Guide', async () => {
    let messageHandler: ((event: { data: unknown }) => void) | undefined;
    installWebView((handler) => {
      messageHandler = handler;
    });
    renderTextInputShell(vi.fn());
    const inputElement = screen.getByRole('textbox', { name: 'Search query' });
    inputElement.focus();
    fireEvent.keyDown(inputElement, { key: 'Enter' });
    fireEvent.change(inputElement, { target: { value: 'updated query' } });

    messageHandler?.({ data: input(1, 'menu') });
    await waitFor(() => {
      expect(
        screen.getByRole('navigation', { name: 'Guide' }).getAttribute(
          'aria-expanded',
        ),
      ).toBe('true');
    });
    expect((inputElement as HTMLInputElement).value).toBe('updated query');
    expect(inputElement).toHaveProperty('readOnly', true);
  });

  it.each(['modalLayer', 'overlayLayer'] as const)(
    'restores decision.restoreTarget when closing %s',
    async (layerName) => {
      let messageHandler: ((event: { data: unknown }) => void) | undefined;
      installWebView((handler) => {
        messageHandler = handler;
      });
      const onClose = vi.fn();
      renderShell({
        [layerName]: {
          onClose,
          returnTarget: {
            scopeKey: 'content-scope',
            focusKey: 'content-target',
          },
        },
      });
      const secondary = screen.getByRole('button', { name: 'Secondary target' });
      secondary.focus();

      messageHandler?.({ data: input(1, 'back') });

      expect(onClose).toHaveBeenCalledOnce();
      await waitFor(() => {
        expect(document.activeElement).toBe(
          screen.getByRole('button', { name: 'Content target' }),
        );
      });
    },
  );
});

function renderShell(
  overrides: Partial<Omit<ComponentProps<typeof BrowseShell>, 'children'>> = {},
) {
  const props: Omit<ComponentProps<typeof BrowseShell>, 'children'> = {
    activeRoute: { kind: 'home' },
    onFavorites: () => undefined,
    onHome: () => undefined,
    onLogout: () => undefined,
    onNavigateBack: () => undefined,
    onNativeBack: () => undefined,
    onSearch: () => undefined,
    routeStack: [{ kind: 'home' }],
    ...overrides,
  };

  return render(
    <FocusProvider>
      <BrowseShell {...props}>
        <ShellAwareContent />
      </BrowseShell>
    </FocusProvider>,
  );
}

function ShellAwareContent() {
  const restoreRequest = useBrowseShellContentRestoreRequest();
  return (
    <main>
      <FocusScope
        defaultFocusKey="content-target"
        orderedKeys={['content-target', 'secondary-target']}
        restoreFocusKey={
          restoreRequest?.target.scopeKey === 'content-scope'
            ? restoreRequest.target.focusKey
            : undefined
        }
        restoreRequestId={
          restoreRequest?.target.scopeKey === 'content-scope'
            ? restoreRequest.requestId
            : undefined
        }
        scopeKey="content-scope"
      >
        <Focusable focusKey="content-target" onSelect={() => undefined}>
          Content target
        </Focusable>
        <Focusable focusKey="secondary-target" onSelect={() => undefined}>
          Secondary target
        </Focusable>
      </FocusScope>
    </main>
  );
}

function renderTextInputShell(onNavigateBack: () => void) {
  return render(
    <FocusProvider>
      <BrowseShell
        activeRoute={{ kind: 'search' }}
        onFavorites={() => undefined}
        onHome={() => undefined}
        onLogout={() => undefined}
        onNavigateBack={onNavigateBack}
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
        <TextInputContent />
      </BrowseShell>
    </FocusProvider>,
  );
}

function TextInputContent() {
  const restoreRequest = useBrowseShellContentRestoreRequest();
  const [engaged, setEngaged] = useState(false);
  const [query, setQuery] = useState('preserved query');
  const { engageTextInput, releaseTextInput } =
    useBrowseShellTextInputEngagement();
  const inputTarget = useMemo(
    () => ({
      scopeKey: 'search-input-scope',
      focusKey: 'search-input',
    } as const),
    [],
  );

  useEffect(
    () => () => releaseTextInput(inputTarget),
    [releaseTextInput],
  );

  return (
    <main>
      <FocusScope
        defaultFocusKey="before-search"
        orderedKeys={['before-search']}
        restoreFocusKey={
          restoreRequest?.target.scopeKey === 'content-scope'
            ? restoreRequest.target.focusKey
            : undefined
        }
        restoreRequestId={
          restoreRequest?.target.scopeKey === 'content-scope'
            ? restoreRequest.requestId
            : undefined
        }
        scopeKey="content-scope"
      >
        <Focusable focusKey="before-search" onSelect={() => undefined}>
          Before search
        </Focusable>
      </FocusScope>
      <div data-focus-scope={inputTarget.scopeKey}>
        <input
          aria-label="Search query"
          data-focus-key={inputTarget.focusKey}
          onChange={(event) => setQuery(event.currentTarget.value)}
          onKeyDown={(event) => {
            if (event.key !== 'Enter' || engaged) {
              return;
            }
            event.preventDefault();
            engageTextInput(inputTarget, () => setEngaged(false));
            setEngaged(true);
          }}
          readOnly={!engaged}
          value={query}
        />
      </div>
    </main>
  );
}

function installWebView(
  setHandler: (handler: (event: { data: unknown }) => void) => void,
) {
  Object.defineProperty(window, 'chrome', {
    configurable: true,
    value: {
      webview: {
        addEventListener: vi.fn(
          (_type: 'message', handler: (event: { data: unknown }) => void) => {
            setHandler(handler);
          },
        ),
        removeEventListener: vi.fn(),
        postMessage: vi.fn(),
      },
    },
  });
}

function input(
  sequence: number,
  command: 'back' | 'menu',
) {
  return {
    type: 'host.input',
    version: 1,
    sequence,
    command,
    phase: 'pressed',
    source: 'gamepad',
    timestamp: sequence,
  };
}
