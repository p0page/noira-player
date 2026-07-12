import { Heart, Home as HomeIcon, LogOut, Search } from 'lucide-react';
import type { FocusEvent, KeyboardEvent } from 'react';
import { Focusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import type { FocusRestoreRequest } from '../navigation/focusRequests';
import type { FocusTarget } from '../navigation/routes';

export type GuideActiveRoute =
  | { kind: 'home' }
  | { kind: 'search' }
  | { kind: 'favorites' }
  | { kind: 'library'; libraryId: string };

export interface GuideProps {
  activeRoute: GuideActiveRoute;
  defaultFocus?: boolean;
  disabled?: boolean;
  expanded: boolean;
  hidden?: boolean;
  onFavorites: () => void;
  onHome: () => void;
  onLogout: () => void;
  onExpandedChange: (expanded: boolean) => void;
  onExitToContent: () => void;
  onKeyInteraction?: () => void;
  onSearch: () => void;
  restoreRequest?: FocusRestoreRequest | null;
}

export const guideScopeKey = 'home-guide';

const homeFocusKey = 'guide:home';
const searchFocusKey = 'guide:search';
const favoritesFocusKey = 'guide:favorites';
const logoutFocusKey = 'guide:logout';

export function Guide({
  activeRoute,
  defaultFocus = false,
  disabled,
  expanded,
  hidden,
  onFavorites,
  onHome,
  onLogout,
  onExpandedChange,
  onExitToContent,
  onKeyInteraction,
  onSearch,
  restoreRequest,
}: GuideProps) {
  const orderedKeys = [searchFocusKey, homeFocusKey, favoritesFocusKey, logoutFocusKey];

  function handleBlur(event: FocusEvent<HTMLElement>) {
    const nextTarget = event.relatedTarget;
    if (!(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
      onExpandedChange(false);
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    onKeyInteraction?.();
    const exitsToContent = event.key === 'Escape' || event.key === 'ArrowRight';
    if (!expanded || !exitsToContent) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    onExitToContent();
  }

  return (
    <aside
      aria-expanded={expanded}
      aria-label="Guide"
      className={`guide${expanded ? ' guide--expanded' : ''}`}
      hidden={hidden}
      role="navigation"
      onBlurCapture={handleBlur}
      onFocusCapture={() => onExpandedChange(true)}
      onKeyDownCapture={handleKeyDown}
    >
      <FocusScope
        boundaryDirections={['right']}
        className="guide__scope"
        defaultFocusKey={defaultFocus ? homeFocusKey : undefined}
        orderedKeys={orderedKeys}
        restoreFocusKey={
          restoreRequest?.target.scopeKey === guideScopeKey
            ? restoreRequest.target.focusKey
            : undefined
        }
        restoreRequestId={
          restoreRequest?.target.scopeKey === guideScopeKey
            ? restoreRequest.requestId
            : undefined
        }
        scopeKey={guideScopeKey}
      >
        <div className="guide__destinations">
          <Focusable
            aria-current={activeRoute.kind === 'search' ? 'page' : undefined}
            aria-label="Search"
            className="guide__button"
            disabled={disabled}
            focusKey={searchFocusKey}
            title="Search"
            onSelect={onSearch}
          >
            <Search aria-hidden="true" size={24} strokeWidth={1.8} />
            <span>Search</span>
          </Focusable>

          <Focusable
            aria-current={activeRoute.kind === 'home' ? 'page' : undefined}
            aria-label="Home"
            className="guide__button"
            disabled={disabled}
            focusKey={homeFocusKey}
            title="Home"
            onSelect={onHome}
          >
            <HomeIcon aria-hidden="true" size={24} strokeWidth={1.8} />
            <span>Home</span>
          </Focusable>

          <Focusable
            aria-current={activeRoute.kind === 'favorites' ? 'page' : undefined}
            aria-label="Favorites"
            className="guide__button"
            disabled={disabled}
            focusKey={favoritesFocusKey}
            title="Favorites"
            onSelect={onFavorites}
          >
            <Heart aria-hidden="true" size={24} strokeWidth={1.8} />
            <span>Favorites</span>
          </Focusable>
        </div>

        <Focusable
          aria-label="Log out"
          className="guide__button guide__button--logout"
          disabled={disabled}
          focusKey={logoutFocusKey}
          title="Log out"
          onSelect={onLogout}
        >
          <LogOut aria-hidden="true" size={24} strokeWidth={1.8} />
          <span>Log out</span>
        </Focusable>
      </FocusScope>
    </aside>
  );
}

export function getGuideFocusTarget(activeRoute: GuideActiveRoute): FocusTarget {
  const focusKey =
    activeRoute.kind === 'search'
      ? searchFocusKey
      : activeRoute.kind === 'favorites'
        ? favoritesFocusKey
        : homeFocusKey;
  return { scopeKey: guideScopeKey, focusKey };
}
