import { describe, expect, it } from 'vitest';
import {
  backRoute,
  isValidBrowseRouteStack,
  pushRoute,
  replaceRoute,
} from './routes';
import type { BrowseRoute, FocusTarget } from './routes';

const homeRoute: BrowseRoute = { kind: 'home' };
const libraryOrigin: FocusTarget = {
  scopeKey: 'home-scope-anonymous',
  focusKey: 'library-focus-anonymous',
};
const libraryRoute: BrowseRoute = {
  kind: 'library',
  libraryId: 'library-anonymous',
  collectionType: 'movies',
  origin: libraryOrigin,
};
const libraryDetailsRoute: BrowseRoute = {
  kind: 'details',
  itemId: 'item-library-anonymous',
  origin: {
    scopeKey: 'library-grid-anonymous',
    focusKey: 'item-focus-anonymous',
  },
};
const homeDetailsRoute: BrowseRoute = {
  kind: 'details',
  itemId: 'item-home-anonymous',
  origin: {
    scopeKey: 'home-row-anonymous',
    focusKey: 'home-item-focus-anonymous',
  },
};

describe('browse route history', () => {
  it('pushes Home to library to details without mutating prior stacks', () => {
    const homeStack = Object.freeze([homeRoute] as BrowseRoute[]);

    const libraryStack = pushRoute(homeStack, libraryRoute);
    const detailsStack = pushRoute(libraryStack, libraryDetailsRoute);

    expect(homeStack).toEqual([homeRoute]);
    expect(libraryStack).toEqual([homeRoute, libraryRoute]);
    expect(detailsStack).toEqual([homeRoute, libraryRoute, libraryDetailsRoute]);
    expect(libraryStack).not.toBe(homeStack);
    expect(detailsStack).not.toBe(libraryStack);
  });

  it('deep snapshots existing and appended routes after a successful push', () => {
    const callerLibrary = {
      kind: 'library' as const,
      libraryId: 'library-push-anonymous',
      collectionType: 'movies',
      origin: {
        scopeKey: 'home-push-anonymous',
        focusKey: 'library-push-focus-anonymous',
      },
    };
    const callerDetails = {
      kind: 'details' as const,
      itemId: 'item-push-anonymous',
      origin: {
        scopeKey: 'library-push-grid-anonymous',
        focusKey: 'item-push-focus-anonymous',
      },
    };

    const history = pushRoute([{ kind: 'home' }, callerLibrary], callerDetails);

    callerLibrary.libraryId = 'caller-mutated-library';
    callerLibrary.origin.focusKey = 'caller-mutated-library-focus';
    callerDetails.itemId = 'caller-mutated-item';
    callerDetails.origin.scopeKey = 'caller-mutated-details-scope';

    expect(history).toEqual([
      { kind: 'home' },
      {
        kind: 'library',
        libraryId: 'library-push-anonymous',
        collectionType: 'movies',
        origin: {
          scopeKey: 'home-push-anonymous',
          focusKey: 'library-push-focus-anonymous',
        },
      },
      {
        kind: 'details',
        itemId: 'item-push-anonymous',
        origin: {
          scopeKey: 'library-push-grid-anonymous',
          focusKey: 'item-push-focus-anonymous',
        },
      },
    ]);
  });

  it('supports Home directly to details and initializes an empty history with Home', () => {
    const emptyStack = Object.freeze([] as BrowseRoute[]);
    const homeStack = pushRoute(emptyStack, homeRoute);

    const detailsStack = pushRoute(homeStack, homeDetailsRoute);

    expect(emptyStack).toEqual([]);
    expect(homeStack).toEqual([homeRoute]);
    expect(detailsStack).toEqual([homeRoute, homeDetailsRoute]);
    expect(homeStack).not.toBe(emptyStack);
    expect(detailsStack).not.toBe(homeStack);
  });

  it('returns the original history reference for illegal and repeated pushes', () => {
    const cases: Array<{
      stack: readonly BrowseRoute[];
      next: BrowseRoute;
    }> = [
      { stack: Object.freeze([] as BrowseRoute[]), next: libraryRoute },
      { stack: Object.freeze([homeRoute] as BrowseRoute[]), next: homeRoute },
      {
        stack: Object.freeze([homeRoute, libraryRoute] as BrowseRoute[]),
        next: libraryRoute,
      },
      {
        stack: Object.freeze([homeRoute, homeDetailsRoute] as BrowseRoute[]),
        next: libraryRoute,
      },
    ];

    for (const { stack, next } of cases) {
      const result = pushRoute(stack, next);

      expect(result).toBe(stack);
    }
  });

  it('replaces only when the resulting route history is valid', () => {
    const libraryStack = Object.freeze([homeRoute, libraryRoute] as BrowseRoute[]);
    const directDetailsStack = replaceRoute(libraryStack, homeDetailsRoute);
    const replacementDetails: BrowseRoute = {
      kind: 'details',
      itemId: 'item-replacement-anonymous',
      origin: libraryDetailsRoute.origin,
    };
    const nestedDetailsStack = Object.freeze([
      homeRoute,
      libraryRoute,
      libraryDetailsRoute,
    ] as BrowseRoute[]);

    const replacedNestedDetails = replaceRoute(nestedDetailsStack, replacementDetails);

    expect(directDetailsStack).toEqual([homeRoute, homeDetailsRoute]);
    expect(replacedNestedDetails).toEqual([homeRoute, libraryRoute, replacementDetails]);
    expect(libraryStack).toEqual([homeRoute, libraryRoute]);
    expect(nestedDetailsStack).toEqual([homeRoute, libraryRoute, libraryDetailsRoute]);
  });

  it('deep snapshots retained and replacement routes after a successful replace', () => {
    const callerLibrary = {
      kind: 'library' as const,
      libraryId: 'library-replace-anonymous',
      collectionType: 'tvshows',
      origin: {
        scopeKey: 'home-replace-anonymous',
        focusKey: 'library-replace-focus-anonymous',
      },
    };
    const currentDetails = {
      kind: 'details' as const,
      itemId: 'item-current-anonymous',
      origin: {
        scopeKey: 'library-replace-grid-anonymous',
        focusKey: 'item-current-focus-anonymous',
      },
    };
    const replacementDetails = {
      kind: 'details' as const,
      itemId: 'item-replacement-anonymous',
      origin: {
        scopeKey: 'library-replace-grid-anonymous',
        focusKey: 'item-replacement-focus-anonymous',
      },
    };

    const history = replaceRoute(
      [{ kind: 'home' }, callerLibrary, currentDetails],
      replacementDetails,
    );

    callerLibrary.collectionType = 'caller-mutated-collection';
    callerLibrary.origin.scopeKey = 'caller-mutated-library-scope';
    replacementDetails.itemId = 'caller-mutated-replacement';
    replacementDetails.origin.focusKey = 'caller-mutated-replacement-focus';

    expect(history).toEqual([
      { kind: 'home' },
      {
        kind: 'library',
        libraryId: 'library-replace-anonymous',
        collectionType: 'tvshows',
        origin: {
          scopeKey: 'home-replace-anonymous',
          focusKey: 'library-replace-focus-anonymous',
        },
      },
      {
        kind: 'details',
        itemId: 'item-replacement-anonymous',
        origin: {
          scopeKey: 'library-replace-grid-anonymous',
          focusKey: 'item-replacement-focus-anonymous',
        },
      },
    ]);
  });

  it('returns the original history reference for illegal and repeated replacements', () => {
    const homeStack = Object.freeze([homeRoute] as BrowseRoute[]);
    const detailsStack = Object.freeze([homeRoute, homeDetailsRoute] as BrowseRoute[]);

    const illegal = replaceRoute(homeStack, homeDetailsRoute);
    const repeated = replaceRoute(detailsStack, { ...homeDetailsRoute });

    expect(illegal).toBe(homeStack);
    expect(repeated).toBe(detailsStack);
  });

  it('backs through both details histories while preserving Home and empty roots', () => {
    const nestedDetails = Object.freeze([
      homeRoute,
      libraryRoute,
      libraryDetailsRoute,
    ] as BrowseRoute[]);
    const directDetails = Object.freeze([homeRoute, homeDetailsRoute] as BrowseRoute[]);
    const home = Object.freeze([homeRoute] as BrowseRoute[]);
    const empty = Object.freeze([] as BrowseRoute[]);

    const fromNestedDetails = backRoute(nestedDetails);
    const fromDirectDetails = backRoute(directDetails);
    const fromHome = backRoute(home);
    const fromEmpty = backRoute(empty);

    expect(fromNestedDetails).toEqual([homeRoute, libraryRoute]);
    expect(fromDirectDetails).toEqual([homeRoute]);
    expect(fromHome).toBe(home);
    expect(fromEmpty).toBe(empty);
    expect(fromNestedDetails).not.toBe(nestedDetails);
    expect(fromDirectDetails).not.toBe(directDetails);
  });

  it('deep snapshots routes retained by Back', () => {
    const callerLibrary = {
      kind: 'library' as const,
      libraryId: 'library-back-anonymous',
      collectionType: 'movies',
      origin: {
        scopeKey: 'home-back-anonymous',
        focusKey: 'library-back-focus-anonymous',
      },
    };
    const callerDetails = {
      kind: 'details' as const,
      itemId: 'item-back-anonymous',
      origin: {
        scopeKey: 'library-back-grid-anonymous',
        focusKey: 'item-back-focus-anonymous',
      },
    };

    const history = backRoute([{ kind: 'home' }, callerLibrary, callerDetails]);

    callerLibrary.libraryId = 'caller-mutated-library';
    callerLibrary.origin.focusKey = 'caller-mutated-library-focus';

    expect(history).toEqual([
      { kind: 'home' },
      {
        kind: 'library',
        libraryId: 'library-back-anonymous',
        collectionType: 'movies',
        origin: {
          scopeKey: 'home-back-anonymous',
          focusKey: 'library-back-focus-anonymous',
        },
      },
    ]);
  });

  it('preserves the exact current history for an illegal no-op', () => {
    const callerDetails = {
      kind: 'details' as const,
      itemId: 'item-illegal-anonymous',
      origin: {
        scopeKey: 'home-illegal-row-anonymous',
        focusKey: 'item-illegal-focus-anonymous',
      },
    };
    const illegalLibrary = {
      kind: 'library' as const,
      libraryId: 'library-illegal-anonymous',
      collectionType: 'movies',
      origin: {
        scopeKey: 'home-illegal-anonymous',
        focusKey: 'library-illegal-focus-anonymous',
      },
    };

    const currentHistory = Object.freeze([{ kind: 'home' }, callerDetails] as const);

    expect(pushRoute(currentHistory, illegalLibrary)).toBe(currentHistory);
  });

  it('preserves the exact current history for a repeated no-op', () => {
    const callerDetails = {
      kind: 'details' as const,
      itemId: 'item-repeated-anonymous',
      origin: {
        scopeKey: 'home-repeated-row-anonymous',
        focusKey: 'item-repeated-focus-anonymous',
      },
    };
    const repeatedDetails = {
      kind: 'details' as const,
      itemId: 'item-repeated-anonymous',
      origin: {
        scopeKey: 'home-repeated-row-anonymous',
        focusKey: 'item-repeated-focus-anonymous',
      },
    };

    const currentHistory = Object.freeze([{ kind: 'home' }, callerDetails] as const);

    expect(replaceRoute(currentHistory, repeatedDetails)).toBe(currentHistory);
  });
});

describe('browse route validation', () => {
  const validOrigin: FocusTarget = {
    scopeKey: 'validation-scope-anonymous',
    focusKey: 'validation-focus-anonymous',
  };
  const validLibrary: BrowseRoute = {
    kind: 'library',
    libraryId: 'validation-library-anonymous',
    collectionType: 'movies',
    origin: validOrigin,
  };
  const validDetails: BrowseRoute = {
    kind: 'details',
    itemId: 'validation-item-anonymous',
    origin: validOrigin,
  };

  it('accepts the empty bootstrap state and four supported history shapes', () => {
    expect(isValidBrowseRouteStack([])).toBe(true);
    expect(isValidBrowseRouteStack([homeRoute])).toBe(true);
    expect(isValidBrowseRouteStack([homeRoute, validLibrary])).toBe(true);
    expect(isValidBrowseRouteStack([homeRoute, validLibrary, validDetails])).toBe(true);
    expect(isValidBrowseRouteStack([homeRoute, validDetails])).toBe(true);
  });

  it('does not call a caller-owned every override while validating', () => {
    const routeStack = [homeRoute];
    Object.defineProperty(routeStack, 'every', {
      value: () => {
        throw new Error('caller-owned every must not run');
      },
    });

    expect(isValidBrowseRouteStack(routeStack)).toBe(true);
  });

  it.each([
    ['non-array', null],
    ['library root', [validLibrary]],
    ['second Home', [homeRoute, homeRoute]],
    ['Details then Details', [homeRoute, validDetails, validDetails]],
    ['library then library', [homeRoute, validLibrary, validLibrary]],
    ['too deep', [homeRoute, validLibrary, validDetails, validDetails]],
  ])('rejects malformed route order: %s', (_label, candidate) => {
    expect(isValidBrowseRouteStack(candidate)).toBe(false);
  });

  it.each([
    [
      'blank libraryId',
      [
        homeRoute,
        { ...validLibrary, libraryId: '   ' },
      ],
    ],
    [
      'blank collectionType',
      [
        homeRoute,
        { ...validLibrary, collectionType: '' },
      ],
    ],
    [
      'blank itemId',
      [
        homeRoute,
        { ...validDetails, itemId: '\t' },
      ],
    ],
    [
      'blank origin scope',
      [
        homeRoute,
        { ...validDetails, origin: { ...validOrigin, scopeKey: ' ' } },
      ],
    ],
    [
      'blank origin focus',
      [
        homeRoute,
        { ...validDetails, origin: { ...validOrigin, focusKey: '' } },
      ],
    ],
    ['missing origin', [homeRoute, { kind: 'details', itemId: 'missing-origin' }]],
    ['unknown kind', [homeRoute, { kind: 'unknown' }]],
  ])('rejects runtime-invalid route data: %s', (_label, candidate) => {
    expect(isValidBrowseRouteStack(candidate)).toBe(false);
  });

  it('makes every route operation fail closed on malformed stacks and routes', () => {
    const malformedStack = Object.freeze([
      homeRoute,
      homeRoute,
    ]) as unknown as readonly BrowseRoute[];
    const validStack = Object.freeze([homeRoute] as BrowseRoute[]);
    const invalidRoute = {
      kind: 'details',
      itemId: '',
      origin: validOrigin,
    } as unknown as BrowseRoute;

    expect(pushRoute(malformedStack, validDetails)).toBe(malformedStack);
    expect(replaceRoute(malformedStack, validDetails)).toBe(malformedStack);
    expect(backRoute(malformedStack)).toBe(malformedStack);
    expect(pushRoute(validStack, invalidRoute)).toBe(validStack);
    expect(replaceRoute(validStack, invalidRoute)).toBe(validStack);
  });
});

describe('atomic browse route normalization', () => {
  it('uses the first route-property snapshot when later reads become blank', () => {
    const homeStack = Object.freeze([homeRoute] as BrowseRoute[]);
    const reads = { kind: 0, itemId: 0, origin: 0 };
    const statefulDetails = Object.defineProperties({}, {
      kind: {
        enumerable: true,
        get: () => {
          reads.kind += 1;
          return 'details';
        },
      },
      itemId: {
        enumerable: true,
        get: () => {
          reads.itemId += 1;
          return reads.itemId === 1 ? 'atomic-item-anonymous' : ' ';
        },
      },
      origin: {
        enumerable: true,
        get: () => {
          reads.origin += 1;
          return {
            scopeKey: 'atomic-route-scope-anonymous',
            focusKey: 'atomic-route-focus-anonymous',
          };
        },
      },
    }) as unknown as BrowseRoute;

    const history = pushRoute(homeStack, statefulDetails);

    expect(history).toEqual([
      homeRoute,
      {
        kind: 'details',
        itemId: 'atomic-item-anonymous',
        origin: {
          scopeKey: 'atomic-route-scope-anonymous',
          focusKey: 'atomic-route-focus-anonymous',
        },
      },
    ]);
    expect(history).not.toBe(homeStack);
    expect(reads).toEqual({ kind: 1, itemId: 1, origin: 1 });
    expect(Object.getOwnPropertyDescriptor(history[1], 'itemId')?.get).toBeUndefined();
  });

  it('reads nested FocusTarget getters once when later reads throw', () => {
    const homeStack = Object.freeze([homeRoute] as BrowseRoute[]);
    const reads = { scopeKey: 0, focusKey: 0 };
    const statefulTarget = Object.defineProperties({}, {
      scopeKey: {
        enumerable: true,
        get: () => {
          reads.scopeKey += 1;
          if (reads.scopeKey > 1) {
            throw new Error('scopeKey was read more than once');
          }
          return 'atomic-target-scope-anonymous';
        },
      },
      focusKey: {
        enumerable: true,
        get: () => {
          reads.focusKey += 1;
          if (reads.focusKey > 1) {
            throw new Error('focusKey was read more than once');
          }
          return 'atomic-target-focus-anonymous';
        },
      },
    });
    const statefulDetails = {
      kind: 'details',
      itemId: 'atomic-target-item-anonymous',
      origin: statefulTarget,
    } as unknown as BrowseRoute;

    const history = pushRoute(homeStack, statefulDetails);

    expect(history).toEqual([
      homeRoute,
      {
        kind: 'details',
        itemId: 'atomic-target-item-anonymous',
        origin: {
          scopeKey: 'atomic-target-scope-anonymous',
          focusKey: 'atomic-target-focus-anonymous',
        },
      },
    ]);
    expect(reads).toEqual({ scopeKey: 1, focusKey: 1 });
  });

  it('reads each route-stack index once before returning a successful Back snapshot', () => {
    const routeStack = [homeRoute, libraryRoute, libraryDetailsRoute];
    let libraryIndexReads = 0;
    Object.defineProperty(routeStack, 1, {
      configurable: true,
      enumerable: true,
      get: () => {
        libraryIndexReads += 1;
        if (libraryIndexReads > 1) {
          throw new Error('route stack index was read more than once');
        }
        return libraryRoute;
      },
    });

    let history: readonly BrowseRoute[] | undefined;
    expect(() => {
      history = backRoute(routeStack);
    }).not.toThrow();

    expect(history).toEqual([homeRoute, libraryRoute]);
    expect(libraryIndexReads).toBe(1);
  });

  it('fails closed when a getter throws on its first normalization read', () => {
    const homeStack = Object.freeze([homeRoute] as BrowseRoute[]);
    const throwingRoute = Object.defineProperty({}, 'kind', {
      get: () => {
        throw new Error('first route read failed');
      },
    }) as unknown as BrowseRoute;

    let history: readonly BrowseRoute[] | undefined;
    expect(() => {
      history = pushRoute(homeStack, throwingRoute);
    }).not.toThrow();

    expect(history).toBe(homeStack);
    expect(isValidBrowseRouteStack([throwingRoute])).toBe(false);
  });
});
