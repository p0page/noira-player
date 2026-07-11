import type { FocusTarget } from './routes';

export interface FocusRestoreRequest {
  readonly requestId: string;
  readonly target: FocusTarget;
}

const fallbackSessionTimestamp = Date.now().toString(36);
let fallbackEventCounter = 0;

export function createFocusRestoreRequest(
  target: FocusTarget,
): FocusRestoreRequest {
  return {
    requestId: createFocusRestoreRequestId(),
    target: {
      scopeKey: target.scopeKey,
      focusKey: target.focusKey,
    },
  };
}

function createFocusRestoreRequestId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  fallbackEventCounter += 1;
  return `focus:${fallbackSessionTimestamp}:${fallbackEventCounter.toString(36)}`;
}
