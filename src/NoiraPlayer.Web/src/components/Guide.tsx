import { Home as HomeIcon, Library, LogOut } from 'lucide-react';
import { useMemo, useState } from 'react';
import type { FocusEvent, KeyboardEvent } from 'react';
import { Focusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import type { FocusTarget } from '../navigation/routes';
import type { LibraryView } from '../types';

export type GuideActiveRoute =
  | { kind: 'home' }
  | { kind: 'library'; libraryId: string };

export interface GuideProps {
  activeRoute: GuideActiveRoute;
  defaultFocus?: boolean;
  disabled?: boolean;
  libraries: readonly LibraryView[];
  onHome: () => void;
  onLibrary: (library: LibraryView) => void;
  onLogout: () => void;
  onRestoreFocus: (target: FocusTarget) => void;
  returnTarget: FocusTarget | null;
}

const homeFocusKey = 'guide:home';
const logoutFocusKey = 'guide:logout';

export function Guide({
  activeRoute,
  defaultFocus = false,
  disabled,
  libraries,
  onHome,
  onLibrary,
  onLogout,
  onRestoreFocus,
  returnTarget,
}: GuideProps) {
  const [expanded, setExpanded] = useState(false);
  const visibleLibraries = useMemo(() => deduplicateLibraries(libraries), [libraries]);
  const libraryEntries = visibleLibraries.map((library) => ({
    focusKey: `guide:library:${encodeURIComponent(library.id.trim())}`,
    library,
  }));
  const orderedKeys = [
    homeFocusKey,
    ...libraryEntries.map(({ focusKey }) => focusKey),
    logoutFocusKey,
  ];

  function handleBlur(event: FocusEvent<HTMLElement>) {
    const nextTarget = event.relatedTarget;
    if (!(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
      setExpanded(false);
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== 'Escape' || !expanded) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    setExpanded(false);
    if (returnTarget) {
      onRestoreFocus(returnTarget);
    }
  }

  return (
    <aside
      aria-expanded={expanded}
      aria-label="Guide"
      className={`guide${expanded ? ' guide--expanded' : ''}`}
      role="navigation"
      onBlurCapture={handleBlur}
      onFocusCapture={() => setExpanded(true)}
      onKeyDownCapture={handleKeyDown}
    >
      <FocusScope
        className="guide__scope"
        defaultFocusKey={defaultFocus ? homeFocusKey : undefined}
        orderedKeys={orderedKeys}
        scopeKey="home-guide"
      >
        <div className="guide__destinations">
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

          {libraryEntries.map(({ focusKey, library }) => (
            <Focusable
              key={focusKey}
              aria-current={
                activeRoute.kind === 'library' && activeRoute.libraryId === library.id
                  ? 'page'
                  : undefined
              }
              aria-label={library.name}
              className="guide__button"
              disabled={disabled}
              focusKey={focusKey}
              title={library.name}
              onSelect={() => onLibrary(library)}
            >
              <Library aria-hidden="true" size={24} strokeWidth={1.8} />
              <span>{library.name}</span>
            </Focusable>
          ))}
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

function deduplicateLibraries(libraries: readonly LibraryView[]): LibraryView[] {
  const seenIds = new Set<string>();
  const result: LibraryView[] = [];

  for (const library of libraries) {
    const id = library.id.trim();
    if (!id || seenIds.has(id)) {
      continue;
    }

    seenIds.add(id);
    result.push(library);
  }

  return result;
}
