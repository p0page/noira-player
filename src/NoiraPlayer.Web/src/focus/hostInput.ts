import type { HostInputEvent } from '../bridge';

const domKeyByCommand = {
  accept: 'Enter',
  moveDown: 'ArrowDown',
  moveLeft: 'ArrowLeft',
  moveRight: 'ArrowRight',
  moveUp: 'ArrowUp',
} as const;

const dedupeKeysByCommand = {
  accept: ['Enter'],
  back: ['Escape', 'BrowserBack', 'GoBack'],
  moveDown: ['ArrowDown'],
  moveLeft: ['ArrowLeft'],
  moveRight: ['ArrowRight'],
  moveUp: ['ArrowUp'],
} as const;

const dedupeKeys = new Set<string>(Object.values(dedupeKeysByCommand).flat());

const defaultDedupeWindowMilliseconds = 80;

interface NativeKeyboardEventLike {
  readonly isTrusted: boolean;
  readonly key: string;
  readonly type: string;
}

export class HostInputKeyDeduper {
  private readonly expiresAtByKey = new Map<string, number>();
  private readonly nativeExpiresAtByKey = new Map<string, number>();

  constructor(
    private readonly now: () => number = monotonicNow,
    private readonly windowMilliseconds = defaultDedupeWindowMilliseconds,
  ) {}

  rememberHostInput(input: HostInputEvent): void {
    if (input.source !== 'gamepad' && input.source !== 'remote') {
      return;
    }

    const signatures = getHostInputKeySignatures(input);
    if (signatures.length === 0) {
      return;
    }

    const expiresAt = this.now() + this.windowMilliseconds;
    for (const signature of signatures) {
      this.expiresAtByKey.set(signature, expiresAt);
    }
  }

  shouldSuppressHostInput(input: HostInputEvent): boolean {
    if (input.source !== 'gamepad' && input.source !== 'remote') {
      return false;
    }

    const signatures = getHostInputKeySignatures(input);
    const now = this.now();
    const matched = signatures.some((signature) => {
      const expiresAt = this.nativeExpiresAtByKey.get(signature);
      return expiresAt !== undefined && now <= expiresAt;
    });
    for (const signature of signatures) {
      this.nativeExpiresAtByKey.delete(signature);
    }
    return matched;
  }

  shouldSuppressNativeEvent(event: NativeKeyboardEventLike): boolean {
    if (!event.isTrusted) {
      return false;
    }

    const signature = createKeySignature(event.type, event.key);
    const expiresAt = this.expiresAtByKey.get(signature);
    if (expiresAt !== undefined) {
      this.expiresAtByKey.delete(signature);
      if (this.now() <= expiresAt) {
        return true;
      }
    }

    if (dedupeKeys.has(event.key)) {
      this.nativeExpiresAtByKey.set(
        signature,
        this.now() + this.windowMilliseconds,
      );
    }
    return false;
  }

  clear(): void {
    this.expiresAtByKey.clear();
    this.nativeExpiresAtByKey.clear();
  }
}

export function installHostInputDedupeCapture(
  deduper: HostInputKeyDeduper,
): () => void {
  const handleKeyboardEvent = (event: KeyboardEvent) => {
    if (!deduper.shouldSuppressNativeEvent(event)) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
  };
  window.addEventListener('keydown', handleKeyboardEvent, true);
  window.addEventListener('keyup', handleKeyboardEvent, true);
  return () => {
    window.removeEventListener('keydown', handleKeyboardEvent, true);
    window.removeEventListener('keyup', handleKeyboardEvent, true);
  };
}

export function dispatchHostInput(input: HostInputEvent): boolean {
  const key = domKeyByCommand[input.command as keyof typeof domKeyByCommand];
  if (!key) {
    return false;
  }

  // Norigin consumes DOM key events; keep gamepad-to-engine translation at this boundary.
  const event = new KeyboardEvent(input.phase === 'released' ? 'keyup' : 'keydown', {
    bubbles: true,
    cancelable: true,
    key,
    repeat: input.phase === 'repeated',
  });
  const target =
    document.activeElement instanceof HTMLElement ? document.activeElement : window;
  target.dispatchEvent(event);
  return true;
}

function getHostInputKeySignatures(input: HostInputEvent): readonly string[] {
  const keys = dedupeKeysByCommand[
    input.command as keyof typeof dedupeKeysByCommand
  ];
  if (!keys) {
    return [];
  }

  const eventType = input.phase === 'released' ? 'keyup' : 'keydown';
  return keys.map((key) => createKeySignature(eventType, key));
}

function createKeySignature(eventType: string, key: string): string {
  return `${eventType}:${key}`;
}

function monotonicNow(): number {
  return performance.now();
}
