import { FocusScope } from '../focus/FocusScope';
import type { HomeRow } from '../catalog/homeCatalog';
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
  onOpenLibrary: (library: LibraryView) => void;
  onOpenMedia: (item: MediaItem) => void;
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
                if (isLibraryView(item)) {
                  onOpenLibrary(item);
                } else {
                  onOpenMedia(item);
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
  const occurrenceByItem = new Map<string, number>();

  return row.items.map((item) => {
    const itemKind = isLibraryView(item) ? 'library' : 'media';
    const itemIdentity = `${itemKind}:${item.id}`;
    const occurrence = occurrenceByItem.get(itemIdentity) ?? 0;
    occurrenceByItem.set(itemIdentity, occurrence + 1);

    return {
      focusKey: [
        'home-card',
        encodeFocusSegment(row.key),
        itemKind,
        encodeFocusSegment(item.id),
        String(occurrence),
      ].join(':'),
      item,
    };
  });
}

function resolveVariant(row: HomeRow): MediaCardVariant {
  return row.kind === 'latest' ? 'poster' : 'wide';
}

function encodeFocusSegment(value: string): string {
  return encodeURIComponent(value.trim() || 'anonymous');
}
