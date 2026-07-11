import type { BrowseRoute, FocusTarget } from '../navigation/routes';

export interface TransientLayer {
  kind: 'guide' | 'overlay';
  returnTarget: FocusTarget;
}

export type BackDecision =
  | {
      kind: 'closeTransient';
      layer: TransientLayer['kind'];
      restoreTarget: FocusTarget;
    }
  | { kind: 'navigate'; route: BrowseRoute; restoreTarget: FocusTarget }
  | { kind: 'nativeBack' };

export interface FocusNavigationPolicy {
  remember(scopeKey: string, focusKey: string, orderedKeys: readonly string[]): void;
  resolve(scopeKey: string, availableKeys: readonly string[]): string | null;
  resolveInitial(availableKeys: readonly string[], defaultKey?: string): string | null;
  decideBack(
    routeStack: readonly BrowseRoute[],
    transientLayer?: TransientLayer,
  ): BackDecision;
  pause(): void;
  resume(): void;
  isPaused(): boolean;
  subscribePause(listener: (paused: boolean) => void): () => void;
}

interface FocusSnapshot {
  focusKey: string;
  orderedKeys: readonly string[];
  index: number;
}

type PauseListener = (paused: boolean) => void;

class InMemoryFocusNavigationPolicy implements FocusNavigationPolicy {
  private readonly snapshots = new Map<string, FocusSnapshot>();
  private readonly pauseListeners = new Set<PauseListener>();
  private paused = false;

  remember(scopeKey: string, focusKey: string, orderedKeys: readonly string[]): void {
    const orderedKeysSnapshot = [...orderedKeys];
    this.snapshots.set(scopeKey, {
      focusKey,
      orderedKeys: orderedKeysSnapshot,
      index: orderedKeysSnapshot.indexOf(focusKey),
    });
  }

  resolve(scopeKey: string, availableKeys: readonly string[]): string | null {
    const availableKeysSnapshot = [...availableKeys];
    if (availableKeysSnapshot.length === 0) {
      return null;
    }

    const snapshot = this.snapshots.get(scopeKey);
    if (!snapshot) {
      return availableKeysSnapshot[0] ?? null;
    }

    if (availableKeysSnapshot.includes(snapshot.focusKey)) {
      return snapshot.focusKey;
    }

    if (snapshot.index >= 0) {
      const nearestIndex = Math.min(snapshot.index, availableKeysSnapshot.length - 1);
      return availableKeysSnapshot[nearestIndex] ?? availableKeysSnapshot[0] ?? null;
    }

    return availableKeysSnapshot[0] ?? null;
  }

  resolveInitial(availableKeys: readonly string[], defaultKey?: string): string | null {
    const availableKeysSnapshot = [...availableKeys];
    if (defaultKey !== undefined && availableKeysSnapshot.includes(defaultKey)) {
      return defaultKey;
    }

    return availableKeysSnapshot[0] ?? null;
  }

  decideBack(
    routeStack: readonly BrowseRoute[],
    transientLayer?: TransientLayer,
  ): BackDecision {
    if (transientLayer) {
      return {
        kind: 'closeTransient',
        layer: transientLayer.kind,
        restoreTarget: cloneTarget(transientLayer.returnTarget),
      };
    }

    const currentRoute = routeStack[routeStack.length - 1];
    const previousRoute = routeStack[routeStack.length - 2];
    if (!currentRoute || currentRoute.kind === 'home' || !previousRoute) {
      return { kind: 'nativeBack' };
    }

    return {
      kind: 'navigate',
      route: cloneRoute(previousRoute),
      restoreTarget: cloneTarget(currentRoute.origin),
    };
  }

  pause(): void {
    this.setPaused(true);
  }

  resume(): void {
    this.setPaused(false);
  }

  isPaused(): boolean {
    return this.paused;
  }

  subscribePause(listener: PauseListener): () => void {
    const subscription = (paused: boolean) => listener(paused);
    this.pauseListeners.add(subscription);
    listener(this.paused);

    return () => {
      this.pauseListeners.delete(subscription);
    };
  }

  private setPaused(paused: boolean): void {
    if (this.paused === paused) {
      return;
    }

    this.paused = paused;
    for (const listener of [...this.pauseListeners]) {
      listener(paused);
    }
  }
}

export function createFocusNavigationPolicy(): FocusNavigationPolicy {
  return new InMemoryFocusNavigationPolicy();
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
