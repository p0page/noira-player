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
  const stackSnapshot = normalizeBrowseRouteStack(routeStack);
  const routeSnapshot = normalizeBrowseRoute(route);
  if (!stackSnapshot || !routeSnapshot) {
    return routeStack;
  }

  const currentRoute = stackSnapshot[stackSnapshot.length - 1];
  if (currentRoute && routesEqual(currentRoute, routeSnapshot)) {
    return routeStack;
  }

  const candidate = appendRoute(stackSnapshot, routeSnapshot);
  return hasSupportedRouteOrder(candidate) ? candidate : routeStack;
}

export function replaceRoute(
  routeStack: readonly BrowseRoute[],
  route: BrowseRoute,
): readonly BrowseRoute[] {
  const stackSnapshot = normalizeBrowseRouteStack(routeStack);
  const routeSnapshot = normalizeBrowseRoute(route);
  if (!stackSnapshot || !routeSnapshot) {
    return routeStack;
  }

  const currentRoute = stackSnapshot[stackSnapshot.length - 1];
  if (currentRoute && routesEqual(currentRoute, routeSnapshot)) {
    return routeStack;
  }

  const retainedLength = currentRoute ? stackSnapshot.length - 1 : 0;
  const candidate = new Array<BrowseRoute>(retainedLength + 1);
  for (let index = 0; index < retainedLength; index += 1) {
    candidate[index] = stackSnapshot[index];
  }
  candidate[retainedLength] = routeSnapshot;

  return hasSupportedRouteOrder(candidate) ? candidate : routeStack;
}

export function backRoute(routeStack: readonly BrowseRoute[]): readonly BrowseRoute[] {
  const stackSnapshot = normalizeBrowseRouteStack(routeStack);
  if (!stackSnapshot || stackSnapshot.length <= 1) {
    return routeStack;
  }

  const previousStack = new Array<BrowseRoute>(stackSnapshot.length - 1);
  for (let index = 0; index < previousStack.length; index += 1) {
    previousStack[index] = stackSnapshot[index];
  }
  return previousStack;
}

export function isValidFocusTarget(candidate: unknown): candidate is FocusTarget {
  return normalizeFocusTarget(candidate) !== null;
}

export function isValidBrowseRouteStack(
  candidate: unknown,
): candidate is readonly BrowseRoute[] {
  return normalizeBrowseRouteStack(candidate) !== null;
}

export function normalizeFocusTarget(candidate: unknown): FocusTarget | null {
  try {
    if (!isRecord(candidate)) {
      return null;
    }

    const scopeKey = candidate.scopeKey;
    const focusKey = candidate.focusKey;
    if (!isNonBlankString(scopeKey) || !isNonBlankString(focusKey)) {
      return null;
    }

    return { scopeKey, focusKey };
  } catch {
    return null;
  }
}

export function normalizeBrowseRouteStack(
  candidate: unknown,
): readonly BrowseRoute[] | null {
  try {
    if (!Array.isArray(candidate)) {
      return null;
    }

    const length = candidate.length;
    const snapshot = new Array<BrowseRoute>(length);
    for (let index = 0; index < length; index += 1) {
      const routeCandidate = candidate[index];
      const routeSnapshot = normalizeBrowseRoute(routeCandidate);
      if (!routeSnapshot) {
        return null;
      }
      snapshot[index] = routeSnapshot;
    }

    return hasSupportedRouteOrder(snapshot) ? snapshot : null;
  } catch {
    return null;
  }
}

function normalizeBrowseRoute(candidate: unknown): BrowseRoute | null {
  try {
    if (!isRecord(candidate)) {
      return null;
    }

    const kind = candidate.kind;
    switch (kind) {
      case 'home':
        return { kind };
      case 'library': {
        const libraryId = candidate.libraryId;
        const collectionType = candidate.collectionType;
        const originCandidate = candidate.origin;
        if (!isNonBlankString(libraryId) || !isNonBlankString(collectionType)) {
          return null;
        }

        const origin = normalizeFocusTarget(originCandidate);
        return origin ? { kind, libraryId, collectionType, origin } : null;
      }
      case 'details': {
        const itemId = candidate.itemId;
        const originCandidate = candidate.origin;
        if (!isNonBlankString(itemId)) {
          return null;
        }

        const origin = normalizeFocusTarget(originCandidate);
        return origin ? { kind, itemId, origin } : null;
      }
      default:
        return null;
    }
  } catch {
    return null;
  }
}

function appendRoute(
  routeStack: readonly BrowseRoute[],
  route: BrowseRoute,
): BrowseRoute[] {
  const candidate = new Array<BrowseRoute>(routeStack.length + 1);
  for (let index = 0; index < routeStack.length; index += 1) {
    candidate[index] = routeStack[index];
  }
  candidate[routeStack.length] = route;
  return candidate;
}

function hasSupportedRouteOrder(routeStack: readonly BrowseRoute[]): boolean {
  if (routeStack.length === 0) {
    return true;
  }

  if (routeStack[0].kind !== 'home') {
    return false;
  }

  if (routeStack.length === 1) {
    return true;
  }

  const secondRoute = routeStack[1];
  if (routeStack.length === 2) {
    return secondRoute.kind === 'library' || secondRoute.kind === 'details';
  }

  return (
    routeStack.length === 3 &&
    secondRoute.kind === 'library' &&
    routeStack[2].kind === 'details'
  );
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
