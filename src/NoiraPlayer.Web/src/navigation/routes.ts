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
): BrowseRoute[] {
  const currentStack = cloneStack(routeStack);
  const currentRoute = currentStack[currentStack.length - 1];
  if (currentRoute && routesEqual(currentRoute, route)) {
    return currentStack;
  }

  const candidate = [...currentStack, cloneRoute(route)];
  return isSupportedRouteStack(candidate) ? candidate : currentStack;
}

export function replaceRoute(
  routeStack: readonly BrowseRoute[],
  route: BrowseRoute,
): BrowseRoute[] {
  const currentStack = cloneStack(routeStack);
  const currentRoute = currentStack[currentStack.length - 1];
  if (currentRoute && routesEqual(currentRoute, route)) {
    return currentStack;
  }

  const candidate = currentRoute
    ? [...currentStack.slice(0, -1), cloneRoute(route)]
    : [cloneRoute(route)];
  return isSupportedRouteStack(candidate) ? candidate : currentStack;
}

export function backRoute(routeStack: readonly BrowseRoute[]): BrowseRoute[] {
  const currentStack = cloneStack(routeStack);
  return currentStack.length > 1 ? currentStack.slice(0, -1) : currentStack;
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

function cloneStack(routeStack: readonly BrowseRoute[]): BrowseRoute[] {
  return routeStack.map(cloneRoute);
}

function isSupportedRouteStack(routeStack: readonly BrowseRoute[]): boolean {
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
