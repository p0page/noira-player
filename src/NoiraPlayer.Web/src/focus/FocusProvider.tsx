import {
  GetBoundingClientRectAdapter,
  doesFocusableExist,
  getCurrentFocusKey,
  init,
  pause as pauseSpatialNavigation,
  resume as resumeSpatialNavigation,
  setFocus,
} from '@noriginmedia/norigin-spatial-navigation';
import { createContext, useContext, useLayoutEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import {
  createFocusNavigationPolicy,
  type FocusNavigationPolicy,
} from './focusPolicy';

export interface FocusProviderProps {
  children: ReactNode;
  policy?: FocusNavigationPolicy;
}

type ScopeRegistrationChange =
  | 'added'
  | 'disabled'
  | 'enabled'
  | 'order'
  | 'removed';

type ScopeRegistrationListener = (
  change: ScopeRegistrationChange,
  focusKey?: string,
) => void;

export interface NoiraFocusScopeController {
  readonly scopeKey: string;
  attachRegistry(listener: ScopeRegistrationListener | null): void;
  getEnabledKeys(orderedKeys?: readonly string[]): readonly string[];
  getSnapshot(): number;
  registerChild(focusKey: string, enabled: boolean): () => void;
  setChildEnabled(focusKey: string, enabled: boolean): void;
  setOrderedKeys(orderedKeys: readonly string[]): void;
  subscribe(listener: () => void): () => void;
}

export interface NoiraFocusScopeState {
  controller: NoiraFocusScopeController;
  orderedKeys: readonly string[];
  scopeKey: string;
}

interface NoiraFocusRestoreRequest {
  requestId: number | string | undefined;
  requestedFocusKey: string;
  scopeKey: string;
  targetFocusKey: string;
}

export interface NoiraFocusRegistry {
  claimFocusKey(focusKey: string, owner: object): () => void;
  hasValidCurrentTarget(): boolean;
  registerScope(controller: NoiraFocusScopeController): () => void;
  requestInitialFocus(focusKey: string): void;
  requestRestoreFocus(request: NoiraFocusRestoreRequest): void;
}

const FocusPolicyContext = createContext<FocusNavigationPolicy | null>(null);
const FocusRegistryContext = createContext<NoiraFocusRegistry | null>(null);
const FocusScopeStateContext = createContext<NoiraFocusScopeState | null>(null);

let spatialNavigationInitialized = false;

export function FocusProvider({ children, policy }: FocusProviderProps) {
  const defaultPolicyRef = useRef<FocusNavigationPolicy | null>(null);
  if (defaultPolicyRef.current === null) {
    defaultPolicyRef.current = createFocusNavigationPolicy();
  }

  const focusPolicy = policy ?? defaultPolicyRef.current;
  const registryRef = useRef<NoiraFocusRegistryImpl | null>(null);
  if (registryRef.current === null) {
    registryRef.current = new NoiraFocusRegistryImpl(focusPolicy);
  }
  registryRef.current.setPolicy(focusPolicy);

  useLayoutEffect(
    () => {
      ensureSpatialNavigationInitialized();

      return focusPolicy.subscribePause((paused) => {
        if (paused) {
          pauseSpatialNavigation();
        } else {
          resumeSpatialNavigation();
        }
      });
    },
    [focusPolicy],
  );

  return (
    <FocusPolicyContext.Provider value={focusPolicy}>
      <FocusRegistryContext.Provider value={registryRef.current}>
        {children}
      </FocusRegistryContext.Provider>
    </FocusPolicyContext.Provider>
  );
}

export function useFocusNavigationPolicy(): FocusNavigationPolicy {
  const policy = useContext(FocusPolicyContext);
  if (!policy) {
    throw new Error('Noira focus components must be rendered inside FocusProvider.');
  }

  return policy;
}

export function useNoiraFocusRegistry(): NoiraFocusRegistry {
  const registry = useContext(FocusRegistryContext);
  if (!registry) {
    throw new Error('Noira focus components must be rendered inside FocusProvider.');
  }

  return registry;
}

export function useNoiraFocusScope(): NoiraFocusScopeState {
  const scope = useContext(FocusScopeStateContext);
  if (!scope) {
    throw new Error('Focusable must be rendered inside FocusScope.');
  }

  return scope;
}

export const NoiraFocusScopeContext = FocusScopeStateContext;

export function createNoiraFocusScopeController(
  scopeKey: string,
  orderedKeys: readonly string[],
): NoiraFocusScopeController {
  return new NoiraFocusScopeControllerImpl(scopeKey, orderedKeys);
}

class NoiraFocusScopeControllerImpl implements NoiraFocusScopeController {
  readonly scopeKey: string;

  private readonly children = new Map<string, boolean>();
  private readonly listeners = new Set<() => void>();
  private orderedKeys: readonly string[];
  private registryListener: ScopeRegistrationListener | null = null;
  private version = 0;

  constructor(scopeKey: string, orderedKeys: readonly string[]) {
    assertNonBlankKey(scopeKey, 'scopeKey');
    this.scopeKey = scopeKey;
    this.orderedKeys = snapshotOrderedKeys(orderedKeys);
  }

  attachRegistry(listener: ScopeRegistrationListener | null): void {
    this.registryListener = listener;
  }

  getEnabledKeys(orderedKeys: readonly string[] = this.orderedKeys): readonly string[] {
    return snapshotOrderedKeys(orderedKeys).filter(
      (focusKey) => this.children.get(focusKey) === true,
    );
  }

  getSnapshot = (): number => this.version;

  registerChild(focusKey: string, enabled: boolean): () => void {
    assertNonBlankKey(focusKey, 'focusKey');
    if (this.children.has(focusKey)) {
      throw new Error(
        `Focusable focusKey "${focusKey}" is already mounted in FocusScope "${this.scopeKey}".`,
      );
    }

    this.children.set(focusKey, enabled);
    try {
      this.notify('added', focusKey);
    } catch (error) {
      this.children.delete(focusKey);
      throw error;
    }
    let registered = true;

    return () => {
      if (!registered) {
        return;
      }

      registered = false;
      this.children.delete(focusKey);
      this.notify('removed', focusKey);
    };
  }

  setChildEnabled(focusKey: string, enabled: boolean): void {
    const previous = this.children.get(focusKey);
    if (previous === undefined || previous === enabled) {
      return;
    }

    this.children.set(focusKey, enabled);
    this.notify(enabled ? 'enabled' : 'disabled', focusKey);
  }

  setOrderedKeys(orderedKeys: readonly string[]): void {
    const nextKeys = snapshotOrderedKeys(orderedKeys);
    if (areEqualKeys(this.orderedKeys, nextKeys)) {
      return;
    }

    this.orderedKeys = nextKeys;
    this.notify('order');
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  private notify(change: ScopeRegistrationChange, focusKey?: string): void {
    this.version += 1;
    for (const listener of [...this.listeners]) {
      listener();
    }
    this.registryListener?.(change, focusKey);
  }
}

class NoiraFocusRegistryImpl implements NoiraFocusRegistry {
  private policy: FocusNavigationPolicy;
  private readonly consumedRestoreRequests = new Map<
    string,
    { focusKey: string; requestId: number | string | undefined }
  >();
  private readonly focusKeyOwners = new Map<
    string,
    { claimCount: number; owner: object }
  >();
  private readonly scopes = new Map<
    string,
    { controller: NoiraFocusScopeController; registrationCount: number }
  >();
  private focusQueue: Promise<void> = Promise.resolve();
  private focusOperation = 0;
  private explicitRequestVersion = 0;
  private initialRequestPending = false;
  private pendingExplicitOperation: number | null = null;
  private repairOriginScopeKey: string | null = null;
  private repairScheduled = false;

  constructor(policy: FocusNavigationPolicy) {
    this.policy = policy;
  }

  setPolicy(policy: FocusNavigationPolicy): void {
    this.policy = policy;
  }

  claimFocusKey(focusKey: string, owner: object): () => void {
    assertNonBlankKey(focusKey, 'focusKey');
    const existing = this.focusKeyOwners.get(focusKey);
    if (existing && existing.owner !== owner) {
      throw new Error(
        `Norigin focusKey "${focusKey}" already has a mounted Noira owner.`,
      );
    }

    const ownership = existing ?? { claimCount: 0, owner };
    ownership.claimCount += 1;
    this.focusKeyOwners.set(focusKey, ownership);
    let claimed = true;

    return () => {
      if (!claimed) {
        return;
      }

      claimed = false;
      const current = this.focusKeyOwners.get(focusKey);
      if (!current || current.owner !== owner) {
        return;
      }

      current.claimCount -= 1;
      if (current.claimCount === 0) {
        this.focusKeyOwners.delete(focusKey);
      }
    };
  }

  hasValidCurrentTarget(): boolean {
    const currentFocusKey = getCurrentFocusKey();
    return (
      typeof currentFocusKey === 'string' &&
      currentFocusKey.length > 0 &&
      doesFocusableExist(currentFocusKey) &&
      this.isEnabledTarget(currentFocusKey)
    );
  }

  registerScope(controller: NoiraFocusScopeController): () => void {
    const existing = this.scopes.get(controller.scopeKey);
    if (existing && existing.controller !== controller) {
      throw new Error(`FocusScope scopeKey "${controller.scopeKey}" is already mounted.`);
    }

    const registration = existing ?? { controller, registrationCount: 0 };
    registration.registrationCount += 1;
    this.scopes.set(controller.scopeKey, registration);
    if (!existing) {
      controller.attachRegistry((change, focusKey) => {
        this.onScopeRegistrationChanged(controller, change, focusKey);
      });
    }
    let registered = true;

    return () => {
      if (!registered) {
        return;
      }

      registered = false;
      const current = this.scopes.get(controller.scopeKey);
      if (!current || current.controller !== controller) {
        return;
      }

      current.registrationCount -= 1;
      if (current.registrationCount > 0) {
        return;
      }

      this.scopes.delete(controller.scopeKey);
      controller.attachRegistry(null);
      this.scheduleRepair(controller.scopeKey);
    };
  }

  requestInitialFocus(focusKey: string): void {
    if (
      this.initialRequestPending ||
      this.pendingExplicitOperation !== null ||
      this.hasValidCurrentTarget() ||
      !this.isEnabledTarget(focusKey)
    ) {
      return;
    }

    this.initialRequestPending = true;
    const operation = ++this.focusOperation;
    this.enqueueFocus(operation, focusKey, false, () => {
      this.initialRequestPending = false;
    });
  }

  requestRestoreFocus({
    requestId,
    requestedFocusKey,
    scopeKey,
    targetFocusKey,
  }: NoiraFocusRestoreRequest): void {
    if (
      !this.isEnabledTarget(targetFocusKey) ||
      !this.consumeRestoreRequest(scopeKey, requestedFocusKey, requestId)
    ) {
      return;
    }

    this.explicitRequestVersion += 1;
    const operation = ++this.focusOperation;
    this.pendingExplicitOperation = operation;
    this.enqueueFocus(operation, targetFocusKey, true, () => {
      if (this.pendingExplicitOperation === operation) {
        this.pendingExplicitOperation = null;
      }
    });
  }

  private enqueueFocus(
    operation: number,
    focusKey: string,
    force: boolean,
    onFinally?: () => void,
  ): void {
    const run = async () => {
      try {
        if (operation !== this.focusOperation) {
          return;
        }
        if (!this.isEnabledTarget(focusKey)) {
          this.scheduleRepair(this.findScopeKey(focusKey));
          return;
        }
        if (!force && this.hasValidCurrentTarget()) {
          return;
        }

        await setFocus(focusKey);
      } finally {
        onFinally?.();
      }
    };
    const pending = this.focusQueue.then(run, run);
    this.focusQueue = pending.then(
      () => undefined,
      () => undefined,
    );
  }

  private findScopeKey(focusKey: string): string | null {
    for (const [scopeKey, { controller }] of this.scopes) {
      if (controller.getEnabledKeys().includes(focusKey)) {
        return scopeKey;
      }
    }
    return null;
  }

  private isEnabledTarget(focusKey: string): boolean {
    return this.findScopeKey(focusKey) !== null;
  }

  private onScopeRegistrationChanged(
    controller: NoiraFocusScopeController,
    change: ScopeRegistrationChange,
    focusKey?: string,
  ): void {
    if (change !== 'disabled' && change !== 'order' && change !== 'removed') {
      return;
    }

    if (focusKey === getCurrentFocusKey() || !this.hasValidCurrentTarget()) {
      this.scheduleRepair(controller.scopeKey);
    }
  }

  private scheduleRepair(originScopeKey: string | null = null): void {
    if (originScopeKey && this.repairOriginScopeKey === null) {
      this.repairOriginScopeKey = originScopeKey;
    }
    if (this.repairScheduled) {
      return;
    }

    this.repairScheduled = true;
    const explicitVersionAtSchedule = this.explicitRequestVersion;
    queueMicrotask(() => {
      this.repairScheduled = false;
      const repairOriginScopeKey = this.repairOriginScopeKey;
      this.repairOriginScopeKey = null;

      if (
        explicitVersionAtSchedule !== this.explicitRequestVersion ||
        this.pendingExplicitOperation !== null ||
        this.hasValidCurrentTarget()
      ) {
        return;
      }

      const repairTarget = this.resolveRepairTarget(repairOriginScopeKey);
      if (!repairTarget) {
        return;
      }

      const operation = ++this.focusOperation;
      this.enqueueFocus(operation, repairTarget, false);
    });
  }

  private resolveRepairTarget(originScopeKey: string | null): string | null {
    if (originScopeKey) {
      const originScope = this.scopes.get(originScopeKey);
      const sameScopeTarget = originScope
        ? this.resolveScopeTarget(originScope.controller)
        : null;
      if (sameScopeTarget) {
        return sameScopeTarget;
      }
    }

    for (const [scopeKey, { controller }] of this.scopes) {
      if (scopeKey === originScopeKey) {
        continue;
      }

      const target = this.resolveScopeTarget(controller);
      if (target) {
        return target;
      }
    }

    return null;
  }

  private resolveScopeTarget(controller: NoiraFocusScopeController): string | null {
    const enabledKeys = controller.getEnabledKeys();
    return enabledKeys.length > 0
      ? this.policy.resolve(controller.scopeKey, enabledKeys)
      : null;
  }

  private consumeRestoreRequest(
    scopeKey: string,
    focusKey: string,
    requestId: number | string | undefined,
  ): boolean {
    const previous = this.consumedRestoreRequests.get(scopeKey);
    if (
      previous?.focusKey === focusKey &&
      Object.is(previous.requestId, requestId)
    ) {
      this.consumedRestoreRequests.delete(scopeKey);
      this.consumedRestoreRequests.set(scopeKey, previous);
      return false;
    }

    this.consumedRestoreRequests.delete(scopeKey);
    this.consumedRestoreRequests.set(scopeKey, { focusKey, requestId });
    if (this.consumedRestoreRequests.size > MAX_CONSUMED_RESTORE_SCOPES) {
      const oldestScopeKey = this.consumedRestoreRequests.keys().next().value;
      if (typeof oldestScopeKey === 'string') {
        this.consumedRestoreRequests.delete(oldestScopeKey);
      }
    }

    return true;
  }
}

const MAX_CONSUMED_RESTORE_SCOPES = 256;

function ensureSpatialNavigationInitialized(): void {
  if (spatialNavigationInitialized) {
    return;
  }

  init({
    shouldFocusDOMNode: true,
    layoutAdapter: GetBoundingClientRectAdapter,
    throttle: 100,
    throttleKeypresses: true,
    focusOnPresetKey: false,
    debug: false,
    visualDebug: false,
  });
  spatialNavigationInitialized = true;
}

function snapshotOrderedKeys(orderedKeys: readonly string[]): readonly string[] {
  const snapshot = [...orderedKeys];
  if (snapshot.some((focusKey) => typeof focusKey !== 'string' || focusKey.trim() === '')) {
    throw new Error('orderedKeys must contain only non-blank keys.');
  }
  if (new Set(snapshot).size !== snapshot.length) {
    throw new Error('orderedKeys must contain unique keys.');
  }
  return snapshot;
}

function assertNonBlankKey(candidate: string, label: string): void {
  if (candidate.trim() === '') {
    throw new Error(`${label} must be a non-blank string.`);
  }
}

function areEqualKeys(left: readonly string[], right: readonly string[]): boolean {
  return left.length === right.length && left.every((key, index) => key === right[index]);
}
