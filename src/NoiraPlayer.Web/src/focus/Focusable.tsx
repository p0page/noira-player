import { useFocusable } from '@noriginmedia/norigin-spatial-navigation';
import type {
  ButtonHTMLAttributes,
  ReactNode,
  RefObject,
} from 'react';
import { useEffect, useRef } from 'react';
import {
  useFocusNavigationPolicy,
  useNoiraFocusScope,
} from './FocusProvider';

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

export function Focusable({
  children,
  className,
  disabled,
  focusKey,
  onSelect,
  type = 'button',
  ...buttonProps
}: FocusableProps) {
  const policy = useFocusNavigationPolicy();
  const scope = useNoiraFocusScope();
  const mountedIdentityRef = useRef({
    controller: scope.controller,
    focusKey,
  });
  const mountedIdentity = mountedIdentityRef.current;
  const registration = useNoiraFocusable<HTMLButtonElement>({
    focusKey: mountedIdentity.focusKey,
    focusable: !disabled,
    onEnter: onSelect,
    onFocus: () => policy.remember(scope.scopeKey, focusKey, scope.orderedKeys),
  });

  useEffect(
    () => mountedIdentity.controller.registerChild(mountedIdentity.focusKey, !disabled),
    [mountedIdentity.controller, mountedIdentity.focusKey],
  );

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
