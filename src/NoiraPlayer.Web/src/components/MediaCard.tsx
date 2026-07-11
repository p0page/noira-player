import { ImageOff } from 'lucide-react';
import type { LibraryView, MediaItem } from '../types';
import { Focusable } from '../focus/Focusable';

export type MediaCardItem = MediaItem | LibraryView;
export type MediaCardVariant = 'poster' | 'wide';

export interface MediaCardProps {
  disabled?: boolean;
  focusKey: string;
  item: MediaCardItem;
  onSelect: () => void;
  variant: MediaCardVariant;
}

export function MediaCard({
  disabled,
  focusKey,
  item,
  onSelect,
  variant,
}: MediaCardProps) {
  const imageUrl = resolveImageUrl(item, variant);
  const metadata = formatMetadata(item);
  const progress = isLibraryView(item) ? null : resolveProgress(item);

  return (
    <Focusable
      aria-label={`Open ${item.name}`}
      className="media-card"
      data-card-variant={variant}
      data-item-id={item.id}
      disabled={disabled}
      focusKey={focusKey}
      onSelect={onSelect}
    >
      <span className="media-card__visual">
        <span className="media-card__artwork" data-media-artwork>
          {imageUrl ? (
            <img src={imageUrl} alt="" loading="lazy" />
          ) : (
            <span className="media-card__fallback" aria-hidden="true">
              <ImageOff size={28} strokeWidth={1.7} />
            </span>
          )}
          {progress !== null ? (
            <span
              className="media-card__progress"
              role="progressbar"
              aria-label={`${item.name} progress`}
              aria-valuemax={100}
              aria-valuemin={0}
              aria-valuenow={progress}
            >
              <span style={{ width: `${progress}%` }} />
            </span>
          ) : null}
        </span>
        <span className="media-card__copy">
          <span className="media-card__title">{item.name}</span>
          <span className="media-card__metadata">{metadata}</span>
        </span>
      </span>
    </Focusable>
  );
}

export function isLibraryView(item: MediaCardItem): item is LibraryView {
  return 'collectionType' in item;
}

function resolveImageUrl(
  item: MediaCardItem,
  variant: MediaCardVariant,
): string | undefined {
  if (isLibraryView(item)) {
    return item.imageUrl;
  }

  if (variant === 'wide') {
    return (
      item.artwork.thumb ??
      item.artwork.backdrop ??
      item.artwork.banner ??
      item.artwork.primary ??
      item.imageUrl
    );
  }

  return (
    item.artwork.primary ??
    item.artwork.thumb ??
    item.artwork.backdrop ??
    item.artwork.banner ??
    item.imageUrl
  );
}

function formatMetadata(item: MediaCardItem): string {
  if (isLibraryView(item)) {
    return 'Library';
  }

  const metadata: string[] = [];
  if (item.seriesName?.trim()) {
    metadata.push(item.seriesName.trim());
  }

  const episode = formatEpisode(item.parentIndexNumber, item.indexNumber);
  if (episode) {
    metadata.push(episode);
  }

  if (item.productionYear !== undefined) {
    metadata.push(String(item.productionYear));
  }

  if (!item.seriesName?.trim() || metadata.length === 0) {
    metadata.push(item.type);
  }

  return metadata.join(' | ');
}

function formatEpisode(
  seasonNumber: number | undefined,
  episodeNumber: number | undefined,
): string | null {
  if (seasonNumber !== undefined && episodeNumber !== undefined) {
    return `S${seasonNumber} E${episodeNumber}`;
  }

  if (episodeNumber !== undefined) {
    return `Episode ${episodeNumber}`;
  }

  return null;
}

function resolveProgress(item: MediaItem): number | null {
  const position = item.startPositionTicks ?? 0;
  const runtime = item.runtimeTicks ?? 0;
  if (!Number.isFinite(position) || !Number.isFinite(runtime) || position <= 0 || runtime <= 0) {
    return null;
  }

  return Math.round(Math.min(100, Math.max(0, (position / runtime) * 100)));
}
