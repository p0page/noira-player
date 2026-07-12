import { Play } from 'lucide-react';
import type { CSSProperties } from 'react';
import { Focusable } from '../focus/Focusable';
import { FocusScope } from '../focus/FocusScope';
import type { FocusRestoreRequest } from '../navigation/focusRequests';
import type { MediaArtwork, MediaItem } from '../types';

export interface DetailsPageProps {
  item: MediaItem;
  busy?: boolean;
  restoreRequest?: FocusRestoreRequest | null;
  onPlay: (item: MediaItem) => void;
}

const detailsActionsScopeKey = 'details-actions';
const detailsPlayFocusKey = 'details-action:play';

export function getDetailsActionsScopeKey(): string {
  return detailsActionsScopeKey;
}

export function getDetailsPlayFocusKey(): string {
  return detailsPlayFocusKey;
}

export function resolveDetailsAtmosphereUrl(
  artwork: MediaArtwork,
): string | undefined {
  return [artwork.backdrop, artwork.thumb, artwork.banner, artwork.primary].find(
    (candidate) => candidate !== undefined && candidate.trim().length > 0,
  );
}

export function DetailsPage({
  item,
  busy = false,
  restoreRequest = null,
  onPlay,
}: DetailsPageProps) {
  const atmosphereUrl = resolveDetailsAtmosphereUrl(item.artwork);
  const actionLabel = (item.startPositionTicks ?? 0) > 0 ? 'Resume' : 'Play';
  const metadata = createDetailsMetadata(item);
  const restoreMatchesActions =
    restoreRequest?.target.scopeKey === detailsActionsScopeKey;

  const atmosphereStyle: CSSProperties | undefined = atmosphereUrl
    ? { backgroundImage: `url(${JSON.stringify(atmosphereUrl)})` }
    : undefined;

  return (
    <main aria-busy={busy || undefined} className="details-page">
      <div
        aria-hidden="true"
        className="details-page__atmosphere"
        data-has-artwork={atmosphereUrl ? 'true' : 'false'}
        data-testid="details-atmosphere"
        style={atmosphereStyle}
      />

      <section className="details-page__content" aria-labelledby="details-title">
        <header className="details-page__header">
          <h1 id="details-title">{item.name}</h1>
          {metadata.length > 0 ? (
            <div className="details-page__metadata" aria-label="Media details">
              {metadata.map((value) => (
                <span key={value}>{value}</span>
              ))}
            </div>
          ) : null}
        </header>

        {item.overview?.trim() ? (
          <p className="details-page__overview">{item.overview.trim()}</p>
        ) : null}

        <FocusScope
          boundaryDirections={['left', 'right', 'up', 'down']}
          className="details-page__actions"
          defaultFocusKey={detailsPlayFocusKey}
          orderedKeys={[detailsPlayFocusKey]}
          restoreFocusKey={
            restoreMatchesActions ? restoreRequest.target.focusKey : undefined
          }
          restoreRequestId={
            restoreMatchesActions ? restoreRequest.requestId : undefined
          }
          scopeKey={detailsActionsScopeKey}
        >
          <Focusable
            aria-label={actionLabel}
            className="details-page__play"
            disabled={busy}
            focusKey={detailsPlayFocusKey}
            onSelect={() => onPlay(item)}
          >
            <Play aria-hidden="true" size={20} fill="currentColor" />
            <span>{actionLabel}</span>
          </Focusable>
        </FocusScope>
      </section>
    </main>
  );
}

function createDetailsMetadata(item: MediaItem): string[] {
  const metadata: string[] = [];
  if (item.productionYear !== undefined) {
    metadata.push(String(item.productionYear));
  }
  if (item.type.trim()) {
    metadata.push(item.type.trim());
  }
  const runtime = formatRuntime(item.runtimeTicks);
  if (runtime) {
    metadata.push(runtime);
  }
  return metadata;
}

function formatRuntime(runtimeTicks: number | undefined): string | null {
  if (runtimeTicks === undefined || runtimeTicks <= 0) {
    return null;
  }

  const totalMinutes = Math.round(runtimeTicks / 600_000_000);
  if (totalMinutes <= 0) {
    return null;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours === 0) {
    return `${minutes}m`;
  }
  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}
