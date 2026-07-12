import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';
import type { FocusEvent, KeyboardEvent, ReactNode } from 'react';
import type { HostInputEvent } from '../bridge';
import { useGlobalInputHandler } from '../focus/FocusProvider';
import { createGlobalInputPolicy } from '../focus/globalInputPolicy';
import {
  createFocusRestoreRequest,
  type FocusRestoreRequest,
} from '../navigation/focusRequests';
import type { BrowseRoute, FocusTarget } from '../navigation/routes';
import {
  getGuideFocusTarget,
  Guide,
  type GuideActiveRoute,
} from './Guide';

export interface BrowseShellLayer {
  onClose: () => void;
  returnTarget: FocusTarget;
}

interface BrowseShellState {
  contentRestoreRequest: FocusRestoreRequest | null;
  engageTextInput: (
    inputTarget: FocusTarget,
    onExit: () => void,
  ) => void;
  guideInteractionVersion: number;
  releaseTextInput: (inputTarget: FocusTarget) => void;
}

interface TextInputEngagement {
  inputTarget: FocusTarget;
  onExit: () => void;
  returnTarget: FocusTarget;
}

const BrowseShellContext = createContext<BrowseShellState | null>(null);

export interface BrowseShellProps {
  activeRoute: GuideActiveRoute;
  children: ReactNode;
  defaultGuideFocus?: boolean;
  guideHidden?: boolean;
  guideOverlayOnly?: boolean;
  modalLayer?: BrowseShellLayer;
  onFavorites: () => void;
  onHome: () => void;
  onLogout: () => void;
  onNavigateBack: (restoreTarget: FocusTarget) => void;
  onNativeBack: () => void;
  onSearch: () => void;
  overlayLayer?: BrowseShellLayer;
  restoreRequest?: FocusRestoreRequest | null;
  routeStack: readonly BrowseRoute[];
}

export function BrowseShell({
  activeRoute,
  children,
  defaultGuideFocus,
  guideHidden,
  guideOverlayOnly,
  modalLayer,
  onFavorites,
  onHome,
  onLogout,
  onNavigateBack,
  onNativeBack,
  onSearch,
  overlayLayer,
  restoreRequest,
  routeStack,
}: BrowseShellProps) {
  const policyRef = useRef(createGlobalInputPolicy());
  const contentTargetRef = useRef<FocusTarget | null>(null);
  const guideReturnTargetRef = useRef<FocusTarget | null>(null);
  const textInputOriginRef = useRef<FocusTarget | null>(null);
  const [guideExpanded, setGuideExpanded] = useState(false);
  const [guideInteractionVersion, setGuideInteractionVersion] = useState(0);
  const [contentRestoreRequest, setContentRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const [guideRestoreRequest, setGuideRestoreRequest] =
    useState<FocusRestoreRequest | null>(null);
  const [textInputEngagement, setTextInputEngagement] =
    useState<TextInputEngagement | null>(null);
  const [deferCollapsedGuide, setDeferCollapsedGuide] = useState(
    guideOverlayOnly === true,
  );
  const guideUnavailable =
    guideHidden === true ||
    ((guideOverlayOnly === true || deferCollapsedGuide) && !guideExpanded);

  useEffect(() => {
    if (guideOverlayOnly) {
      setDeferCollapsedGuide(true);
      return;
    }

    const timeout = window.setTimeout(() => setDeferCollapsedGuide(false), 0);
    return () => window.clearTimeout(timeout);
  }, [guideOverlayOnly]);

  const restoreFocus = useCallback((target: FocusTarget) => {
    const request = createFocusRestoreRequest(target);
    if (target.scopeKey === 'home-guide') {
      setGuideRestoreRequest(request);
    } else {
      setContentRestoreRequest(request);
    }
  }, []);

  const engageTextInput = useCallback(
    (inputTarget: FocusTarget, onExit: () => void) => {
      const currentRoute = routeStack[routeStack.length - 1];
      const routeOrigin =
        currentRoute && currentRoute.kind !== 'home'
          ? currentRoute.origin
          : null;
      const returnTarget =
        textInputOriginRef.current ??
        routeOrigin ??
        contentTargetRef.current ??
        inputTarget;
      setTextInputEngagement({ inputTarget, onExit, returnTarget });
    },
    [routeStack],
  );

  const releaseTextInput = useCallback((inputTarget: FocusTarget) => {
    setTextInputEngagement((current) =>
      current && targetsEqual(current.inputTarget, inputTarget) ? null : current,
    );
  }, []);

  const openGuide = useCallback(() => {
    if (textInputEngagement) {
      textInputEngagement.onExit();
      setTextInputEngagement(null);
      guideReturnTargetRef.current = textInputEngagement.inputTarget;
    } else {
      guideReturnTargetRef.current =
        readFocusTarget(document.activeElement) ?? contentTargetRef.current;
    }
    setGuideExpanded(true);
    setGuideRestoreRequest(createFocusRestoreRequest(getGuideFocusTarget(activeRoute)));
  }, [activeRoute, textInputEngagement]);

  const closeGuide = useCallback(() => {
    const returnTarget = guideReturnTargetRef.current ?? contentTargetRef.current;
    setGuideExpanded(false);
    setGuideRestoreRequest(null);
    if (returnTarget) {
      restoreFocus(returnTarget);
    }
  }, [restoreFocus]);

  const closeActiveLayer = useCallback(
    (layer: 'guide' | 'modal' | 'overlay' | 'textInput') => {
      switch (layer) {
        case 'guide':
          setGuideExpanded(false);
          setGuideRestoreRequest(null);
          break;
        case 'modal':
          modalLayer?.onClose();
          break;
        case 'overlay':
          overlayLayer?.onClose();
          break;
        case 'textInput':
          textInputEngagement?.onExit();
          setTextInputEngagement(null);
          if (isTextInput(document.activeElement)) {
            document.activeElement.blur();
          }
          break;
      }
    },
    [modalLayer, overlayLayer, textInputEngagement],
  );

  const markGuideInteraction = useCallback(() => {
    setGuideInteractionVersion((current) => current + 1);
  }, []);

  const handleInput = useCallback(
    (input: HostInputEvent): boolean => {
      if (
        input.phase === 'pressed' &&
        (input.command === 'menu' || input.command === 'view')
      ) {
        markGuideInteraction();
      }
      const activeElement = document.activeElement;
      const guideHasFocus =
        activeElement instanceof HTMLElement &&
        activeElement.closest('.guide') !== null;
      const decision = policyRef.current.decide(input, {
        layers: {
          guide: (guideExpanded || guideHasFocus) && guideReturnTargetRef.current
            ? { returnTarget: guideReturnTargetRef.current }
            : undefined,
          modal: modalLayer,
          overlay: overlayLayer,
          textInput: textInputEngagement
            ? { returnTarget: textInputEngagement.returnTarget }
            : undefined,
        },
        routeStack,
      });

      switch (decision.kind) {
        case 'spatial':
          return false;
        case 'consume':
          return true;
        case 'openGuide':
          if (!guideHidden) {
            openGuide();
          }
          return true;
        case 'navigate':
          onNavigateBack(decision.restoreTarget);
          return true;
        case 'nativeBack':
          onNativeBack();
          return true;
        case 'closeLayer':
          closeActiveLayer(decision.layer);
          restoreFocus(decision.restoreTarget);
          return true;
      }
    },
    [
      closeActiveLayer,
      guideExpanded,
      guideHidden,
      markGuideInteraction,
      modalLayer,
      onNavigateBack,
      onNativeBack,
      openGuide,
      overlayLayer,
      restoreFocus,
      routeStack,
      textInputEngagement,
    ],
  );
  useGlobalInputHandler(handleInput);

  function handleFocus(event: FocusEvent<HTMLElement>) {
    if (!(event.target instanceof HTMLElement) || event.target.closest('.guide')) {
      return;
    }

    const target = readFocusTarget(event.target);
    if (isTextInput(event.target)) {
      const relatedTarget =
        event.relatedTarget instanceof HTMLElement && event.relatedTarget.isConnected
          ? readFocusTarget(event.relatedTarget)
          : null;
      textInputOriginRef.current = relatedTarget;
      return;
    }

    if (target) {
      contentTargetRef.current = target;
      textInputOriginRef.current = null;
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (
      event.defaultPrevented ||
      event.key !== 'Escape' ||
      (event.target instanceof HTMLElement && event.target.closest('.guide'))
    ) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    handleInput({
      type: 'host.input',
      version: 1,
      sequence: 0,
      command: 'back',
      phase: 'pressed',
      source: 'keyboard',
      timestamp: 0,
    });
  }

  return (
    <div
      className="tv-shell"
      onFocusCapture={handleFocus}
      onKeyDownCapture={handleKeyDown}
    >
      <Guide
        activeRoute={activeRoute}
        defaultFocus={defaultGuideFocus}
        disabled={guideUnavailable}
        expanded={!guideHidden && guideExpanded}
        hidden={guideUnavailable}
        onExpandedChange={(expanded) => {
          if (expanded && !guideExpanded) {
            guideReturnTargetRef.current = contentTargetRef.current;
          }
          setGuideExpanded(expanded);
        }}
        onExitToContent={closeGuide}
        onFavorites={() => {
          markGuideInteraction();
          onFavorites();
        }}
        onHome={() => {
          markGuideInteraction();
          onHome();
        }}
        onKeyInteraction={markGuideInteraction}
        onLogout={() => {
          markGuideInteraction();
          onLogout();
        }}
        onSearch={() => {
          markGuideInteraction();
          onSearch();
        }}
        restoreRequest={
          guideRestoreRequest ??
          (restoreRequest?.target.scopeKey === 'home-guide'
            ? restoreRequest
            : null)
        }
      />
      <BrowseShellContext.Provider
        value={{
          contentRestoreRequest,
          engageTextInput,
          guideInteractionVersion,
          releaseTextInput,
        }}
      >
        {children}
      </BrowseShellContext.Provider>
    </div>
  );
}

export function useBrowseShellGuideInteractionVersion(): number {
  const state = useContext(BrowseShellContext);
  if (!state) {
    throw new Error('Browse content must be rendered inside BrowseShell.');
  }
  return state.guideInteractionVersion;
}

export function useBrowseShellContentRestoreRequest(): FocusRestoreRequest | null {
  const state = useContext(BrowseShellContext);
  if (!state) {
    throw new Error('Browse content must be rendered inside BrowseShell.');
  }
  return state.contentRestoreRequest;
}

export function useBrowseShellTextInputEngagement(): Pick<
  BrowseShellState,
  'engageTextInput' | 'releaseTextInput'
> {
  const state = useContext(BrowseShellContext);
  if (!state) {
    throw new Error('Browse content must be rendered inside BrowseShell.');
  }
  return {
    engageTextInput: state.engageTextInput,
    releaseTextInput: state.releaseTextInput,
  };
}

function isTextInput(target: Element | null): target is HTMLElement {
  return (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    (target instanceof HTMLElement && target.isContentEditable)
  );
}

function readFocusTarget(candidate: Element | null): FocusTarget | null {
  if (!(candidate instanceof HTMLElement)) {
    return null;
  }

  const focusKey = candidate.dataset.focusKey;
  const scopeKey = candidate
    .closest<HTMLElement>('[data-focus-scope]')
    ?.dataset.focusScope;
  return focusKey && scopeKey ? { focusKey, scopeKey } : null;
}

function targetsEqual(left: FocusTarget, right: FocusTarget): boolean {
  return left.scopeKey === right.scopeKey && left.focusKey === right.focusKey;
}
