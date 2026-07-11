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

  it('handles reordered, empty, and duplicate key lists deterministically', () => {
    const policy = createFocusNavigationPolicy();

    policy.remember('scope-duplicate-anonymous', 'focus-duplicate', [
      'focus-leading',
      'focus-duplicate',
      'focus-duplicate',
      'focus-trailing',
    ]);

    expect(
      policy.resolve('scope-duplicate-anonymous', [
        'focus-reordered-last',
        'focus-duplicate',
        'focus-reordered-first',
      ]),
    ).toBe('focus-duplicate');
    expect(
      policy.resolve('scope-duplicate-anonymous', [
        'focus-current-first',
        'focus-current-at-first-duplicate-index',
        'focus-current-last',
      ]),
    ).toBe('focus-current-at-first-duplicate-index');
    expect(policy.resolve('scope-duplicate-anonymous', [])).toBeNull();
  });

  it('resolves an available initial default, then the first key, then null', () => {
    const policy = createFocusNavigationPolicy();

    expect(
      policy.resolveInitial(['focus-first', 'focus-default', 'focus-default'], 'focus-default'),
    ).toBe('focus-default');
    expect(policy.resolveInitial(['focus-first', 'focus-second'], 'focus-missing')).toBe(
      'focus-first',
    );
    expect(policy.resolveInitial([])).toBeNull();
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
});
