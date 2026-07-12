import { describe, expect, it } from 'vitest';
import { createFocusRestoreRequest } from './focusRequests';

describe('focus restore requests', () => {
  it('creates unique event identities without embedding target values', () => {
    const first = createFocusRestoreRequest({
      scopeKey: 'scope-private-anonymous',
      focusKey: 'focus-private-anonymous',
    });
    const second = createFocusRestoreRequest({
      scopeKey: 'scope-private-anonymous',
      focusKey: 'focus-private-anonymous',
    });

    expect(first.target).toEqual({
      scopeKey: 'scope-private-anonymous',
      focusKey: 'focus-private-anonymous',
    });
    expect(second.requestId).not.toBe(first.requestId);
    expect(String(first.requestId)).not.toContain('scope-private-anonymous');
    expect(String(first.requestId)).not.toContain('focus-private-anonymous');
  });

  it('snapshots the target instead of retaining caller-owned route data', () => {
    const target = {
      scopeKey: 'snapshot-scope-anonymous',
      focusKey: 'snapshot-focus-anonymous',
    };

    const request = createFocusRestoreRequest(target);
    target.scopeKey = 'caller-mutated-scope-anonymous';
    target.focusKey = 'caller-mutated-focus-anonymous';

    expect(request.target).toEqual({
      scopeKey: 'snapshot-scope-anonymous',
      focusKey: 'snapshot-focus-anonymous',
    });
  });
});
