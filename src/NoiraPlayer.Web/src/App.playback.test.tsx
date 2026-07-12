// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { HostLifecycleEvent } from './bridge';
import { requestBridge, subscribeHostLifecycle } from './bridge';
import { loadHomeCatalog, loadLibraryLatestCatalog } from './catalog/homeCatalog';
import { FocusProvider } from './focus/FocusProvider';
import { createFocusNavigationPolicy } from './focus/focusPolicy';
import { createEmbyFetchTransport } from './transport';
import { App } from './App';

vi.mock('./bridge', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./bridge')>();
  return {
    ...actual,
    requestBridge: vi.fn(),
    subscribeHostLifecycle: vi.fn(),
  };
});

vi.mock('./catalog/homeCatalog', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./catalog/homeCatalog')>();
  return {
    ...actual,
    loadHomeCatalog: vi.fn(),
    loadLibraryLatestCatalog: vi.fn(),
  };
});

vi.mock('./transport', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./transport')>();
  return {
    ...actual,
    createEmbyFetchTransport: vi.fn(),
  };
});

const lifecycleListeners = new Set<(event: HostLifecycleEvent) => void>();

beforeEach(() => {
  lifecycleListeners.clear();
  vi.mocked(subscribeHostLifecycle).mockImplementation((listener) => {
    lifecycleListeners.add(listener);
    return () => lifecycleListeners.delete(listener);
  });
  vi.mocked(loadHomeCatalog).mockResolvedValue({
    rows: [
      {
        key: 'latest',
        title: 'Latest anonymous',
        kind: 'latest',
        items: [
          {
            id: 'playback-details-item-anonymous',
            name: 'Playback details item anonymous',
            type: 'Movie',
            artwork: {},
          },
        ],
      },
    ],
    failedKinds: [],
  });
  vi.mocked(loadLibraryLatestCatalog).mockResolvedValue({
    rows: [],
    failedRowKeys: [],
  });
  vi.mocked(createEmbyFetchTransport).mockReturnValue(
    vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(
        typeof input === 'string' || input instanceof URL
          ? String(input)
          : input.url,
      );
      if (!url.pathname.endsWith('/Items/playback-details-item-anonymous')) {
        throw new Error(`Unexpected playback test request: ${url.pathname}`);
      }

      return new Response(
        JSON.stringify({
          Id: 'playback-details-item-anonymous',
          Name: 'Playback details result anonymous',
          Type: 'Movie',
          RunTimeTicks: 72000000000,
          UserData: { PlaybackPositionTicks: 1800000000 },
          MediaSources: [{ Id: 'media-source-anonymous' }],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      );
    }),
  );
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('App native playback handoff', () => {
  it('returns an already-running Details route to Home when the host is launched again', async () => {
    vi.mocked(requestBridge).mockResolvedValueOnce(bootstrapResult());

    render(
      <FocusProvider>
        <App />
      </FocusProvider>,
    );
    const card = await screen.findByRole('button', {
      name: 'Open Playback details item anonymous',
    });
    fireEvent.click(card);
    await screen.findByRole('button', { name: 'Resume' });

    act(() => {
      for (const listener of [...lifecycleListeners]) {
        listener({ type: 'host.lifecycle', event: 'activated-home' });
      }
    });

    expect(screen.queryByRole('button', { name: 'Resume' })).toBeNull();
    const restoredCard = screen.getByRole('button', {
      name: 'Open Playback details item anonymous',
    });
    await waitFor(() => expect(document.activeElement).toBe(restoredCard));
  });

  it('pauses before sending the exact payload and resumes Details from a host event', async () => {
    let resolvePlayback!: (value: unknown) => void;
    const playback = new Promise((resolve) => {
      resolvePlayback = resolve;
    });
    vi.mocked(requestBridge)
      .mockResolvedValueOnce(bootstrapResult())
      .mockReturnValueOnce(playback);
    const policy = createFocusNavigationPolicy();

    render(
      <FocusProvider policy={policy}>
        <App />
      </FocusProvider>,
    );
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Open Playback details item anonymous',
      }),
    );
    const resume = await screen.findByRole('button', { name: 'Resume' });
    fireEvent.click(resume);
    fireEvent.click(resume);

    expect(policy.isPaused()).toBe(true);
    expect(vi.mocked(requestBridge)).toHaveBeenCalledTimes(2);
    expect(vi.mocked(requestBridge).mock.calls[1]).toEqual([
      'playback.nativePlayItem',
      {
        itemId: 'playback-details-item-anonymous',
        itemName: 'Playback details result anonymous',
        startPositionTicks: 1800000000,
        mediaSourceId: 'media-source-anonymous',
        runtimeTicks: 72000000000,
      },
    ]);

    await act(async () => {
      resolvePlayback({ started: true, surface: 'native' });
      await playback;
    });
    expect(policy.isPaused()).toBe(true);

    const outside = document.createElement('button');
    document.body.append(outside);
    outside.focus();
    act(() => {
      for (const listener of [...lifecycleListeners]) {
        listener({ type: 'host.lifecycle', event: 'playback-returned' });
      }
    });

    expect(policy.isPaused()).toBe(false);
    await waitFor(() => expect(document.activeElement).toBe(resume));
    vi.mocked(requestBridge).mockReturnValueOnce(new Promise(() => undefined));
    fireEvent.click(resume);
    expect(vi.mocked(requestBridge)).toHaveBeenCalledTimes(3);
    outside.remove();
  });

  it('resumes input immediately and restores Resume when native launch rejects', async () => {
    vi.mocked(requestBridge)
      .mockResolvedValueOnce(bootstrapResult())
      .mockRejectedValueOnce(new Error('Native launch rejected anonymous'));
    const policy = createFocusNavigationPolicy();

    render(
      <FocusProvider policy={policy}>
        <App />
      </FocusProvider>,
    );
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Open Playback details item anonymous',
      }),
    );
    const resume = await screen.findByRole('button', { name: 'Resume' });
    fireEvent.click(resume);

    await screen.findByRole('alert');
    expect(policy.isPaused()).toBe(false);
    await waitFor(() => expect(document.activeElement).toBe(resume));
    vi.mocked(requestBridge).mockReturnValueOnce(new Promise(() => undefined));
    fireEvent.click(resume);
    expect(vi.mocked(requestBridge)).toHaveBeenCalledTimes(3);
  });
});

function bootstrapResult() {
  return {
    session: {
      serverUrl: 'https://anonymous.invalid',
      userId: 'anonymous-user',
      userName: 'Anonymous User',
      accessToken: 'anonymous-token',
      authorization: 'Anonymous authorization',
    },
  };
}
