import { describe, expect, it } from 'vitest';
import type { HostInputCommand, HostInputEvent } from '../bridge';
import type { BrowseRoute, FocusTarget } from '../navigation/routes';
import {
  createGlobalInputPolicy,
  type GlobalInputLayerState,
} from './globalInputPolicy';

const targets = {
  guide: { scopeKey: 'home-row:resume', focusKey: 'home-card:resume' },
  modal: { scopeKey: 'modal', focusKey: 'modal:close' },
  overlay: { scopeKey: 'overlay', focusKey: 'overlay:primary' },
  textInput: { scopeKey: 'search-input-scope', focusKey: 'search-input' },
} satisfies Record<string, FocusTarget>;

const homeRoute = { kind: 'home' } as const;
const searchRoute = {
  kind: 'search',
  origin: { scopeKey: 'home-guide', focusKey: 'guide:search' },
} as const;

describe('GlobalInputPolicy', () => {
  it.each([
    ['moveUp', 'pressed'],
    ['moveDown', 'repeated'],
    ['moveLeft', 'released'],
    ['moveRight', 'pressed'],
    ['accept', 'released'],
  ] as const)('routes %s %s through the spatial adapter', (command, phase) => {
    const policy = createGlobalInputPolicy();

    expect(policy.decide(input(command, phase), state([homeRoute]))).toEqual({
      kind: 'spatial',
    });
  });

  it('closes only the highest-priority active layer on Back', () => {
    const policy = createGlobalInputPolicy();
    const layers: GlobalInputLayerState = {
      guide: { returnTarget: targets.guide },
      modal: { returnTarget: targets.modal },
      overlay: { returnTarget: targets.overlay },
      textInput: { returnTarget: targets.textInput },
    };

    expect(policy.decide(input('back'), state([homeRoute], layers))).toEqual({
      kind: 'closeLayer',
      layer: 'modal',
      restoreTarget: targets.modal,
    });
    expect(
      policy.decide(
        input('back'),
        state([homeRoute], { ...layers, modal: undefined }),
      ),
    ).toEqual({
      kind: 'closeLayer',
      layer: 'textInput',
      restoreTarget: targets.textInput,
    });
    expect(
      policy.decide(
        input('back'),
        state([homeRoute], {
          ...layers,
          modal: undefined,
          textInput: undefined,
        }),
      ),
    ).toEqual({
      kind: 'closeLayer',
      layer: 'overlay',
      restoreTarget: targets.overlay,
    });
    expect(
      policy.decide(
        input('back'),
        state([homeRoute], {
          guide: layers.guide,
        }),
      ),
    ).toEqual({
      kind: 'closeLayer',
      layer: 'guide',
      restoreTarget: targets.guide,
    });
  });

  it('navigates a non-root route and restores its exact origin on Back', () => {
    const policy = createGlobalInputPolicy();

    expect(
      policy.decide(input('back'), state([homeRoute, searchRoute])),
    ).toEqual({
      kind: 'navigate',
      restoreTarget: searchRoute.origin,
      route: homeRoute,
    });
  });

  it('delegates Back from Home to the native host', () => {
    const policy = createGlobalInputPolicy();

    expect(policy.decide(input('back'), state([homeRoute]))).toEqual({
      kind: 'nativeBack',
    });
  });

  it.each(['menu', 'view'] as const)(
    'toggles Guide with %s but never passes through a modal',
    (command) => {
      const policy = createGlobalInputPolicy();

      expect(policy.decide(input(command), state([homeRoute]))).toEqual({
        kind: 'openGuide',
      });
      expect(
        policy.decide(
          input(command),
          state([homeRoute], { guide: { returnTarget: targets.guide } }),
        ),
      ).toEqual({
        kind: 'closeLayer',
        layer: 'guide',
        restoreTarget: targets.guide,
      });
      expect(
        policy.decide(
          input(command),
          state([homeRoute], { modal: { returnTarget: targets.modal } }),
        ),
      ).toEqual({ kind: 'consume' });
    },
  );

  it.each(['back', 'menu', 'view'] as const)(
    'consumes non-pressed %s phases without repeating actions',
    (command) => {
      const policy = createGlobalInputPolicy();

      expect(
        policy.decide(input(command, 'released'), state([homeRoute])),
      ).toEqual({ kind: 'consume' });
      expect(
        policy.decide(input(command, 'repeated'), state([homeRoute])),
      ).toEqual({ kind: 'consume' });
    },
  );
});

function input(
  command: HostInputCommand,
  phase: HostInputEvent['phase'] = 'pressed',
): HostInputEvent {
  return {
    type: 'host.input',
    version: 1,
    sequence: 1,
    command,
    phase,
    source: 'gamepad',
    timestamp: 1,
  };
}

function state(
  routeStack: readonly BrowseRoute[],
  layers: GlobalInputLayerState = {},
) {
  return { layers, routeStack };
}
