import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
import type { AuthSession } from '../api/identity-api.types';
import { SessionStore } from '../auth/session.store';
import { ConnectivityStore } from '../pwa/connectivity.store';
import {
  CommunicationsRealtimeService,
  parseCommunicationEvent,
} from './communications-realtime.service';

describe('CommunicationsRealtimeService', () => {
  let connections: FakeHubConnection[];
  let online: ReturnType<typeof signal<boolean>>;
  let session: SessionStore;

  beforeEach(() => {
    vi.useFakeTimers();
    connections = [];
    online = signal(true);
    TestBed.configureTestingModule({
      providers: [
        CommunicationsRealtimeService,
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ConnectivityStore, useValue: { isOnline: online.asReadonly() } },
      ],
    });
    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockImplementation(() => {
      const connection = connections.shift();
      if (!connection) throw new Error('The test connection queue is empty.');
      return connection as unknown as HubConnection;
    });
    session = TestBed.inject(SessionStore);
    session.establish(authSession());
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('retries an initial start failure for the same online user', async () => {
    const first = new FakeHubConnection(new Error('initial failure'));
    const second = new FakeHubConnection();
    connections.push(first, second);

    const service = TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    await settlePromises();

    expect(first.start).toHaveBeenCalledOnce();
    expect(service.status()).toBe('error');
    expect(vi.getTimerCount()).toBe(1);

    await vi.advanceTimersByTimeAsync(0);
    await settlePromises();

    expect(second.start).toHaveBeenCalledOnce();
    expect(service.status()).toBe('connected');
  });

  it('uses the standard access token factory without a token query parameter', async () => {
    const connection = new FakeHubConnection();
    connections.push(connection);
    const withUrl = vi.spyOn(HubConnectionBuilder.prototype, 'withUrl');

    TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    await settlePromises();

    const call = withUrl.mock.calls[0];
    if (!call) throw new Error('Expected SignalR hub URL configuration.');
    const [url, options] = call;
    expect(url).toBe('/hubs/communications');
    expect(url).not.toContain('?');
    expect(options.accessTokenFactory?.()).toBe('access-token');
  });

  it('caps fresh connection retry delays at 30 seconds', async () => {
    const failedConnections = Array.from(
      { length: 7 },
      () => new FakeHubConnection(new Error('unavailable')),
    );
    connections.push(...failedConnections);

    TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    await settlePromises();
    expect(failedConnections[0]?.start).toHaveBeenCalledOnce();

    for (const [index, delay] of [0, 2_000, 5_000, 10_000, 30_000, 30_000].entries()) {
      const nextConnection = failedConnections[index + 1];
      if (!nextConnection) throw new Error('Expected another failed test connection.');
      expect(vi.getTimerCount()).toBe(1);
      if (delay > 0) {
        await vi.advanceTimersByTimeAsync(delay - 1);
        expect(nextConnection.start).not.toHaveBeenCalled();
        await vi.advanceTimersByTimeAsync(1);
      } else {
        await vi.advanceTimersByTimeAsync(0);
      }
      await settlePromises();
      expect(nextConnection.start).toHaveBeenCalledOnce();
    }

    expect(vi.getTimerCount()).toBe(1);
  });

  it('schedules a fresh connection after the current connection closes', async () => {
    const first = new FakeHubConnection();
    const second = new FakeHubConnection();
    connections.push(first, second);

    const service = TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    await settlePromises();
    expect(service.status()).toBe('connected');

    first.close();

    expect(service.status()).toBe('disconnected');
    expect(vi.getTimerCount()).toBe(1);
    await vi.advanceTimersByTimeAsync(0);
    await settlePromises();

    expect(second.start).toHaveBeenCalledOnce();
    expect(service.status()).toBe('connected');
  });

  it('cancels a pending retry when the session logs out', async () => {
    const first = new FakeHubConnection(new Error('initial failure'));
    const second = new FakeHubConnection();
    connections.push(first, second);

    const service = TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    await settlePromises();
    expect(vi.getTimerCount()).toBe(1);

    session.markAnonymous();
    TestBed.tick();
    expect(service.status()).toBe('disconnected');
    await settlePromises();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(vi.getTimerCount()).toBe(0);
    expect(second.start).not.toHaveBeenCalled();
  });

  it('keeps one retry timer when start failure and close overlap', async () => {
    const first = new FakeHubConnection(new Error('initial failure'));
    const second = new FakeHubConnection(new Error('retry failure'));
    connections.push(first, second);

    TestBed.inject(CommunicationsRealtimeService);
    TestBed.tick();
    first.close();
    await settlePromises();

    expect(vi.getTimerCount()).toBe(1);
    await vi.advanceTimersByTimeAsync(0);
    await settlePromises();
    expect(second.start).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(1);

    second.close();
    second.close();
    expect(vi.getTimerCount()).toBe(1);
  });
});

class FakeHubConnection {
  state = HubConnectionState.Disconnected;
  private readonly eventCallbacks = new Map<string, (...args: unknown[]) => unknown>();
  private closeCallback: ((error?: Error) => void) | null = null;
  private reconnectingCallback: ((error?: Error) => void) | null = null;
  private reconnectedCallback: ((connectionId?: string) => void) | null = null;

  readonly start = vi.fn((): Promise<void> => {
    this.state = HubConnectionState.Connecting;
    if (this.startError !== null) {
      this.state = HubConnectionState.Disconnected;
      return Promise.reject(this.startError);
    }
    this.state = HubConnectionState.Connected;
    return Promise.resolve();
  });
  readonly stop = vi.fn(() => {
    this.state = HubConnectionState.Disconnected;
    return Promise.resolve();
  });

  constructor(private readonly startError: Error | null = null) {}

  on(methodName: string, newMethod: (...args: unknown[]) => unknown): void {
    this.eventCallbacks.set(methodName, newMethod);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingCallback = callback;
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedCallback = callback;
  }

  onclose(callback: (error?: Error) => void): void {
    this.closeCallback = callback;
  }

  close(): void {
    this.state = HubConnectionState.Disconnected;
    this.closeCallback?.();
  }

  reconnect(): void {
    this.state = HubConnectionState.Reconnecting;
    this.reconnectingCallback?.();
  }

  reconnectSuccessfully(): void {
    this.state = HubConnectionState.Connected;
    this.reconnectedCallback?.();
  }
}

const settlePromises = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
};

const authSession = (): AuthSession => ({
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  identity: {
    userId: 'user-1',
    sessionId: 'session-1',
    displayName: 'Test User',
    email: 'user@example.test',
    emailVerified: true,
    mfaEnabled: false,
    authenticatedAt: '2029-12-31T23:50:00Z',
    recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
    authorizationVersion: 1,
    roles: ['Student'],
    permissions: [],
    authenticationMethods: ['pwd'],
  },
});

describe('parseCommunicationEvent', () => {
  it('accepts a metadata-only message event', () => {
    const event = parseCommunicationEvent({
      eventId: 'event-1',
      eventType: 'communication.message-created',
      schemaVersion: 1,
      occurredAt: '2030-01-01T00:00:00Z',
      payload: {
        messageId: 'message-1',
        conversationId: 'conversation-1',
        senderUserId: 'user-1',
        sequence: 4,
        body: 'This field must be discarded.',
      },
    });

    expect(event?.eventType).toBe('communication.message-created');
    expect(event && 'body' in event.payload).toBe(false);
  });

  it('rejects unsupported schemas and malformed payloads', () => {
    expect(
      parseCommunicationEvent({
        eventId: 'event-1',
        eventType: 'communication.message-created',
        schemaVersion: 2,
        occurredAt: '2030-01-01T00:00:00Z',
        payload: {},
      }),
    ).toBeNull();
    expect(
      parseCommunicationEvent({
        eventId: 'event-2',
        eventType: 'communication.message-created',
        schemaVersion: 1,
        occurredAt: '2030-01-01T00:00:00Z',
        payload: { body: 'A body must never be treated as event metadata.' },
      }),
    ).toBeNull();
  });
});
