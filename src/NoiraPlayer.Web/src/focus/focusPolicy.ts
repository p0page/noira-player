import {
  normalizeBrowseRouteStack,
  normalizeFocusTarget,
} from '../navigation/routes';
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
  clear(): void;
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
  index: number;
}

type PauseListener = (paused: boolean) => void;

class InMemoryFocusNavigationPolicy implements FocusNavigationPolicy {
  private readonly snapshots = new Map<string, FocusSnapshot>();
  private readonly pauseListeners = new Set<PauseListener>();
  private paused = false;
  private pauseDeliveryActive = false;
  private queuedPauseState: boolean | null = null;

  remember(scopeKey: string, focusKey: string, orderedKeys: readonly string[]): void {
    assertNonBlankKey(scopeKey, 'scopeKey');
    assertNonBlankKey(focusKey, 'focusKey');
    const orderedKeysSnapshot = snapshotKeys(orderedKeys, 'orderedKeys');
    const rememberedIndex = orderedKeysSnapshot.indexOf(focusKey);
    if (rememberedIndex < 0) {
      throw new RangeError('focusKey must be present in orderedKeys.');
    }

    this.snapshots.set(scopeKey, {
      focusKey,
      index: rememberedIndex,
    });
  }

  clear(): void {
    this.snapshots.clear();
  }

  resolve(scopeKey: string, availableKeys: readonly string[]): string | null {
    assertNonBlankKey(scopeKey, 'scopeKey');
    const availableKeysSnapshot = snapshotKeys(availableKeys, 'availableKeys');
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
    const availableKeysSnapshot = snapshotKeys(availableKeys, 'availableKeys');
    if (defaultKey !== undefined) {
      assertNonBlankKey(defaultKey, 'defaultKey');
      if (availableKeysSnapshot.includes(defaultKey)) {
        return defaultKey;
      }
    }

    return availableKeysSnapshot[0] ?? null;
  }

  decideBack(
    routeStack: readonly BrowseRoute[],
    transientLayer?: TransientLayer,
  ): BackDecision {
    if (transientLayer !== undefined) {
      const transientSnapshot = normalizeTransientLayer(transientLayer);
      if (!transientSnapshot) {
        return { kind: 'nativeBack' };
      }

      return {
        kind: 'closeTransient',
        layer: transientSnapshot.kind,
        restoreTarget: transientSnapshot.returnTarget,
      };
    }

    const stackSnapshot = normalizeBrowseRouteStack(routeStack);
    if (!stackSnapshot) {
      return { kind: 'nativeBack' };
    }

    const currentRoute = stackSnapshot[stackSnapshot.length - 1];
    const previousRoute = stackSnapshot[stackSnapshot.length - 2];
    if (!currentRoute || currentRoute.kind === 'home' || !previousRoute) {
      return { kind: 'nativeBack' };
    }

    return {
      kind: 'navigate',
      route: previousRoute,
      restoreTarget: currentRoute.origin,
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
    try {
      subscription(this.paused);
    } catch (error) {
      this.pauseListeners.delete(subscription);
      throw error;
    }

    return () => {
      this.pauseListeners.delete(subscription);
    };
  }

  private setPaused(paused: boolean): void {
    if (this.pauseDeliveryActive) {
      this.queuedPauseState = paused;
      return;
    }

    if (this.paused === paused) {
      return;
    }

    let nextState: boolean | null = paused;
    let firstError: unknown;
    let hasError = false;
    this.pauseDeliveryActive = true;

    try {
      while (nextState !== null) {
        const stateToDeliver = nextState;
        this.queuedPauseState = null;

        if (stateToDeliver !== this.paused) {
          this.paused = stateToDeliver;
          for (const listener of [...this.pauseListeners]) {
            if (!this.pauseListeners.has(listener)) {
              continue;
            }

            try {
              listener(stateToDeliver);
            } catch (error) {
              if (!hasError) {
                firstError = error;
                hasError = true;
              }
            }
          }
        }

        nextState = this.queuedPauseState;
      }
    } finally {
      this.pauseDeliveryActive = false;
      this.queuedPauseState = null;
    }

    if (hasError) {
      throw firstError;
    }
  }
}

export function createFocusNavigationPolicy(): FocusNavigationPolicy {
  return new InMemoryFocusNavigationPolicy();
}

function normalizeTransientLayer(candidate: unknown): TransientLayer | null {
  try {
    if (!isRecord(candidate)) {
      return null;
    }

    const kind = candidate.kind;
    const returnTargetCandidate = candidate.returnTarget;
    if (kind !== 'guide' && kind !== 'overlay') {
      return null;
    }

    const returnTarget = normalizeFocusTarget(returnTargetCandidate);
    return returnTarget ? { kind, returnTarget } : null;
  } catch {
    return null;
  }
}

function snapshotKeys(
  keys: readonly string[],
  label: 'availableKeys' | 'orderedKeys',
): string[] {
  const snapshot = [...keys];
  if (snapshot.some((key) => typeof key !== 'string' || key.trim().length === 0)) {
    throw new Error(`${label} must contain only non-blank keys.`);
  }

  if (new Set(snapshot).size !== snapshot.length) {
    throw new Error(`${label} must contain unique keys.`);
  }

  return snapshot;
}

function assertNonBlankKey(candidate: unknown, label: string): asserts candidate is string {
  if (typeof candidate !== 'string' || candidate.trim().length === 0) {
    throw new Error(`${label} must be a non-blank string.`);
  }
}

function isRecord(candidate: unknown): candidate is Record<string, unknown> {
  return typeof candidate === 'object' && candidate !== null && !Array.isArray(candidate);
}
