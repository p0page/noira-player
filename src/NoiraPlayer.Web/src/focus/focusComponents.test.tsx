// @vitest-environment jsdom

import {
  FocusContext,
  GetBoundingClientRectAdapter,
  SpatialNavigation,
  doesFocusableExist,
  getCurrentFocusKey,
  setFocus,
  updateAllLayouts,
} from '@noriginmedia/norigin-spatial-navigation';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Component, StrictMode } from 'react';
import type { ComponentProps, ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Focusable } from './Focusable';
import {
  FocusProvider,
  NoiraFocusScopeContext,
  useNoiraFocusScope,
} from './FocusProvider';
import type { NoiraFocusScopeState } from './FocusProvider';
import { FocusScope } from './FocusScope';
import { createFocusNavigationPolicy } from './focusPolicy';

type SpatialNavigationInternals = {
  debug: boolean;
  enabled: boolean;
  focusKey: string;
  focusableComponents: Record<
    string,
    {
      focusable: boolean;
      parentFocusKey: string;
    }
  >;
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
  Reflect.deleteProperty(window, 'chrome');
  vi.restoreAllMocks();
});

describe('FocusProvider', () => {
  it('initializes once and keeps descendant registrations unique through StrictMode replay', async () => {
    const addEventListener = vi.spyOn(window, 'addEventListener');

    render(
      <StrictMode>
        <FocusProvider>
          <FocusScope
            scopeKey="strict-scope-anonymous"
            orderedKeys={['strict-focus-anonymous']}
            defaultFocusKey="strict-focus-anonymous"
          >
            <Focusable focusKey="strict-focus-anonymous" onSelect={() => undefined}>
              Strict target
            </Focusable>
          </FocusScope>
        </FocusProvider>
      </StrictMode>,
    );

    await waitFor(() => {
      expect(getCurrentFocusKey()).toBe('strict-focus-anonymous');
      expect(Object.keys(spatialNavigation.focusableComponents).sort()).toEqual([
        'strict-focus-anonymous',
        'strict-scope-anonymous',
      ]);
    });

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
    expect(
      spatialNavigation.focusableComponents['strict-focus-anonymous']
        ?.parentFocusKey,
    ).toBe('strict-scope-anonymous');
  });

  it('resumes the global policy from a typed native playback return event', async () => {
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
    const policy = createFocusNavigationPolicy();

    render(
      <FocusProvider policy={policy}>
        <p>Lifecycle child anonymous</p>
      </FocusProvider>,
    );
    policy.pause();
    expect(policy.isPaused()).toBe(true);

    act(() => {
      messageHandler?.({
        data: { type: 'host.lifecycle', event: 'playback-returned' },
      });
    });

    expect(policy.isPaused()).toBe(false);
  });

  it('preserves a later pending default through StrictMode registration replay', async () => {
    spatialNavigation.focusKey = '';

    render(
      <StrictMode>
        <FocusProvider>
          <FocusScope
            scopeKey="strict-guide-scope-anonymous"
            orderedKeys={['strict-guide-focus-anonymous']}
          >
            <Focusable focusKey="strict-guide-focus-anonymous" onSelect={() => undefined}>
              Strict guide
            </Focusable>
          </FocusScope>
          <FocusScope
            scopeKey="strict-media-scope-anonymous"
            orderedKeys={['strict-media-focus-anonymous']}
            defaultFocusKey="strict-media-focus-anonymous"
          >
            <Focusable focusKey="strict-media-focus-anonymous" onSelect={() => undefined}>
              Strict media
            </Focusable>
          </FocusScope>
        </FocusProvider>
      </StrictMode>,
    );

    await expectEngineFocus('strict-media-focus-anonymous', 'Strict media');
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

  it('fails fast when a mounted Focusable focusKey changes', () => {
    const renderTree = (focusKey: string) => (
      <FocusProvider>
        <FocusScope
          scopeKey="immutable-focus-scope-anonymous"
          orderedKeys={['immutable-focus-first-anonymous', 'immutable-focus-next-anonymous']}
        >
          <Focusable focusKey={focusKey} onSelect={() => undefined}>
            Immutable focus
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree('immutable-focus-first-anonymous'));

    expectRenderError(
      () => view.rerender(renderTree('immutable-focus-next-anonymous')),
      'Focusable focusKey cannot change after mount. Remount with a new React key.',
    );
  });

  it('fails fast when a mounted FocusScope scopeKey changes', () => {
    const renderTree = (scopeKey: string) => (
      <FocusProvider>
        <FocusScope scopeKey={scopeKey} orderedKeys={[]}>
          <div>Immutable scope</div>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree('immutable-scope-first-anonymous'));

    expectRenderError(
      () => view.rerender(renderTree('immutable-scope-next-anonymous')),
      'FocusScope scopeKey cannot change after mount. Remount with a new React key.',
    );
  });

  it('fails fast when a mounted Focusable moves to another parent scope', () => {
    let firstScope: NoiraFocusScopeState | null = null;
    let secondScope: NoiraFocusScopeState | null = null;
    const captureView = render(
      <FocusProvider>
        <FocusScope
          scopeKey="captured-parent-first-anonymous"
          orderedKeys={['immutable-parent-focus-anonymous']}
        >
          <CaptureScope onCapture={(scope) => { firstScope = scope; }} />
        </FocusScope>
        <FocusScope
          scopeKey="captured-parent-second-anonymous"
          orderedKeys={['immutable-parent-focus-anonymous']}
        >
          <CaptureScope onCapture={(scope) => { secondScope = scope; }} />
        </FocusScope>
      </FocusProvider>,
    );
    expect(firstScope).not.toBeNull();
    expect(secondScope).not.toBeNull();
    captureView.unmount();

    const renderTree = (scope: NoiraFocusScopeState) => (
      <FocusProvider>
        <NoiraFocusScopeContext.Provider value={scope}>
          <FocusContext.Provider value={scope.scopeKey}>
            <Focusable focusKey="immutable-parent-focus-anonymous" onSelect={() => undefined}>
              Immutable parent
            </Focusable>
          </FocusContext.Provider>
        </NoiraFocusScopeContext.Provider>
      </FocusProvider>
    );
    const view = render(renderTree(firstScope!));

    expectRenderError(
      () => view.rerender(renderTree(secondScope!)),
      'Focusable parent FocusScope cannot change after mount. Remount with a new React key.',
    );
  });

  it('rejects a Focusable key absent from its Scope orderedKeys', () => {
    expectRenderError(
      () =>
        render(
          <FocusProvider>
            <FocusScope
              scopeKey="missing-ordered-scope-anonymous"
              orderedKeys={['present-ordered-focus-anonymous']}
            >
              <Focusable focusKey="missing-ordered-focus-anonymous" onSelect={() => undefined}>
                Missing ordered key
              </Focusable>
            </FocusScope>
          </FocusProvider>,
        ),
      'Focusable focusKey "missing-ordered-focus-anonymous" must be present in FocusScope "missing-ordered-scope-anonymous" orderedKeys.',
    );
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

describe('global focus-key ownership', () => {
  it('rejects the second child owner before Norigin can replace the first registration', async () => {
    const collisionErrors: Error[] = [];
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const sharedFocusKey = 'duplicate-child-focus-anonymous';
    const renderTree = (showCollision: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey="duplicate-child-first-scope-anonymous"
          orderedKeys={[sharedFocusKey]}
        >
          <Focusable focusKey={sharedFocusKey} onSelect={() => undefined}>
            First child owner
          </Focusable>
        </FocusScope>
        <CollisionErrorBoundary onError={(error) => collisionErrors.push(error)}>
          {showCollision ? (
            <FocusScope
              scopeKey="duplicate-child-second-scope-anonymous"
              orderedKeys={[sharedFocusKey]}
            >
              <Focusable focusKey={sharedFocusKey} onSelect={() => undefined}>
                Second child owner
              </Focusable>
            </FocusScope>
          ) : null}
        </CollisionErrorBoundary>
      </FocusProvider>
    );
    const view = render(renderTree(false));
    const firstButton = screen.getByRole('button', { name: 'First child owner' });

    await waitFor(() => {
      expect(spatialNavigation.focusableComponents[sharedFocusKey]?.parentFocusKey)
        .toBe('duplicate-child-first-scope-anonymous');
    });
    await updateLayoutsAndFocus(sharedFocusKey);
    expect(document.activeElement).toBe(firstButton);
    const addFocusable = vi.spyOn(SpatialNavigation, 'addFocusable');

    view.rerender(renderTree(true));

    await expectFocusOwnershipCollision(collisionErrors, sharedFocusKey);
    expect(screen.queryByRole('button', { name: 'Second child owner' })).toBeNull();
    await waitFor(() => {
      expect(spatialNavigation.focusableComponents[sharedFocusKey]?.focusable).toBe(true);
      expect(spatialNavigation.focusableComponents[sharedFocusKey]?.parentFocusKey)
        .toBe('duplicate-child-first-scope-anonymous');
    });
    expect(
      addFocusable.mock.calls.some(
        ([registration]) =>
          registration.focusKey === sharedFocusKey &&
          registration.parentFocusKey === 'duplicate-child-second-scope-anonymous',
      ),
    ).toBe(false);
    expect(document.activeElement).toBe(firstButton);
  });

  it('rejects a second scope owner before Norigin can replace the first scope', async () => {
    const collisionErrors: Error[] = [];
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const sharedScopeKey = 'duplicate-scope-key-anonymous';
    const renderTree = (showCollision: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey={sharedScopeKey}
          orderedKeys={['duplicate-scope-first-focus-anonymous']}
        >
          <Focusable
            focusKey="duplicate-scope-first-focus-anonymous"
            onSelect={() => undefined}
          >
            First scope owner
          </Focusable>
        </FocusScope>
        <CollisionErrorBoundary onError={(error) => collisionErrors.push(error)}>
          {showCollision ? (
            <FocusScope
              scopeKey={sharedScopeKey}
              orderedKeys={['duplicate-scope-second-focus-anonymous']}
            >
              <Focusable
                focusKey="duplicate-scope-second-focus-anonymous"
                onSelect={() => undefined}
              >
                Second scope owner
              </Focusable>
            </FocusScope>
          ) : null}
        </CollisionErrorBoundary>
      </FocusProvider>
    );
    const view = render(renderTree(false));

    await waitFor(() => {
      expect(spatialNavigation.focusableComponents[sharedScopeKey]?.focusable).toBe(true);
    });

    view.rerender(renderTree(true));

    await expectFocusOwnershipCollision(collisionErrors, sharedScopeKey);
    await waitFor(() => {
      expect(doesFocusableExist(sharedScopeKey)).toBe(true);
      expect(
        spatialNavigation.focusableComponents['duplicate-scope-first-focus-anonymous']
          ?.parentFocusKey,
      ).toBe(sharedScopeKey);
    });

    await updateLayoutsAndFocus('duplicate-scope-first-focus-anonymous');
    expect(document.activeElement).toBe(
      screen.getByRole('button', { name: 'First scope owner' }),
    );
  });

  it('rejects a scope key that is already owned by a child', async () => {
    const collisionErrors: Error[] = [];
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const sharedFocusKey = 'child-scope-collision-anonymous';
    const renderTree = (showCollision: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey="child-scope-owner-anonymous"
          orderedKeys={[sharedFocusKey]}
        >
          <Focusable focusKey={sharedFocusKey} onSelect={() => undefined}>
            Child key owner
          </Focusable>
        </FocusScope>
        <CollisionErrorBoundary onError={(error) => collisionErrors.push(error)}>
          {showCollision ? (
            <FocusScope
              scopeKey={sharedFocusKey}
              orderedKeys={['child-scope-collision-nested-focus-anonymous']}
            >
              <Focusable
                focusKey="child-scope-collision-nested-focus-anonymous"
                onSelect={() => undefined}
              >
                Colliding scope child
              </Focusable>
            </FocusScope>
          ) : null}
        </CollisionErrorBoundary>
      </FocusProvider>
    );
    const view = render(renderTree(false));

    await waitFor(() => {
      expect(spatialNavigation.focusableComponents[sharedFocusKey]?.parentFocusKey)
        .toBe('child-scope-owner-anonymous');
    });

    view.rerender(renderTree(true));

    await expectFocusOwnershipCollision(collisionErrors, sharedFocusKey);
    await waitFor(() => {
      expect(spatialNavigation.focusableComponents[sharedFocusKey]?.parentFocusKey)
        .toBe('child-scope-owner-anonymous');
      expect(
        doesFocusableExist('child-scope-collision-nested-focus-anonymous'),
      ).toBe(false);
    });
  });
});

describe('focused owner handoff', () => {
  it('restores DOM focus to a keyed-remounted child with the same engine key', async () => {
    const policy = createFocusNavigationPolicy();
    const remember = vi.spyOn(policy, 'remember');
    const focusKey = 'child-handoff-focus-anonymous';
    const renderTree = (instance: number) => (
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="child-handoff-scope-anonymous"
          orderedKeys={[focusKey]}
          defaultFocusKey={focusKey}
        >
          <Focusable key={instance} focusKey={focusKey} onSelect={() => undefined}>
            Child handoff {instance}
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(1));
    const originalButton = screen.getByRole('button', { name: 'Child handoff 1' });

    await expectEngineFocus(focusKey, 'Child handoff 1');
    remember.mockClear();

    view.rerender(renderTree(2));
    const replacementButton = screen.getByRole('button', { name: 'Child handoff 2' });
    expect(replacementButton).not.toBe(originalButton);

    await expectEngineFocus(focusKey, 'Child handoff 2');
    expect(remember).not.toHaveBeenCalled();
  });

  it('restores DOM focus when a whole scope remounts with stable engine identities', async () => {
    const focusKey = 'scope-handoff-focus-anonymous';
    const renderTree = (instance: number) => (
      <FocusProvider>
        <FocusScope
          key={instance}
          scopeKey="scope-handoff-scope-anonymous"
          orderedKeys={[focusKey]}
          defaultFocusKey={focusKey}
        >
          <Focusable focusKey={focusKey} onSelect={() => undefined}>
            Scope handoff {instance}
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(1));
    const originalButton = screen.getByRole('button', { name: 'Scope handoff 1' });

    await expectEngineFocus(focusKey, 'Scope handoff 1');

    view.rerender(renderTree(2));
    const replacementButton = screen.getByRole('button', { name: 'Scope handoff 2' });
    expect(replacementButton).not.toBe(originalButton);

    await expectEngineFocus(focusKey, 'Scope handoff 2');
  });

  it('does not restore a remounted child when its key is no longer current', async () => {
    const sourceKey = 'noncurrent-handoff-source-anonymous';
    const destinationKey = 'noncurrent-handoff-destination-anonymous';
    const renderTree = (instance: number) => (
      <FocusProvider>
        <FocusScope
          scopeKey="noncurrent-handoff-scope-anonymous"
          orderedKeys={[sourceKey, destinationKey]}
          defaultFocusKey={sourceKey}
        >
          <Focusable key={instance} focusKey={sourceKey} onSelect={() => undefined}>
            Noncurrent source {instance}
          </Focusable>
          <Focusable focusKey={destinationKey} onSelect={() => undefined}>
            Noncurrent destination
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(1));

    await expectEngineFocus(sourceKey, 'Noncurrent source 1');
    await updateLayoutsAndFocus(destinationKey);
    const destination = screen.getByRole('button', {
      name: 'Noncurrent destination',
    });
    const focusTargets: EventTarget[] = [];
    const onFocusIn = (event: FocusEvent) => focusTargets.push(event.target!);
    document.addEventListener('focusin', onFocusIn);

    try {
      view.rerender(renderTree(2));
      const replacement = screen.getByRole('button', { name: 'Noncurrent source 2' });
      await settleFocusWork();

      expect(getCurrentFocusKey()).toBe(destinationKey);
      expect(document.activeElement).toBe(destination);
      expect(focusTargets).not.toContain(replacement);
    } finally {
      document.removeEventListener('focusin', onFocusIn);
    }
  });

  it('does not restore a keyed-remounted child that is disabled', async () => {
    const focusKey = 'disabled-handoff-focus-anonymous';
    const renderTree = (instance: number, disabled: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey="disabled-handoff-scope-anonymous"
          orderedKeys={[focusKey]}
          defaultFocusKey={focusKey}
        >
          <Focusable
            key={instance}
            disabled={disabled}
            focusKey={focusKey}
            onSelect={() => undefined}
          >
            Disabled handoff {instance}
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(1, false));

    await expectEngineFocus(focusKey, 'Disabled handoff 1');
    view.rerender(renderTree(2, true));
    const replacement = screen.getByRole('button', { name: 'Disabled handoff 2' });
    await settleFocusWork();

    expect(getCurrentFocusKey()).toBe(focusKey);
    expect((replacement as HTMLButtonElement).disabled).toBe(true);
    expect(document.activeElement).toBe(document.body);
  });

  it('does not restore a stale key over a newer explicit restore request', async () => {
    const sourceKey = 'explicit-handoff-source-anonymous';
    const destinationKey = 'explicit-handoff-destination-anonymous';
    const renderTree = (instance: number, restoreRequestId?: string) => (
      <FocusProvider>
        <FocusScope
          scopeKey="explicit-handoff-scope-anonymous"
          orderedKeys={[sourceKey, destinationKey]}
          defaultFocusKey={sourceKey}
          restoreFocusKey={restoreRequestId ? destinationKey : undefined}
          restoreRequestId={restoreRequestId}
        >
          <Focusable key={instance} focusKey={sourceKey} onSelect={() => undefined}>
            Explicit source {instance}
          </Focusable>
          <Focusable focusKey={destinationKey} onSelect={() => undefined}>
            Explicit destination
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(1));

    await expectEngineFocus(sourceKey, 'Explicit source 1');
    const focusTargets: EventTarget[] = [];
    const onFocusIn = (event: FocusEvent) => focusTargets.push(event.target!);
    document.addEventListener('focusin', onFocusIn);

    try {
      view.rerender(renderTree(2, 'session-a:explicit-handoff-1'));
      const replacement = screen.getByRole('button', { name: 'Explicit source 2' });

      await expectEngineFocus(destinationKey, 'Explicit destination');
      expect(focusTargets).not.toContain(replacement);
    } finally {
      document.removeEventListener('focusin', onFocusIn);
    }
  });
});

describe('adapter-owned focus lifecycle', () => {
  it('repairs a naturally unmounted focused child within the same scope', async () => {
    const policy = createFocusNavigationPolicy();
    const renderTree = (showSecond: boolean) => (
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="natural-unmount-scope-anonymous"
          orderedKeys={
            showSecond
              ? ['natural-first-anonymous', 'natural-second-anonymous']
              : ['natural-first-anonymous']
          }
        >
          <Focusable focusKey="natural-first-anonymous" onSelect={() => undefined}>
            Natural first
          </Focusable>
          {showSecond ? (
            <Focusable focusKey="natural-second-anonymous" onSelect={() => undefined}>
              Natural second
            </Focusable>
          ) : null}
        </FocusScope>
      </FocusProvider>
    );
    const { rerender } = render(renderTree(true));

    await updateLayoutsAndFocus('natural-second-anonymous');
    await waitFor(() => {
      expect(
        policy.resolve('natural-unmount-scope-anonymous', [
          'natural-first-anonymous',
          'natural-second-anonymous',
        ]),
      ).toBe('natural-second-anonymous');
    });

    rerender(renderTree(false));

    await expectEngineFocus('natural-first-anonymous', 'Natural first');
  });

  it('repairs a focused child that becomes disabled', async () => {
    const policy = createFocusNavigationPolicy();
    const renderTree = (disableSecond: boolean) => (
      <FocusProvider policy={policy}>
        <FocusScope
          scopeKey="disable-scope-anonymous"
          orderedKeys={['disable-first-anonymous', 'disable-second-anonymous']}
        >
          <Focusable focusKey="disable-first-anonymous" onSelect={() => undefined}>
            Enabled fallback
          </Focusable>
          <Focusable
            disabled={disableSecond}
            focusKey="disable-second-anonymous"
            onSelect={() => undefined}
          >
            Disabled target
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const { rerender } = render(renderTree(false));

    await updateLayoutsAndFocus('disable-second-anonymous');
    await waitFor(() => {
      expect(
        policy.resolve('disable-scope-anonymous', [
          'disable-first-anonymous',
          'disable-second-anonymous',
        ]),
      ).toBe('disable-second-anonymous');
    });

    rerender(renderTree(true));

    await expectEngineFocus('disable-first-anonymous', 'Enabled fallback');
  });

  it('leaves an empty active scope for the first enabled registered page scope', async () => {
    const renderTree = (showOrigin: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey="empty-origin-scope-anonymous"
          orderedKeys={showOrigin ? ['empty-origin-focus-anonymous'] : []}
        >
          {showOrigin ? (
            <Focusable focusKey="empty-origin-focus-anonymous" onSelect={() => undefined}>
              Empty origin
            </Focusable>
          ) : null}
        </FocusScope>
        <FocusScope scopeKey="empty-scope-anonymous" orderedKeys={[]}>
          <div>Empty scope</div>
        </FocusScope>
        <FocusScope
          scopeKey="disabled-scope-anonymous"
          orderedKeys={['disabled-only-focus-anonymous']}
        >
          <Focusable
            disabled
            focusKey="disabled-only-focus-anonymous"
            onSelect={() => undefined}
          >
            Disabled only
          </Focusable>
        </FocusScope>
        <FocusScope
          scopeKey="destination-scope-anonymous"
          orderedKeys={['destination-focus-anonymous']}
        >
          <Focusable focusKey="destination-focus-anonymous" onSelect={() => undefined}>
            Page destination
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const { rerender } = render(renderTree(true));

    await updateLayoutsAndFocus('empty-origin-focus-anonymous');
    expect(getCurrentFocusKey()).toBe('empty-origin-focus-anonymous');

    rerender(renderTree(false));

    await expectEngineFocus('destination-focus-anonymous', 'Page destination');
    expect(doesFocusableExist('empty-scope-anonymous')).toBe(true);
    expect(doesFocusableExist('disabled-scope-anonymous')).toBe(true);
    expect(
      spatialNavigation.focusableComponents['empty-scope-anonymous']?.focusable,
    ).toBe(false);
    expect(
      spatialNavigation.focusableComponents['disabled-scope-anonymous']?.focusable,
    ).toBe(false);
  });
});

describe('consumable scope focus requests', () => {
  it.each(['default', 'preferred', 'restore'] as const)(
    'does not let an appended missing %s target steal current focus',
    async (requestKind) => {
      const fallbackKey = `late-${requestKind}-fallback-anonymous`;
      const lateKey = `late-${requestKind}-target-anonymous`;
      const scopeKey = `late-${requestKind}-scope-anonymous`;
      const requestProps =
        requestKind === 'default'
          ? { defaultFocusKey: lateKey }
          : requestKind === 'preferred'
            ? { preferredFocusKey: lateKey }
            : { restoreFocusKey: lateKey };
      const renderTree = (includeLateTarget: boolean) => (
        <FocusProvider>
          <FocusScope
            {...requestProps}
            scopeKey={scopeKey}
            orderedKeys={includeLateTarget ? [fallbackKey, lateKey] : [fallbackKey]}
          >
            <Focusable focusKey={fallbackKey} onSelect={() => undefined}>
              Late fallback
            </Focusable>
            {includeLateTarget ? (
              <Focusable focusKey={lateKey} onSelect={() => undefined}>
                Late target
              </Focusable>
            ) : null}
          </FocusScope>
        </FocusProvider>
      );
      const { rerender } = render(renderTree(false));

      await expectEngineFocus(fallbackKey, 'Late fallback');

      rerender(renderTree(true));
      await settleFocusWork();

      expect(getCurrentFocusKey()).toBe(fallbackKey);
      expect(document.activeElement).toBe(
        screen.getByRole('button', { name: 'Late fallback' }),
      );
    },
  );

  it('does not apply a newly mounted initial default over a valid registered target', async () => {
    const renderTree = (showLateScope: boolean) => (
      <FocusProvider>
        <FocusScope
          scopeKey="stable-current-scope-anonymous"
          orderedKeys={['stable-current-focus-anonymous']}
        >
          <Focusable focusKey="stable-current-focus-anonymous" onSelect={() => undefined}>
            Stable current
          </Focusable>
        </FocusScope>
        {showLateScope ? (
          <FocusScope
            scopeKey="late-default-scope-anonymous"
            orderedKeys={['late-default-focus-anonymous']}
            defaultFocusKey="late-default-focus-anonymous"
          >
            <Focusable focusKey="late-default-focus-anonymous" onSelect={() => undefined}>
              Late default
            </Focusable>
          </FocusScope>
        ) : null}
      </FocusProvider>
    );
    const { rerender } = render(renderTree(false));

    await updateLayoutsAndFocus('stable-current-focus-anonymous');
    await expectEngineFocus('stable-current-focus-anonymous', 'Stable current');

    rerender(renderTree(true));
    await settleFocusWork();

    expect(getCurrentFocusKey()).toBe('stable-current-focus-anonymous');
  });

  it('restores the same target again when its request id changes', async () => {
    const renderTree = (restoreRequestId: number) => (
      <FocusProvider>
        <FocusScope
          scopeKey="restore-epoch-scope-anonymous"
          orderedKeys={['restore-epoch-first-anonymous', 'restore-epoch-target-anonymous']}
          restoreFocusKey="restore-epoch-target-anonymous"
          restoreRequestId={restoreRequestId}
        >
          <Focusable focusKey="restore-epoch-first-anonymous" onSelect={() => undefined}>
            Epoch first
          </Focusable>
          <Focusable focusKey="restore-epoch-target-anonymous" onSelect={() => undefined}>
            Epoch target
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const { rerender } = render(renderTree(1));

    await expectEngineFocus('restore-epoch-target-anonymous', 'Epoch target');
    await updateLayoutsAndFocus('restore-epoch-first-anonymous');
    await expectEngineFocus('restore-epoch-first-anonymous', 'Epoch first');

    rerender(renderTree(1));
    await settleFocusWork();
    expect(getCurrentFocusKey()).toBe('restore-epoch-first-anonymous');

    rerender(renderTree(2));
    await expectEngineFocus('restore-epoch-target-anonymous', 'Epoch target');
  });

  it('does not replay a consumed restore request when its scope remounts', async () => {
    const renderTree = (
      showRestoreScope: boolean,
      restoreRequestId: string,
    ) => (
      <FocusProvider>
        {showRestoreScope ? (
          <FocusScope
            scopeKey="remount-restore-scope-anonymous"
            orderedKeys={['remount-restore-target-anonymous']}
            restoreFocusKey="remount-restore-target-anonymous"
            restoreRequestId={restoreRequestId}
          >
            <Focusable
              focusKey="remount-restore-target-anonymous"
              onSelect={() => undefined}
            >
              Remount restore target
            </Focusable>
          </FocusScope>
        ) : null}
        <FocusScope
          scopeKey="remount-current-scope-anonymous"
          orderedKeys={['remount-current-focus-anonymous']}
        >
          <Focusable focusKey="remount-current-focus-anonymous" onSelect={() => undefined}>
            Remount current target
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const view = render(renderTree(true, 'session-a:restore-event-1'));

    await expectEngineFocus(
      'remount-restore-target-anonymous',
      'Remount restore target',
    );
    await updateLayoutsAndFocus('remount-current-focus-anonymous');
    await expectEngineFocus(
      'remount-current-focus-anonymous',
      'Remount current target',
    );

    view.rerender(renderTree(false, 'session-a:restore-event-1'));
    await settleFocusWork();
    view.rerender(renderTree(true, 'session-a:restore-event-1'));
    await settleFocusWork();

    expect(getCurrentFocusKey()).toBe('remount-current-focus-anonymous');
    expect(document.activeElement).toBe(
      screen.getByRole('button', { name: 'Remount current target' }),
    );

    view.rerender(renderTree(true, 'session-a:restore-event-2'));
    await expectEngineFocus(
      'remount-restore-target-anonymous',
      'Remount restore target',
    );
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

  it('treats an empty boundary direction list as trapping no directions', async () => {
    render(
      <FocusProvider>
        <FocusScope
          scopeKey="open-boundary-scope-anonymous"
          orderedKeys={['open-boundary-focus-anonymous']}
          defaultFocusKey="open-boundary-focus-anonymous"
          boundaryDirections={[]}
        >
          <Focusable focusKey="open-boundary-focus-anonymous" onSelect={() => undefined}>
            Open boundary
          </Focusable>
        </FocusScope>
        <FocusScope
          scopeKey="open-outside-scope-anonymous"
          orderedKeys={['open-outside-focus-anonymous']}
        >
          <Focusable focusKey="open-outside-focus-anonymous" onSelect={() => undefined}>
            Open outside
          </Focusable>
        </FocusScope>
      </FocusProvider>,
    );

    const inside = screen.getByRole('button', { name: 'Open boundary' });
    const outside = screen.getByRole('button', { name: 'Open outside' });
    const insideScope = getScopeElement('open-boundary-scope-anonymous');
    const outsideScope = getScopeElement('open-outside-scope-anonymous');
    mockRect(inside, 0, 0);
    mockRect(outside, 200, 0);
    mockRect(insideScope, 0, 0);
    mockRect(outsideScope, 200, 0);

    await updateLayoutsAndFocus('open-boundary-focus-anonymous');
    expect(document.activeElement).toBe(inside);

    await waitForThrottleWindow();
    dispatchKey(window, 'ArrowRight', 39);

    await waitFor(() => {
      expect(document.activeElement).toBe(outside);
    });
  });

  it.each([
    { direction: 'up', key: 'ArrowUp', keyCode: 38, outsideLeft: 100, outsideTop: 0 },
    { direction: 'down', key: 'ArrowDown', keyCode: 40, outsideLeft: 100, outsideTop: 200 },
    { direction: 'left', key: 'ArrowLeft', keyCode: 37, outsideLeft: 0, outsideTop: 100 },
    { direction: 'right', key: 'ArrowRight', keyCode: 39, outsideLeft: 200, outsideTop: 100 },
  ])('traps $direction when all boundary directions are configured', async ({
    direction,
    key,
    keyCode,
    outsideLeft,
    outsideTop,
  }) => {
    const scopeKey = `closed-boundary-${direction}-scope-anonymous`;
    const focusKey = `closed-boundary-${direction}-focus-anonymous`;
    const outsideScopeKey = `closed-outside-${direction}-scope-anonymous`;
    const outsideFocusKey = `closed-outside-${direction}-focus-anonymous`;
    render(
      <FocusProvider>
        <FocusScope
          scopeKey={scopeKey}
          orderedKeys={[focusKey]}
          defaultFocusKey={focusKey}
          boundaryDirections={['up', 'down', 'left', 'right']}
        >
          <Focusable focusKey={focusKey} onSelect={() => undefined}>
            Closed boundary
          </Focusable>
        </FocusScope>
        <FocusScope scopeKey={outsideScopeKey} orderedKeys={[outsideFocusKey]}>
          <Focusable focusKey={outsideFocusKey} onSelect={() => undefined}>
            Closed outside
          </Focusable>
        </FocusScope>
      </FocusProvider>
    );
    const inside = screen.getByRole('button', { name: 'Closed boundary' });
    const outside = screen.getByRole('button', { name: 'Closed outside' });
    mockRect(inside, 100, 100);
    mockRect(outside, outsideLeft, outsideTop);
    mockRect(getScopeElement(scopeKey), 100, 100);
    mockRect(getScopeElement(outsideScopeKey), outsideLeft, outsideTop);

    await updateLayoutsAndFocus(focusKey);
    expect(document.activeElement).toBe(inside);

    await waitForThrottleWindow();
    dispatchKey(window, key, keyCode);

    await flushNavigationScheduler();
    expect(document.activeElement).toBe(inside);
  });
});

function CaptureScope({
  onCapture,
}: {
  onCapture: (scope: NoiraFocusScopeState) => void;
}) {
  onCapture(useNoiraFocusScope());
  return null;
}

class CollisionErrorBoundary extends Component<
  { children: ReactNode; onError: (error: Error) => void },
  { failed: boolean }
> {
  state = { failed: false };

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true };
  }

  componentDidCatch(error: Error): void {
    this.props.onError(error);
  }

  render(): ReactNode {
    return this.state.failed ? null : this.props.children;
  }
}

async function expectFocusOwnershipCollision(
  errors: Error[],
  focusKey: string,
): Promise<void> {
  await waitFor(() => {
    expect(errors.map((error) => error.message)).toContain(
      `Norigin focusKey "${focusKey}" already has a mounted Noira owner.`,
    );
  });
}

function expectRenderError(action: () => void, message: string): void {
  const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  try {
    expect(action).toThrowError(message);
  } finally {
    consoleError.mockRestore();
  }
}

async function expectEngineFocus(focusKey: string, accessibleName: string): Promise<void> {
  await waitFor(() => {
    expect(getCurrentFocusKey()).toBe(focusKey);
    expect(document.activeElement).toBe(
      screen.getByRole('button', { name: accessibleName }),
    );
  });
}

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

async function settleFocusWork(): Promise<void> {
  await flushNavigationScheduler();
  await flushNavigationScheduler();
}

function dispatchKey(target: Window | HTMLElement, key: string, keyCode: number): void {
  fireEvent.keyDown(target, { code: key, key, keyCode, which: keyCode });
  fireEvent.keyUp(target, { code: key, key, keyCode, which: keyCode });
}
