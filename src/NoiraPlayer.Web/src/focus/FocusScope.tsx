import { FocusContext } from '@noriginmedia/norigin-spatial-navigation';
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useSyncExternalStore,
} from 'react';
import type { ReactNode } from 'react';
import {
  NoiraFocusScopeContext,
  createNoiraFocusScopeController,
  useFocusNavigationPolicy,
  useNoiraFocusRegistry,
} from './FocusProvider';
import type { NoiraFocusScopeController } from './FocusProvider';
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
  restoreRequestId?: FocusRestoreRequestId;
  scopeKey: string;
}

interface ConsumedRestoreRequest {
  focusKey: string;
  requestId: FocusRestoreRequestId | undefined;
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
  const controllerRef = useRef<NoiraFocusScopeController | null>(null);
  if (controllerRef.current === null) {
    controllerRef.current = createNoiraFocusScopeController(scopeKey, orderedKeys);
  }
  const controller = controllerRef.current;

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
  const registration = useNoiraFocusable<HTMLDivElement>({
    boundaryDirections,
    focusKey: mountedScopeKeyRef.current,
    focusable: enabledKeys.length > 0,
    preferredFocusKey: preferredEntryKey ?? undefined,
  });
  const scopeState = useMemo(
    () => ({ controller, orderedKeys, scopeKey: mountedScopeKeyRef.current }),
    [controller, orderedKeys],
  );
  const initialRequestConsumedRef = useRef(false);
  const consumedRestoreRequestRef = useRef<ConsumedRestoreRequest | null>(null);

  useLayoutEffect(() => {
    controller.setOrderedKeys(orderedKeys);
  }, [controller, orderedKeys]);

  useEffect(
    () => registry.registerScope(controller),
    [controller, registry],
  );

  useEffect(() => {
    if (enabledKeys.length === 0) {
      return;
    }

    if (restoreFocusKey !== undefined) {
      const consumedRequest = consumedRestoreRequestRef.current;
      const alreadyConsumed =
        consumedRequest?.focusKey === restoreFocusKey &&
        Object.is(consumedRequest.requestId, restoreRequestId);
      if (alreadyConsumed) {
        return;
      }

      const target = enabledKeys.includes(restoreFocusKey)
        ? restoreFocusKey
        : policy.resolve(mountedScopeKeyRef.current, enabledKeys);
      consumedRestoreRequestRef.current = {
        focusKey: restoreFocusKey,
        requestId: restoreRequestId,
      };
      initialRequestConsumedRef.current = true;
      if (target) {
        registry.requestRestoreFocus(target);
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
