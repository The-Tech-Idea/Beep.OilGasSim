import { describe, expect, test } from 'vitest';
import { isTransientRealtimeError } from './GameHubClient';

describe('isTransientRealtimeError', () => {
  test('treats SignalR negotiation aborts as transient teardown noise', () => {
    expect(
      isTransientRealtimeError(new Error('The connection was stopped during negotiation.')),
    ).toBe(true);
  });

  test('does not hide unrelated realtime failures', () => {
    expect(isTransientRealtimeError(new Error('401 Unauthorized'))).toBe(false);
  });
});
