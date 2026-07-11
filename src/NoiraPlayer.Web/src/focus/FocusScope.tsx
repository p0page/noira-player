import {
  FocusContext,
  setFocus,
} from '@noriginmedia/norigin-spatial-navigation';
import { useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import {
  NoiraFocusScopeContext,
  useFocusNavigationPolicy,
} from './FocusProvider';
import {
  useNoiraFocusable,
  type NoiraFocusDirection,
} from './Focusable';
import type { FocusNavigationPolicy } from './focusPolicy';

export type FocusDirection = NoiraFocusDirection;

export interface FocusScopeProps {
  boundaryDirections?: readonly FocusDirection[];
  children: ReactNode;
  className?: string;
  defaultFocusKey?: string;
  orderedKeys: readonly string[];
  preferredFocusKey?: string;
  restoreFocusKey?: string;
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
  scopeKey,
}: FocusScopeProps) {
  const policy = useFocusNavigationPolicy();
  const requestedFocusKey = resolveRequestedFocusKey(
    policy,
    scopeKey,
    orderedKeys,
    preferredFocusKey,
    defaultFocusKey,
    restoreFocusKey,
  );
  const registration = useNoiraFocusable<HTMLDivElement>({
    boundaryDirections,
    focusKey: scopeKey,
    preferredFocusKey: requestedFocusKey ?? undefined,
  });
  const scopeState = useMemo(
    () => ({ orderedKeys, scopeKey }),
    [orderedKeys, scopeKey],
  );

  useEffect(() => {
    if (requestedFocusKey) {
      void setFocus(registration.focusKey);
    }
  }, [registration.focusKey, requestedFocusKey]);

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

function resolveRequestedFocusKey(
  policy: FocusNavigationPolicy,
  scopeKey: string,
  orderedKeys: readonly string[],
  preferredFocusKey?: string,
  defaultFocusKey?: string,
  restoreFocusKey?: string,
): string | null {
  if (restoreFocusKey !== undefined) {
    if (orderedKeys.includes(restoreFocusKey)) {
      return restoreFocusKey;
    }

    return policy.resolve(scopeKey, orderedKeys);
  }

  if (preferredFocusKey !== undefined) {
    if (orderedKeys.includes(preferredFocusKey)) {
      return preferredFocusKey;
    }

    return policy.resolveInitial(orderedKeys, defaultFocusKey);
  }

  if (defaultFocusKey !== undefined) {
    return policy.resolveInitial(orderedKeys, defaultFocusKey);
  }

  return null;
}
