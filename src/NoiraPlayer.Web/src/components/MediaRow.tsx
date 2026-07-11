import { FocusScope } from '../focus/FocusScope';
import type { HomeRow } from '../catalog/homeCatalog';
import type { FocusTarget } from '../navigation/routes';
import type { LibraryView, MediaItem } from '../types';
import {
  isLibraryView,
  MediaCard,
  type MediaCardItem,
  type MediaCardVariant,
} from './MediaCard';

export interface MediaRowProps {
  defaultFocus?: boolean;
  disabled?: boolean;
  onOpenLibrary: (library: LibraryView, origin: FocusTarget) => void;
  onOpenMedia: (item: MediaItem, origin: FocusTarget) => void;
  preferredFocusKey?: string;
  restoreFocusKey?: string;
  restoreRequestId?: number | string;
  row: HomeRow;
}

interface CardEntry {
  focusKey: string;
  item: MediaCardItem;
}

export function MediaRow({
  defaultFocus = false,
  disabled,
  onOpenLibrary,
  onOpenMedia,
  preferredFocusKey,
  restoreFocusKey,
  restoreRequestId,
  row,
}: MediaRowProps) {
  const entries = createCardEntries(row);
  if (entries.length === 0) {
    return null;
  }

  const orderedKeys = entries.map(({ focusKey }) => focusKey);
  const scopeKey = getHomeRowScopeKey(row.key);
  const headingId = `home-row-title:${encodeFocusSegment(row.key)}`;
  const variant = resolveVariant(row);

  return (
    <FocusScope
      className="media-row"
      defaultFocusKey={defaultFocus ? orderedKeys[0] : undefined}
      orderedKeys={orderedKeys}
      preferredFocusKey={preferredFocusKey}
      restoreFocusKey={restoreFocusKey}
      restoreRequestId={restoreRequestId}
      scopeKey={scopeKey}
    >
      <section aria-labelledby={headingId} data-row-key={row.key}>
        <h2 id={headingId}>{row.title}</h2>
        <div className="media-row__track">
          {entries.map(({ focusKey, item }) => (
            <MediaCard
              key={focusKey}
              disabled={disabled}
              focusKey={focusKey}
              item={item}
              onSelect={() => {
                const origin = { focusKey, scopeKey };
                if (isLibraryView(item)) {
                  onOpenLibrary(item, origin);
                } else {
                  onOpenMedia(item, origin);
                }
              }}
              variant={variant}
            />
          ))}
        </div>
      </section>
    </FocusScope>
  );
}

export function getHomeRowScopeKey(rowKey: string): string {
  return `home-row:${encodeFocusSegment(rowKey)}`;
}

function createCardEntries(row: HomeRow): CardEntry[] {
  return row.items.map((item) => {
    const itemKind = isLibraryView(item) ? 'library' : 'media';

    return {
      focusKey: [
        'home-card',
        encodeFocusSegment(row.key),
        itemKind,
        encodeFocusSegment(item.id),
      ].join(':'),
      item,
    };
  });
}

export function normalizeHomeRow(row: HomeRow): HomeRow | null {
  const key = row.key.trim();
  if (!key) {
    return null;
  }

  const seenItems = new Set<string>();
  const items: MediaCardItem[] = [];
  for (const item of row.items) {
    const id = item.id.trim();
    const itemKind = isLibraryView(item) ? 'library' : 'media';
    const identity = `${itemKind}:${id}`;
    if (!id || seenItems.has(identity)) {
      continue;
    }

    seenItems.add(identity);
    items.push({ ...item, id });
  }

  return { ...row, key, items };
}

function resolveVariant(row: HomeRow): MediaCardVariant {
  return row.kind === 'latest' ? 'poster' : 'wide';
}

function encodeFocusSegment(value: string): string {
  return encodeURIComponent(value.trim() || 'anonymous');
}
