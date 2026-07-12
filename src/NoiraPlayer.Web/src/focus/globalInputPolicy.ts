import type { HostInputEvent } from '../bridge';
import type { BrowseRoute, FocusTarget } from '../navigation/routes';
import { createFocusNavigationPolicy } from './focusPolicy';

export type GlobalInputLayerKind = 'guide' | 'modal' | 'overlay' | 'textInput';

export interface GlobalInputLayer {
  returnTarget: FocusTarget;
}

export interface GlobalInputLayerState {
  guide?: GlobalInputLayer;
  modal?: GlobalInputLayer;
  overlay?: GlobalInputLayer;
  textInput?: GlobalInputLayer;
}

export interface GlobalInputState {
  layers: GlobalInputLayerState;
  routeStack: readonly BrowseRoute[];
}

export type GlobalInputDecision =
  | { kind: 'closeLayer'; layer: GlobalInputLayerKind; restoreTarget: FocusTarget }
  | { kind: 'consume' }
  | { kind: 'navigate'; route: BrowseRoute; restoreTarget: FocusTarget }
  | { kind: 'nativeBack' }
  | { kind: 'openGuide' }
  | { kind: 'spatial' };

export interface GlobalInputPolicy {
  decide(input: HostInputEvent, state: GlobalInputState): GlobalInputDecision;
}

export function createGlobalInputPolicy(): GlobalInputPolicy {
  const focusPolicy = createFocusNavigationPolicy();
  return {
    decide(input, state) {
      if (isSpatialCommand(input.command)) {
        return { kind: 'spatial' };
      }

      if (input.phase !== 'pressed') {
        return { kind: 'consume' };
      }

      if (input.command === 'menu' || input.command === 'view') {
        if (state.layers.modal) {
          return { kind: 'consume' };
        }

        return state.layers.guide
          ? closeLayer('guide', state.layers.guide)
          : { kind: 'openGuide' };
      }

      const activeLayer = getHighestPriorityLayer(state.layers);
      if (activeLayer) {
        return closeLayer(activeLayer.kind, activeLayer.layer);
      }

      const backDecision = focusPolicy.decideBack(state.routeStack);
      if (backDecision.kind === 'navigate') {
        return backDecision;
      }

      return { kind: 'nativeBack' };
    },
  };
}

function isSpatialCommand(command: HostInputEvent['command']): boolean {
  return (
    command === 'accept' ||
    command === 'moveDown' ||
    command === 'moveLeft' ||
    command === 'moveRight' ||
    command === 'moveUp'
  );
}

function getHighestPriorityLayer(layers: GlobalInputLayerState):
  | { kind: GlobalInputLayerKind; layer: GlobalInputLayer }
  | undefined {
  if (layers.modal) {
    return { kind: 'modal', layer: layers.modal };
  }
  if (layers.textInput) {
    return { kind: 'textInput', layer: layers.textInput };
  }
  if (layers.overlay) {
    return { kind: 'overlay', layer: layers.overlay };
  }
  if (layers.guide) {
    return { kind: 'guide', layer: layers.guide };
  }
  return undefined;
}

function closeLayer(
  kind: GlobalInputLayerKind,
  layer: GlobalInputLayer,
): GlobalInputDecision {
  return {
    kind: 'closeLayer',
    layer: kind,
    restoreTarget: layer.returnTarget,
  };
}
