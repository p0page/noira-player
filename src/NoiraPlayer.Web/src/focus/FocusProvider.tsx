import {
  GetBoundingClientRectAdapter,
  init,
  pause as pauseSpatialNavigation,
  resume as resumeSpatialNavigation,
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

export interface NoiraFocusScopeState {
  orderedKeys: readonly string[];
  scopeKey: string;
}

const FocusPolicyContext = createContext<FocusNavigationPolicy | null>(null);
const FocusScopeStateContext = createContext<NoiraFocusScopeState | null>(null);

let spatialNavigationInitialized = false;

export function FocusProvider({ children, policy }: FocusProviderProps) {
  const defaultPolicyRef = useRef<FocusNavigationPolicy | null>(null);
  if (defaultPolicyRef.current === null) {
    defaultPolicyRef.current = createFocusNavigationPolicy();
  }

  const focusPolicy = policy ?? defaultPolicyRef.current;

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
      {children}
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

export function useNoiraFocusScope(): NoiraFocusScopeState {
  const scope = useContext(FocusScopeStateContext);
  if (!scope) {
    throw new Error('Focusable must be rendered inside FocusScope.');
  }

  return scope;
}

export const NoiraFocusScopeContext = FocusScopeStateContext;

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
