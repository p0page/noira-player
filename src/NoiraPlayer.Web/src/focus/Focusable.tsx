import { useFocusable } from '@noriginmedia/norigin-spatial-navigation';
import type {
  ButtonHTMLAttributes,
  ReactNode,
  RefObject,
} from 'react';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import {
  useFocusNavigationPolicy,
  useNoiraFocusRegistry,
  useNoiraFocusScope,
} from './FocusProvider';
import type { NoiraFocusRegistry } from './FocusProvider';

export type NoiraFocusDirection = 'down' | 'left' | 'right' | 'up';

type NativeButtonProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  | 'children'
  | 'className'
  | 'onBlur'
  | 'onClick'
  | 'onFocus'
  | 'onKeyDown'
  | 'onKeyUp'
  | 'onSelect'
  | 'ref'
>;

export interface FocusableProps extends NativeButtonProps {
  children: ReactNode;
  className?: string;
  focusKey: string;
  onSelect: () => void;
}

interface NoiraFocusableOptions {
  boundaryDirections?: readonly NoiraFocusDirection[];
  focusKey: string;
  focusable?: boolean;
  onEnter?: () => void;
  onFocus?: () => void;
  preferredFocusKey?: string;
}

interface NoiraFocusableRegistration<Element extends HTMLElement> {
  focusKey: string;
  ref: RefObject<Element>;
}

export function Focusable(props: FocusableProps) {
  const { disabled, focusKey } = props;
  const policy = useFocusNavigationPolicy();
  const registry = useNoiraFocusRegistry();
  const scope = useNoiraFocusScope();
  const ownerRef = useRef<object | null>(null);
  if (ownerRef.current === null) {
    ownerRef.current = {};
  }
  const owner = ownerRef.current;
  const mountedIdentityRef = useRef({
    controller: scope.controller,
    focusKey,
  });
  const mountedIdentity = mountedIdentityRef.current;
  const [ownershipReady, setOwnershipReady] = useState(false);

  useLayoutEffect(() => {
    const release = registry.claimFocusKey(mountedIdentity.focusKey, owner);
    let unregisterChild: (() => void) | null = null;
    try {
      unregisterChild = mountedIdentity.controller.registerChild(
        mountedIdentity.focusKey,
        !disabled,
      );
    } catch (error) {
      release();
      throw error;
    }

    setOwnershipReady(true);
    return () => {
      unregisterChild?.();
      release();
    };
  }, [mountedIdentity.controller, mountedIdentity.focusKey, owner, registry]);

  useEffect(() => {
    mountedIdentity.controller.setChildEnabled(mountedIdentity.focusKey, !disabled);
  }, [disabled, mountedIdentity.controller, mountedIdentity.focusKey]);

  if (mountedIdentity.focusKey !== focusKey) {
    throw new Error(
      'Focusable focusKey cannot change after mount. Remount with a new React key.',
    );
  }
  if (mountedIdentity.controller !== scope.controller) {
    throw new Error(
      'Focusable parent FocusScope cannot change after mount. Remount with a new React key.',
    );
  }
  if (!scope.orderedKeys.includes(focusKey)) {
    throw new Error(
      `Focusable focusKey "${focusKey}" must be present in FocusScope "${scope.scopeKey}" orderedKeys.`,
    );
  }

  return ownershipReady ? (
    <FocusableButton
      {...props}
      focusKey={mountedIdentity.focusKey}
      onEngineFocus={() =>
        policy.remember(scope.scopeKey, focusKey, scope.orderedKeys)
      }
      owner={owner}
      registry={registry}
    />
  ) : null;
}

function FocusableButton({
  children,
  className,
  disabled,
  focusKey,
  onEngineFocus,
  onSelect,
  owner,
  registry,
  type = 'button',
  ...buttonProps
}: FocusableProps & {
  onEngineFocus: () => void;
  owner: object;
  registry: NoiraFocusRegistry;
}) {
  const registration = useNoiraFocusable<HTMLButtonElement>({
    focusKey,
    focusable: !disabled,
    onEnter: onSelect,
    onFocus: onEngineFocus,
  });

  useEffect(() => {
    if (disabled) {
      return;
    }

    let cancelled = false;
    queueMicrotask(() => {
      const node = registration.ref.current;
      if (
        cancelled ||
        !node ||
        document.activeElement === node ||
        !registry.canHandoffDomFocus(focusKey, owner)
      ) {
        return;
      }

      node.focus();
    });

    return () => {
      cancelled = true;
    };
  }, [disabled, focusKey, owner, registration.ref, registry]);

  return (
    <button
      {...buttonProps}
      ref={registration.ref}
      className={className}
      data-focus-key={registration.focusKey}
      disabled={disabled}
      type={type}
      onClick={() => onSelect()}
      onKeyDown={(event) => {
        if (event.key === 'Enter') {
          event.preventDefault();
        }
      }}
    >
      {children}
    </button>
  );
}

export function useNoiraFocusable<Element extends HTMLElement>({
  boundaryDirections,
  focusKey,
  focusable = true,
  onEnter,
  onFocus,
  preferredFocusKey,
}: NoiraFocusableOptions): NoiraFocusableRegistration<Element> {
  const registration = useFocusable<object, Element>({
    autoRestoreFocus: false,
    focusBoundaryDirections: boundaryDirections
      ? [...boundaryDirections]
      : undefined,
    focusKey,
    focusable,
    isFocusBoundary: boundaryDirections !== undefined,
    onEnterPress: onEnter,
    onFocus,
    preferredChildFocusKey: preferredFocusKey,
    saveLastFocusedChild: false,
  });

  return {
    focusKey: registration.focusKey,
    ref: registration.ref,
  };
}
