// @vitest-environment jsdom

import {
  GetBoundingClientRectAdapter,
  SpatialNavigation,
  setFocus,
  updateAllLayouts,
} from '@noriginmedia/norigin-spatial-navigation';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { StrictMode } from 'react';
import type { ComponentProps } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Focusable } from './Focusable';
import { FocusProvider } from './FocusProvider';
import { FocusScope } from './FocusScope';
import { createFocusNavigationPolicy } from './focusPolicy';

type SpatialNavigationInternals = {
  debug: boolean;
  enabled: boolean;
  focusOnPresetKey: boolean;
  layoutAdapter: unknown;
  paused: boolean;
  throttle: number;
  throttleKeypresses: boolean;
  visualDebugger: unknown;
};

const spatialNavigation = SpatialNavigation as unknown as SpatialNavigationInternals;

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('FocusProvider', () => {
  it('initializes the real singleton once and keeps it alive through StrictMode replay', () => {
    const addEventListener = vi.spyOn(window, 'addEventListener');

    render(
      <StrictMode>
        <FocusProvider>
          <span>content</span>
        </FocusProvider>
      </StrictMode>,
    );

    expect(
      addEventListener.mock.calls.filter(([eventName]) => eventName === 'keydown'),
    ).toHaveLength(1);
    expect(
      addEventListener.mock.calls.filter(([eventName]) => eventName === 'keyup'),
    ).toHaveLength(1);
    expect(spatialNavigation.enabled).toBe(true);
    expect(SpatialNavigation.options.shouldFocusDOMNode).toBe(true);
    expect(spatialNavigation.layoutAdapter).toBeInstanceOf(GetBoundingClientRectAdapter);
    expect(spatialNavigation.throttle).toBe(100);
    expect(spatialNavigation.throttleKeypresses).toBe(true);
    expect(spatialNavigation.focusOnPresetKey).toBe(false);
    expect(spatialNavigation.debug).toBe(false);
    expect(spatialNavigation.visualDebugger).toBeNull();
  });

  it('propagates policy pause and resume synchronously to the real engine', async () => {
    const policy = createFocusNavigationPolicy();
    const onSelect = vi.fn();

    render(
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="pause-scope-anonymous"
          orderedKeys={['pause-focus-anonymous']}
          defaultFocusKey="pause-focus-anonymous"
        >
          <Focusable focusKey="pause-focus-anonymous" onSelect={onSelect}>
            Pause target
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    await waitFor(() => {
      expect(document.activeElement).toBe(screen.getByRole('button'));
    });

    policy.pause();
    expect(spatialNavigation.paused).toBe(true);

    await waitForThrottleWindow();
    dispatchKey(window, 'Enter', 13);
    expect(onSelect).not.toHaveBeenCalled();

    policy.resume();
    expect(spatialNavigation.paused).toBe(false);

    await waitForThrottleWindow();
    dispatchKey(window, 'Enter', 13);
    expect(onSelect).toHaveBeenCalledTimes(1);
  });
});

describe('Noira focus component contract', () => {
  it('keeps Norigin methods and configuration out of page-facing props', () => {
    type NoriginProp =
      | 'autoRestoreFocus'
      | 'extraProps'
      | 'focusBoundaryDirections'
      | 'focusSelf'
      | 'focusable'
      | 'forceFocus'
      | 'hasFocusedChild'
      | 'isFocusBoundary'
      | 'navigateByDirection'
      | 'nextFocusResolver'
      | 'onArrowPress'
      | 'onEnterPress'
      | 'preferredChildFocusKey'
      | 'saveLastFocusedChild'
      | 'setFocus'
      | 'trackChildren'
      | 'useFocusable';
    type HasNoNoriginProps<Props> = Extract<keyof Props, NoriginProp> extends never
      ? true
      : false;

    const pagePropsStayNoiraOwned: [
      HasNoNoriginProps<ComponentProps<typeof FocusProvider>>,
      HasNoNoriginProps<ComponentProps<typeof FocusScope>>,
      HasNoNoriginProps<ComponentProps<typeof Focusable>>,
    ] = [true, true, true];

    expect(pagePropsStayNoiraOwned).toEqual([true, true, true]);
  });

  it('renders an accessible real button with a stable key and remembers its ordered scope', async () => {
    const orderedKeys = ['first-focus-anonymous', 'second-focus-anonymous'] as const;
    const policy = createFocusNavigationPolicy();
    const remember = vi.spyOn(policy, 'remember');
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    render(
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="button-scope-anonymous"
          orderedKeys={orderedKeys}
          defaultFocusKey="second-focus-anonymous"
        >
          <Focusable focusKey="first-focus-anonymous" onSelect={() => undefined}>
            First
          </Focusable>
          <Focusable
            aria-label="Open second item"
            className="media-action"
            focusKey="second-focus-anonymous"
            onSelect={() => undefined}
          >
            Second
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    const button = screen.getByRole('button', { name: 'Open second item' });

    await waitFor(() => {
      expect(document.activeElement).toBe(button);
      expect(remember).toHaveBeenCalledWith(
        'button-scope-anonymous',
        'second-focus-anonymous',
        orderedKeys,
      );
    });

    expect(button.tagName).toBe('BUTTON');
    expect(button.getAttribute('type')).toBe('button');
    expect(button.getAttribute('data-focus-key')).toBe('second-focus-anonymous');
    expect(button.className).toBe('media-action');
    expect(button.textContent).toBe('Second');
    expect(focus).toHaveBeenCalled();
  });

  it('invokes selection exactly once for Enter and once for a native click', async () => {
    const onSelect = vi.fn();

    render(
      <FocusProvider>
        <FocusScope
          scopeKey="select-scope-anonymous"
          orderedKeys={['select-focus-anonymous']}
          preferredFocusKey="select-focus-anonymous"
        >
          <Focusable focusKey="select-focus-anonymous" onSelect={onSelect}>
            Select item
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    const button = screen.getByRole('button', { name: 'Select item' });
    await waitFor(() => {
      expect(document.activeElement).toBe(button);
    });

    await waitForThrottleWindow();
    dispatchKey(button, 'Enter', 13);
    expect(onSelect).toHaveBeenCalledTimes(1);

    onSelect.mockClear();
    fireEvent.click(button);
    expect(onSelect).toHaveBeenCalledTimes(1);
  });
});

describe('real Norigin navigation through Noira scopes', () => {
  it('moves DOM focus right according to three-item geometry', async () => {
    render(
      <FocusProvider>
        <FocusScope
          scopeKey="geometry-scope-anonymous"
          orderedKeys={[
            'geometry-left-anonymous',
            'geometry-middle-anonymous',
            'geometry-right-anonymous',
          ]}
          defaultFocusKey="geometry-left-anonymous"
        >
          <Focusable focusKey="geometry-left-anonymous" onSelect={() => undefined}>
            Left
          </Focusable>
          <Focusable focusKey="geometry-middle-anonymous" onSelect={() => undefined}>
            Middle
          </Focusable>
          <Focusable focusKey="geometry-right-anonymous" onSelect={() => undefined}>
            Right
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    const left = screen.getByRole('button', { name: 'Left' });
    const middle = screen.getByRole('button', { name: 'Middle' });
    const right = screen.getByRole('button', { name: 'Right' });
    mockRect(left, 0, 0);
    mockRect(middle, 120, 0);
    mockRect(right, 240, 0);

    await updateLayoutsAndFocus('geometry-left-anonymous');
    expect(document.activeElement).toBe(left);

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowRight', 39);

    await waitFor(() => {
      expect(document.activeElement).toBe(middle);
    });
  });

  it('traps navigation in a configured directional boundary', async () => {
    render(
      <FocusProvider>
        <FocusScope
          scopeKey="boundary-scope-anonymous"
          orderedKeys={['boundary-focus-anonymous']}
          defaultFocusKey="boundary-focus-anonymous"
          boundaryDirections={['right']}
        >
          <Focusable focusKey="boundary-focus-anonymous" onSelect={() => undefined}>
            Inside boundary
          </Focusable>
        </FocusScope>
        <FocusScope
          scopeKey="outside-scope-anonymous"
          orderedKeys={['outside-focus-anonymous']}
        >
          <Focusable focusKey="outside-focus-anonymous" onSelect={() => undefined}>
            Outside boundary
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    const inside = screen.getByRole('button', { name: 'Inside boundary' });
    const outside = screen.getByRole('button', { name: 'Outside boundary' });
    const insideScope = getScopeElement('boundary-scope-anonymous');
    const outsideScope = getScopeElement('outside-scope-anonymous');
    mockRect(inside, 0, 0);
    mockRect(outside, 200, 0);
    mockRect(insideScope, 0, 0);
    mockRect(outsideScope, 200, 0);

    await updateLayoutsAndFocus('boundary-focus-anonymous');
    expect(document.activeElement).toBe(inside);

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowRight', 39);

    await flushNavigationScheduler();
    expect(document.activeElement).toBe(inside);
  });

  it('uses Noira restore fallback when the focused child unmounts', async () => {
    const policy = createFocusNavigationPolicy();

    const renderTree = (orderedKeys: readonly string[]) => (
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="unmount-scope-anonymous"
          orderedKeys={orderedKeys}
          restoreFocusKey="unmount-removed-anonymous"
        >
          <Focusable focusKey="unmount-first-anonymous" onSelect={() => undefined}>
            Remaining item
          </Focusable>
          {orderedKeys.includes('unmount-removed-anonymous') ? (
            <Focusable focusKey="unmount-removed-anonymous" onSelect={() => undefined}>
              Removed item
            </Focusable>
          ) : null}
        </FocusScope>
      </FocusProvider>
    );

    const { rerender } = render(
      renderTree(['unmount-first-anonymous', 'unmount-removed-anonymous']),
    );

    await waitFor(() => {
      expect(document.activeElement).toBe(
        screen.getByRole('button', { name: 'Removed item' }),
      );
    });

    rerender(renderTree(['unmount-first-anonymous']));

    await waitFor(() => {
      expect(document.activeElement).toBe(
        screen.getByRole('button', { name: 'Remaining item' }),
      );
    });
    expect(policy.resolve('unmount-scope-anonymous', ['unmount-first-anonymous'])).toBe(
      'unmount-first-anonymous',
    );
  });
});

function mockRect(
  element: HTMLElement,
  left: number,
  top: number,
  width = 80,
  height = 40,
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

function getScopeElement(scopeKey: string): HTMLElement {
  const scope = document.querySelector<HTMLElement>(
    `[data-focus-scope="${scopeKey}"]`,
  );
  if (!scope) {
    throw new Error(`Focus scope ${scopeKey} was not rendered.`);
  }

  return scope;
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

async function flushNavigationScheduler(): Promise<void> {
  await act(async () => {
    await new Promise((resolve) => window.setTimeout(resolve, 0));
  });
}

function dispatchKey(target: Window | HTMLElement, key: string, keyCode: number): void {
  fireEvent.keyDown(target, { code: key, key, keyCode, which: keyCode });
  fireEvent.keyUp(target, { code: key, key, keyCode, which: keyCode });
}
