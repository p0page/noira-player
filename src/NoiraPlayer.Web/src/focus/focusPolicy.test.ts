import { describe, expect, it, vi } from 'vitest';
import { createFocusNavigationPolicy } from './focusPolicy';
import type { TransientLayer } from './focusPolicy';
import type { BrowseRoute, FocusTarget } from '../navigation/routes';

describe('focus restoration', () => {
  it('restores exact surviving keys independently per scope', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-a-anonymous', 'focus-a-2', ['focus-a-1', 'focus-a-2']);
    policy.remember('scope-b-anonymous', 'focus-b-2', ['focus-b-1', 'focus-b-2']);

    expect(
      policy.resolve('scope-a-anonymous', ['focus-a-2', 'focus-a-1']),
    ).toBe('focus-a-2');
    expect(policy.resolve('scope-b-anonymous', ['focus-b-1', 'focus-b-2'])).toBe(
      'focus-b-2',
    );
    expect(policy.resolve('scope-c-anonymous', ['focus-c-1', 'focus-c-2'])).toBe(
      'focus-c-1',
    );
  });

  it('snapshots the remembered index instead of retaining a caller-owned array', () => {
    const policy = createFocusNavigationPolicy();
    const orderedKeys = ['focus-first', 'focus-remembered', 'focus-last'];

    policy.remember('scope-snapshot-anonymous', 'focus-remembered', orderedKeys);
    orderedKeys.splice(0, orderedKeys.length, 'caller-mutated-only');

    expect(
      policy.resolve('scope-snapshot-anonymous', [
        'focus-replacement-first',
        'focus-replacement-index',
        'focus-replacement-last',
      ]),
    ).toBe('focus-replacement-index');
  });

  it('uses the remembered index after removal and the preceding last key when shortened', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-removal-anonymous', 'focus-remembered', [
      'focus-first',
      'focus-second',
      'focus-remembered',
      'focus-last',
    ]);

    expect(
      policy.resolve('scope-removal-anonymous', [
        'focus-current-first',
        'focus-current-second',
        'focus-current-index',
      ]),
    ).toBe('focus-current-index');
    expect(
      policy.resolve('scope-removal-anonymous', [
        'focus-current-first',
        'focus-current-last',
      ]),
    ).toBe('focus-current-last');
  });

  it('handles reordered and empty key lists deterministically', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-reordered-anonymous', 'focus-remembered', [
      'focus-leading',
      'focus-remembered',
      'focus-trailing',
    ]);

    expect(
      policy.resolve('scope-reordered-anonymous', [
        'focus-reordered-last',
        'focus-remembered',
        'focus-reordered-first',
      ]),
    ).toBe('focus-remembered');
    expect(
      policy.resolve('scope-reordered-anonymous', [
        'focus-current-first',
        'focus-current-at-remembered-index',
        'focus-current-last',
      ]),
    ).toBe('focus-current-at-remembered-index');
    expect(policy.resolve('scope-reordered-anonymous', [])).toBeNull();
  });

  it('resolves an available initial default, then the first key, then null', () => {
    const policy = createFocusNavigationPolicy();

    expect(
      policy.resolveInitial(['focus-first', 'focus-default'], 'focus-default'),
    ).toBe('focus-default');
    expect(policy.resolveInitial(['focus-first', 'focus-second'], 'focus-missing')).toBe(
      'focus-first',
    );
    expect(policy.resolveInitial([])).toBeNull();
  });
});

describe('focus key invariants', () => {
  it('rejects blank scope and focus keys', () => {
    const policy = createFocusNavigationPolicy();

    expect(() =>
      policy.remember('   ', 'focus-valid-anonymous', ['focus-valid-anonymous']),
    ).toThrowError('scopeKey must be a non-blank string.');
    expect(() => policy.resolve('', [])).toThrowError(
      'scopeKey must be a non-blank string.',
    );
    expect(() => policy.remember('scope-valid-anonymous', '\t', ['\t'])).toThrowError(
      'focusKey must be a non-blank string.',
    );
    expect(() => policy.resolveInitial(['focus-valid-anonymous'], ' ')).toThrowError(
      'defaultKey must be a non-blank string.',
    );
  });

  it('rejects blank ordered and available keys', () => {
    const policy = createFocusNavigationPolicy();

    expect(() =>
      policy.remember('scope-valid-anonymous', 'focus-valid-anonymous', [
        'focus-valid-anonymous',
        ' ',
      ]),
    ).toThrowError('orderedKeys must contain only non-blank keys.');
    expect(() =>
      policy.resolve('scope-valid-anonymous', ['focus-valid-anonymous', '']),
    ).toThrowError('availableKeys must contain only non-blank keys.');
    expect(() => policy.resolveInitial(['\t'])).toThrowError(
      'availableKeys must contain only non-blank keys.',
    );
  });

  it('rejects duplicate ordered and available keys', () => {
    const policy = createFocusNavigationPolicy();

    expect(() =>
      policy.remember('scope-duplicate-anonymous', 'focus-duplicate-anonymous', [
        'focus-duplicate-anonymous',
        'focus-duplicate-anonymous',
      ]),
    ).toThrowError('orderedKeys must contain unique keys.');
    expect(() =>
      policy.resolve('scope-duplicate-anonymous', [
        'focus-duplicate-anonymous',
        'focus-duplicate-anonymous',
      ]),
    ).toThrowError('availableKeys must contain unique keys.');
    expect(() =>
      policy.resolveInitial(['focus-duplicate-anonymous', 'focus-duplicate-anonymous']),
    ).toThrowError('availableKeys must contain unique keys.');
  });

  it('rejects a remembered key absent from its ordered scope with RangeError', () => {
    const policy = createFocusNavigationPolicy();
    const rememberMissingKey = () =>
      policy.remember('scope-missing-anonymous', 'focus-missing-anonymous', [
        'focus-present-anonymous',
      ]);

    expect(rememberMissingKey).toThrowError(RangeError);
    expect(rememberMissingKey).toThrowError(
      'focusKey must be present in orderedKeys.',
    );
  });

  it('keeps empty available lists valid', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-empty-anonymous', 'focus-present-anonymous', [
      'focus-present-anonymous',
    ]);

    expect(policy.resolve('scope-empty-anonymous', [])).toBeNull();
    expect(policy.resolveInitial([], 'focus-default-anonymous')).toBeNull();
  });
});

describe('focus session lifecycle', () => {
  it('clears every remembered scope for a user or session reset', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-session-a-anonymous', 'focus-a-2', [
      'focus-a-1',
      'focus-a-2',
    ]);
    policy.remember('scope-session-b-anonymous', 'focus-b-2', [
      'focus-b-1',
      'focus-b-2',
    ]);
    expect(policy.resolve('scope-session-a-anonymous', ['focus-a-1', 'focus-a-2'])).toBe(
      'focus-a-2',
    );
    expect(policy.resolve('scope-session-b-anonymous', ['focus-b-1', 'focus-b-2'])).toBe(
      'focus-b-2',
    );

    policy.clear();
    policy.clear();

    expect(policy.resolve('scope-session-a-anonymous', ['focus-a-1', 'focus-a-2'])).toBe(
      'focus-a-1',
    );
    expect(policy.resolve('scope-session-b-anonymous', ['focus-b-1', 'focus-b-2'])).toBe(
      'focus-b-1',
    );
  });
});

describe('Back policy', () => {
  const homeRoute: BrowseRoute = { kind: 'home' };
  const homeLibraryTarget: FocusTarget = {
    scopeKey: 'home-libraries-anonymous',
    focusKey: 'library-focus-anonymous',
  };
  const libraryItemTarget: FocusTarget = {
    scopeKey: 'library-grid-anonymous',
    focusKey: 'item-focus-anonymous',
  };
  const homeItemTarget: FocusTarget = {
    scopeKey: 'home-row-anonymous',
    focusKey: 'home-item-focus-anonymous',
  };
  const libraryRoute: BrowseRoute = {
    kind: 'library',
    libraryId: 'library-anonymous',
    collectionType: 'movies',
    origin: homeLibraryTarget,
  };
  const libraryDetailsRoute: BrowseRoute = {
    kind: 'details',
    itemId: 'item-library-anonymous',
    origin: libraryItemTarget,
  };
  const homeDetailsRoute: BrowseRoute = {
    kind: 'details',
    itemId: 'item-home-anonymous',
    origin: homeItemTarget,
  };

  it('closes a transient layer before navigating and returns its complete target', () => {
    const policy = createFocusNavigationPolicy();
    const routeStack = Object.freeze([
      homeRoute,
      libraryRoute,
      libraryDetailsRoute,
    ] as BrowseRoute[]);
    const transientLayer: TransientLayer = {
      kind: 'guide',
      returnTarget: {
        scopeKey: 'details-actions-anonymous',
        focusKey: 'play-focus-anonymous',
      },
    };
    const stackSnapshot = JSON.stringify(routeStack);
    const transientSnapshot = JSON.stringify(transientLayer);

    expect(policy.decideBack(routeStack, transientLayer)).toEqual({
      kind: 'closeTransient',
      layer: 'guide',
      restoreTarget: {
        scopeKey: 'details-actions-anonymous',
        focusKey: 'play-focus-anonymous',
      },
    });
    expect(JSON.stringify(routeStack)).toBe(stackSnapshot);
    expect(JSON.stringify(transientLayer)).toBe(transientSnapshot);
  });

  it('gives a valid transient layer priority over a malformed route stack', () => {
    const policy = createFocusNavigationPolicy();
    const malformedStack = [libraryRoute] as unknown as readonly BrowseRoute[];

    expect(
      policy.decideBack(malformedStack, {
        kind: 'overlay',
        returnTarget: {
          scopeKey: 'overlay-scope-anonymous',
          focusKey: 'overlay-focus-anonymous',
        },
      }),
    ).toEqual({
      kind: 'closeTransient',
      layer: 'overlay',
      restoreTarget: {
        scopeKey: 'overlay-scope-anonymous',
        focusKey: 'overlay-focus-anonymous',
      },
    });
  });

  it.each([
    {
      kind: 'guide',
      returnTarget: {
        scopeKey: ' ',
        focusKey: 'transient-focus-anonymous',
      },
    },
    {
      kind: 'overlay',
      returnTarget: {
        scopeKey: 'transient-scope-anonymous',
        focusKey: '',
      },
    },
    {
      kind: 'unknown',
      returnTarget: {
        scopeKey: 'transient-scope-anonymous',
        focusKey: 'transient-focus-anonymous',
      },
    },
  ])('fails closed for a malformed transient layer: $kind', (transientLayer) => {
    const policy = createFocusNavigationPolicy();

    expect(
      policy.decideBack(
        [homeRoute, libraryRoute],
        transientLayer as unknown as TransientLayer,
      ),
    ).toEqual({ kind: 'nativeBack' });
  });

  it('fails closed for malformed route order, fields, and origins', () => {
    const policy = createFocusNavigationPolicy();
    const malformedStacks = [
      [homeRoute, homeRoute],
      [
        homeRoute,
        {
          kind: 'details',
          itemId: '',
          origin: homeItemTarget,
        },
      ],
      [
        homeRoute,
        {
          kind: 'details',
          itemId: 'item-malformed-origin-anonymous',
          origin: {
            scopeKey: 'home-row-anonymous',
            focusKey: ' ',
          },
        },
      ],
    ] as unknown as readonly BrowseRoute[][];

    for (const malformedStack of malformedStacks) {
      expect(policy.decideBack(malformedStack)).toEqual({ kind: 'nativeBack' });
    }
  });

  it('returns Details to its library route and exact library-grid target', () => {
    const policy = createFocusNavigationPolicy();
    const routeStack = Object.freeze([
      homeRoute,
      libraryRoute,
      libraryDetailsRoute,
    ] as BrowseRoute[]);
    const stackSnapshot = JSON.stringify(routeStack);

    expect(policy.decideBack(routeStack)).toEqual({
      kind: 'navigate',
      route: libraryRoute,
      restoreTarget: libraryItemTarget,
    });
    expect(JSON.stringify(routeStack)).toBe(stackSnapshot);
  });

  it('returns direct Details to Home and library to its Home origin', () => {
    const policy = createFocusNavigationPolicy();

    expect(policy.decideBack([homeRoute, homeDetailsRoute])).toEqual({
      kind: 'navigate',
      route: homeRoute,
      restoreTarget: homeItemTarget,
    });
    expect(policy.decideBack([homeRoute, libraryRoute])).toEqual({
      kind: 'navigate',
      route: homeRoute,
      restoreTarget: homeLibraryTarget,
    });
  });

  it('hands Home and empty history Back to the native host', () => {
    const policy = createFocusNavigationPolicy();

    expect(policy.decideBack([homeRoute])).toEqual({ kind: 'nativeBack' });
    expect(policy.decideBack([])).toEqual({ kind: 'nativeBack' });
  });
});

describe('focus pause state', () => {
  it('notifies immediately and synchronously, suppresses repeats, and honors unsubscribe', () => {
    const policy = createFocusNavigationPolicy();
    const listener = vi.fn<(paused: boolean) => void>();

    const unsubscribe = policy.subscribePause(listener);

    expect(listener.mock.calls).toEqual([[false]]);
    expect(policy.isPaused()).toBe(false);

    policy.resume();
    policy.pause();
    expect(listener.mock.calls).toEqual([[false], [true]]);
    expect(policy.isPaused()).toBe(true);

    policy.pause();
    policy.resume();
    expect(listener.mock.calls).toEqual([[false], [true], [false]]);
    expect(policy.isPaused()).toBe(false);

    unsubscribe();
    unsubscribe();
    policy.pause();
    expect(listener.mock.calls).toEqual([[false], [true], [false]]);
    expect(policy.isPaused()).toBe(true);
  });

  it('serializes a reentrant state change for every listener', () => {
    const policy = createFocusNavigationPolicy();
    const firstStates: boolean[] = [];
    const secondStates: boolean[] = [];
    let requestedResume = false;

    policy.subscribePause((paused) => {
      firstStates.push(paused);
      if (paused && !requestedResume) {
        requestedResume = true;
        policy.resume();
      }
    });
    policy.subscribePause((paused) => {
      secondStates.push(paused);
    });

    policy.pause();

    expect(firstStates).toEqual([false, true, false]);
    expect(secondStates).toEqual([false, true, false]);
    expect(firstStates.at(-1)).toBe(policy.isPaused());
    expect(secondStates.at(-1)).toBe(policy.isPaused());
  });

  it('uses the last reentrant state request during a broadcast', () => {
    const policy = createFocusNavigationPolicy();
    const firstStates: boolean[] = [];
    const secondStates: boolean[] = [];
    let requestedResume = false;
    let requestedPause = false;

    policy.subscribePause((paused) => {
      firstStates.push(paused);
      if (paused && !requestedResume) {
        requestedResume = true;
        policy.resume();
      }
    });
    policy.subscribePause((paused) => {
      secondStates.push(paused);
      if (paused && !requestedPause) {
        requestedPause = true;
        policy.pause();
      }
    });

    policy.pause();

    expect(firstStates).toEqual([false, true]);
    expect(secondStates).toEqual([false, true]);
    expect(policy.isPaused()).toBe(true);
  });

  it('finishes queued delivery for all listeners before rethrowing the first error', () => {
    const policy = createFocusNavigationPolicy();
    const firstError = new Error('first pause listener failure');
    const secondError = new Error('second pause listener failure');
    const laterStates: boolean[] = [];
    let requestedResume = false;

    policy.subscribePause((paused) => {
      if (paused) {
        if (!requestedResume) {
          requestedResume = true;
          policy.resume();
        }
        throw firstError;
      }
    });
    policy.subscribePause((paused) => {
      if (paused) {
        throw secondError;
      }
    });
    policy.subscribePause((paused) => {
      laterStates.push(paused);
    });

    let thrown: unknown;
    try {
      policy.pause();
    } catch (error) {
      thrown = error;
    }

    expect(thrown).toBe(firstError);
    expect(laterStates).toEqual([false, true, false]);
    expect(policy.isPaused()).toBe(false);
  });

  it('removes a subscription when its immediate callback throws', () => {
    const policy = createFocusNavigationPolicy();
    const immediateError = new Error('immediate pause listener failure');
    const listener = vi.fn((_paused: boolean) => {
      throw immediateError;
    });

    expect(() => policy.subscribePause(listener)).toThrow(immediateError);
    expect(listener).toHaveBeenCalledOnce();

    expect(() => policy.pause()).not.toThrow();
    expect(listener).toHaveBeenCalledOnce();
    expect(policy.isPaused()).toBe(true);
  });
});
