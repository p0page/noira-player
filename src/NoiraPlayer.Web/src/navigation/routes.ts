export interface FocusTarget {
  readonly scopeKey: string;
  readonly focusKey: string;
}

export type BrowseRoute =
  | { readonly kind: 'home' }
  | {
      readonly kind: 'library';
      readonly libraryId: string;
      readonly collectionType: string;
      readonly origin: FocusTarget;
    }
  | {
      readonly kind: 'details';
      readonly itemId: string;
      readonly origin: FocusTarget;
    };

export function pushRoute(
  routeStack: readonly BrowseRoute[],
  route: BrowseRoute,
): readonly BrowseRoute[] {
  if (!isValidBrowseRouteStack(routeStack) || !isValidBrowseRoute(route)) {
    return routeStack;
  }

  const currentRoute = routeStack[routeStack.length - 1];
  if (currentRoute && routesEqual(currentRoute, route)) {
    return routeStack;
  }

  const candidate = [...routeStack, route];
  return isValidBrowseRouteStack(candidate) ? cloneStack(candidate) : routeStack;
}

export function replaceRoute(
  routeStack: readonly BrowseRoute[],
  route: BrowseRoute,
): readonly BrowseRoute[] {
  if (!isValidBrowseRouteStack(routeStack) || !isValidBrowseRoute(route)) {
    return routeStack;
  }

  const currentRoute = routeStack[routeStack.length - 1];
  if (currentRoute && routesEqual(currentRoute, route)) {
    return routeStack;
  }

  const candidate = currentRoute
    ? [...routeStack.slice(0, -1), route]
    : [route];
  return isValidBrowseRouteStack(candidate) ? cloneStack(candidate) : routeStack;
}

export function backRoute(routeStack: readonly BrowseRoute[]): readonly BrowseRoute[] {
  if (!isValidBrowseRouteStack(routeStack) || routeStack.length <= 1) {
    return routeStack;
  }

  return cloneStack(routeStack.slice(0, -1));
}

export function isValidFocusTarget(candidate: unknown): candidate is FocusTarget {
  try {
    return (
      isRecord(candidate) &&
      isNonBlankString(candidate.scopeKey) &&
      isNonBlankString(candidate.focusKey)
    );
  } catch {
    return false;
  }
}

export function isValidBrowseRouteStack(
  candidate: unknown,
): candidate is readonly BrowseRoute[] {
  try {
    if (!Array.isArray(candidate) || !candidate.every(isValidBrowseRoute)) {
      return false;
    }

    if (candidate.length === 0) {
      return true;
    }

    if (candidate[0].kind !== 'home') {
      return false;
    }

    if (candidate.length === 1) {
      return true;
    }

    const secondRoute = candidate[1];
    if (candidate.length === 2) {
      return secondRoute.kind === 'library' || secondRoute.kind === 'details';
    }

    return (
      candidate.length === 3 &&
      secondRoute.kind === 'library' &&
      candidate[2].kind === 'details'
    );
  } catch {
    return false;
  }
}

function cloneTarget(target: FocusTarget): FocusTarget {
  return {
    scopeKey: target.scopeKey,
    focusKey: target.focusKey,
  };
}

function cloneRoute(route: BrowseRoute): BrowseRoute {
  switch (route.kind) {
    case 'home':
      return { kind: 'home' };
    case 'library':
      return {
        kind: 'library',
        libraryId: route.libraryId,
        collectionType: route.collectionType,
        origin: cloneTarget(route.origin),
      };
    case 'details':
      return {
        kind: 'details',
        itemId: route.itemId,
        origin: cloneTarget(route.origin),
      };
  }
}

function cloneStack(routeStack: readonly BrowseRoute[]): readonly BrowseRoute[] {
  return routeStack.map(cloneRoute);
}

function isValidBrowseRoute(candidate: unknown): candidate is BrowseRoute {
  try {
    if (!isRecord(candidate)) {
      return false;
    }

    switch (candidate.kind) {
      case 'home':
        return true;
      case 'library':
        return (
          isNonBlankString(candidate.libraryId) &&
          isNonBlankString(candidate.collectionType) &&
          isValidFocusTarget(candidate.origin)
        );
      case 'details':
        return (
          isNonBlankString(candidate.itemId) && isValidFocusTarget(candidate.origin)
        );
      default:
        return false;
    }
  } catch {
    return false;
  }
}

function routesEqual(left: BrowseRoute, right: BrowseRoute): boolean {
  if (left.kind !== right.kind) {
    return false;
  }

  switch (left.kind) {
    case 'home':
      return true;
    case 'library':
      return (
        right.kind === 'library' &&
        left.libraryId === right.libraryId &&
        left.collectionType === right.collectionType &&
        targetsEqual(left.origin, right.origin)
      );
    case 'details':
      return (
        right.kind === 'details' &&
        left.itemId === right.itemId &&
        targetsEqual(left.origin, right.origin)
      );
  }
}

function targetsEqual(left: FocusTarget, right: FocusTarget): boolean {
  return left.scopeKey === right.scopeKey && left.focusKey === right.focusKey;
}

function isRecord(candidate: unknown): candidate is Record<string, unknown> {
  return typeof candidate === 'object' && candidate !== null && !Array.isArray(candidate);
}

function isNonBlankString(candidate: unknown): candidate is string {
  return typeof candidate === 'string' && candidate.trim().length > 0;
}
