// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from 'vitest';
import type { HostInputEvent } from '../bridge';
import {
  dispatchHostInput,
  HostInputKeyDeduper,
  installHostInputDedupeCapture,
} from './hostInput';

afterEach(() => {
  document.body.replaceChildren();
});

describe('host input adapter', () => {
  it('dispatches a bubbling keyboard event from the active element', () => {
    const button = document.createElement('button');
    document.body.append(button);
    button.focus();
    const buttonListener = vi.fn();
    const windowListener = vi.fn();
    button.addEventListener('keydown', buttonListener);
    window.addEventListener('keydown', windowListener, { once: true });

    dispatchHostInput({
      type: 'host.input',
      version: 1,
      sequence: 1,
      command: 'moveRight',
      phase: 'repeated',
      source: 'gamepad',
      timestamp: 1,
    });

    expect(buttonListener).toHaveBeenCalledOnce();
    expect(windowListener).toHaveBeenCalledOnce();
    const event = buttonListener.mock.calls[0]?.[0] as KeyboardEvent;
    expect(event.key).toBe('ArrowRight');
    expect(event.repeat).toBe(true);
    expect(event.bubbles).toBe(true);
  });

  it('maps accept release to Enter keyup and falls back to window', () => {
    const listener = vi.fn();
    window.addEventListener('keyup', listener, { once: true });

    dispatchHostInput({
      type: 'host.input',
      version: 1,
      sequence: 2,
      command: 'accept',
      phase: 'released',
      source: 'remote',
      timestamp: 2,
    });

    expect(listener).toHaveBeenCalledOnce();
    const event = listener.mock.calls[0]?.[0] as KeyboardEvent;
    expect(event.type).toBe('keyup');
    expect(event.key).toBe('Enter');
  });

  it.each(['back', 'menu', 'view'] as const)(
    'does not turn %s into a DOM keyboard event',
    (command) => {
    const listener = vi.fn();
      window.addEventListener('keydown', listener, { once: true });

    dispatchHostInput({
      type: 'host.input',
        version: 1,
        sequence: 3,
        command,
        phase: 'pressed',
        source: 'gamepad',
        timestamp: 3,
    });

      expect(listener).not.toHaveBeenCalled();
    },
  );

  it('lets the synthetic adapter event reach DOM consumers', () => {
    let now = 100;
    const deduper = new HostInputKeyDeduper(() => now);
    const removeCapture = installHostInputDedupeCapture(deduper);
    const listener = vi.fn();
    window.addEventListener('keydown', listener, { once: true });
    const hostInput = createInput('moveRight', 'pressed', 'gamepad');
    deduper.rememberHostInput(hostInput);

    dispatchHostInput(hostInput);

    expect(listener).toHaveBeenCalledOnce();
    expect((listener.mock.calls[0]?.[0] as KeyboardEvent).isTrusted).toBe(false);
    now += 1;
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowRight',
        type: 'keydown',
      }),
    ).toBe(true);
    removeCapture();
  });

  it('suppresses one matching trusted platform mapping after gamepad input', () => {
    let now = 100;
    const deduper = new HostInputKeyDeduper(() => now);
    deduper.rememberHostInput(createInput('accept', 'pressed', 'remote'));

    now += 20;
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'Enter',
        type: 'keydown',
      }),
    ).toBe(true);
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'Enter',
        type: 'keydown',
      }),
    ).toBe(false);
  });

  it('suppresses host input when the trusted platform mapping arrives first', () => {
    let now = 100;
    const deduper = new HostInputKeyDeduper(() => now);

    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowLeft',
        type: 'keydown',
      }),
    ).toBe(false);
    now += 20;
    expect(
      deduper.shouldSuppressHostInput(
        createInput('moveLeft', 'pressed', 'gamepad'),
      ),
    ).toBe(true);
    expect(
      deduper.shouldSuppressHostInput(
        createInput('moveLeft', 'pressed', 'gamepad'),
      ),
    ).toBe(false);
  });

  it('deduplicates native Escape against semantic gamepad Back', () => {
    const deduper = new HostInputKeyDeduper(() => 100);

    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'Escape',
        type: 'keydown',
      }),
    ).toBe(false);
    expect(
      deduper.shouldSuppressHostInput(createInput('back', 'pressed', 'gamepad')),
    ).toBe(true);
  });

  it('keeps ordinary keyboard, unrelated, and expired events', () => {
    let now = 100;
    const deduper = new HostInputKeyDeduper(() => now, 50);
    deduper.rememberHostInput(createInput('moveLeft', 'pressed', 'keyboard'));
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowLeft',
        type: 'keydown',
      }),
    ).toBe(false);

    deduper.rememberHostInput(createInput('moveLeft', 'pressed', 'gamepad'));
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowRight',
        type: 'keydown',
      }),
    ).toBe(false);
    now += 51;
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowLeft',
        type: 'keydown',
      }),
    ).toBe(false);
  });

  it('keys release dedupe separately from keydown', () => {
    const deduper = new HostInputKeyDeduper(() => 100);
    deduper.rememberHostInput(createInput('moveUp', 'released', 'gamepad'));

    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowUp',
        type: 'keydown',
      }),
    ).toBe(false);
    expect(
      deduper.shouldSuppressNativeEvent({
        isTrusted: true,
        key: 'ArrowUp',
        type: 'keyup',
      }),
    ).toBe(true);
  });
});

function createInput(
  command: HostInputEvent['command'],
  phase: HostInputEvent['phase'],
  source: HostInputEvent['source'],
): HostInputEvent {
  return {
    type: 'host.input',
    version: 1,
    sequence: 10,
    command,
    phase,
    source,
    timestamp: 100,
  };
}
