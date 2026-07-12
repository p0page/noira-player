import { FocusContext } from '@noriginmedia/norigin-spatial-navigation';
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from 'react';
import type { ReactNode } from 'react';
import {
  NoiraFocusScopeContext,
  createNoiraFocusScopeController,
  useFocusNavigationPolicy,
  useNoiraFocusRegistry,
} from './FocusProvider';
import type {
  NoiraFocusScopeController,
  NoiraFocusScopeState,
} from './FocusProvider';
import {
  useNoiraFocusable,
  type NoiraFocusDirection,
} from './Focusable';
import type { FocusNavigationPolicy } from './focusPolicy';

export type FocusDirection = NoiraFocusDirection;
export type FocusRestoreRequestId = number | string;

export interface FocusScopeProps {
  boundaryDirections?: readonly FocusDirection[];
  children: ReactNode;
  className?: string;
  defaultFocusKey?: string;
  orderedKeys: readonly string[];
  preferredFocusKey?: string;
  restoreFocusKey?: string;
  /** Unique event identity for the Provider lifetime; include a session nonce, never a list index. */
  restoreRequestId?: FocusRestoreRequestId;
  scopeKey: string;
}

export function FocusScope({
  boundaryDirections,
  children,
  className,
  defaultFocusKey,
  orderedKeys,
  preferredFocusKey,
  restoreFocusKey,
  restoreRequestId,
  scopeKey,
}: FocusScopeProps) {
  const policy = useFocusNavigationPolicy();
  const registry = useNoiraFocusRegistry();
  const mountedScopeKeyRef = useRef(scopeKey);
  const ownerRef = useRef<object | null>(null);
  if (ownerRef.current === null) {
    ownerRef.current = {};
  }
  const owner = ownerRef.current;
  const controllerRef = useRef<NoiraFocusScopeController | null>(null);
  if (controllerRef.current === null) {
    controllerRef.current = createNoiraFocusScopeController(scopeKey, orderedKeys);
  }
  const controller = controllerRef.current;
  const [ownershipReady, setOwnershipReady] = useState(false);

  useSyncExternalStore(
    controller.subscribe,
    controller.getSnapshot,
    controller.getSnapshot,
  );
  const enabledKeys = controller.getEnabledKeys(orderedKeys);
  const preferredEntryKey = resolvePreferredEntryKey(
    policy,
    mountedScopeKeyRef.current,
    enabledKeys,
    restoreFocusKey,
    preferredFocusKey,
    defaultFocusKey,
  );
  const scopeState = useMemo(
    () => ({ controller, orderedKeys, scopeKey: mountedScopeKeyRef.current }),
    [controller, orderedKeys],
  );
  const initialRequestConsumedRef = useRef(false);

  useLayoutEffect(() => {
    controller.setOrderedKeys(orderedKeys);
  }, [controller, orderedKeys]);

  useLayoutEffect(() => {
    const release = registry.claimFocusKey(mountedScopeKeyRef.current, owner);
    let unregisterScope: (() => void) | null = null;
    try {
      unregisterScope = registry.registerScope(controller);
    } catch (error) {
      release();
      throw error;
    }

    setOwnershipReady(true);
    return () => {
      unregisterScope?.();
      release();
    };
  }, [controller, owner, registry]);

  useEffect(() => {
    if (enabledKeys.length === 0) {
      return;
    }

    if (restoreFocusKey !== undefined) {
      const target = enabledKeys.includes(restoreFocusKey)
        ? restoreFocusKey
        : policy.resolve(mountedScopeKeyRef.current, enabledKeys);
      initialRequestConsumedRef.current = true;
      if (target) {
        registry.requestRestoreFocus({
          requestId: restoreRequestId,
          requestedFocusKey: restoreFocusKey,
          scopeKey: mountedScopeKeyRef.current,
          targetFocusKey: target,
        });
      }
      return;
    }

    if (
      initialRequestConsumedRef.current ||
      (preferredFocusKey === undefined && defaultFocusKey === undefined)
    ) {
      return;
    }

    initialRequestConsumedRef.current = true;
    const target = enabledKeys.includes(preferredFocusKey ?? '')
      ? preferredFocusKey ?? null
      : policy.resolveInitial(enabledKeys, defaultFocusKey);
    if (target) {
      registry.requestInitialFocus(target);
    }
  }, [
    defaultFocusKey,
    enabledKeys,
    policy,
    preferredFocusKey,
    registry,
    restoreFocusKey,
    restoreRequestId,
  ]);

  if (mountedScopeKeyRef.current !== scopeKey) {
    throw new Error(
      'FocusScope scopeKey cannot change after mount. Remount with a new React key.',
    );
  }

  return ownershipReady ? (
    <FocusScopeRegistration
      boundaryDirections={boundaryDirections}
      className={className}
      enabledKeys={enabledKeys}
      preferredEntryKey={preferredEntryKey}
      scopeKey={mountedScopeKeyRef.current}
      scopeState={scopeState}
    >
      {children}
    </FocusScopeRegistration>
  ) : null;
}

function FocusScopeRegistration({
  boundaryDirections,
  children,
  className,
  enabledKeys,
  preferredEntryKey,
  scopeKey,
  scopeState,
}: {
  boundaryDirections?: readonly FocusDirection[];
  children: ReactNode;
  className?: string;
  enabledKeys: readonly string[];
  preferredEntryKey: string | null;
  scopeKey: string;
  scopeState: NoiraFocusScopeState;
}) {
  const registration = useNoiraFocusable<HTMLDivElement>({
    boundaryDirections,
    focusKey: scopeKey,
    focusable: enabledKeys.length > 0,
    preferredFocusKey: preferredEntryKey ?? undefined,
  });

  return (
    <NoiraFocusScopeContext.Provider value={scopeState}>
      <FocusContext.Provider value={registration.focusKey}>
        <div
          ref={registration.ref}
          className={className}
          data-focus-scope={registration.focusKey}
        >
          {children}
        </div>
      </FocusContext.Provider>
    </NoiraFocusScopeContext.Provider>
  );
}

function resolvePreferredEntryKey(
  policy: FocusNavigationPolicy,
  scopeKey: string,
  enabledKeys: readonly string[],
  restoreFocusKey?: string,
  preferredFocusKey?: string,
  defaultFocusKey?: string,
): string | null {
  if (restoreFocusKey && enabledKeys.includes(restoreFocusKey)) {
    return restoreFocusKey;
  }
  if (preferredFocusKey && enabledKeys.includes(preferredFocusKey)) {
    return preferredFocusKey;
  }
  if (defaultFocusKey && enabledKeys.includes(defaultFocusKey)) {
    return defaultFocusKey;
  }
  return enabledKeys.length > 0 ? policy.resolve(scopeKey, enabledKeys) : null;
}
