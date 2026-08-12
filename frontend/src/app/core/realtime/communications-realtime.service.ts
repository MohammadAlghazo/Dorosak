import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, effect, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import type { CommunicationRealtimeEvent } from '../api/communications-api.types';
import { SessionLifecycleService } from '../auth/session-lifecycle.service';
import { SessionStore } from '../auth/session.store';
import { ConnectivityStore } from '../pwa/connectivity.store';

export type CommunicationsRealtimeStatus =
  'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

const hubPath = '/hubs/communications';
const reconnectDelays = [0, 2_000, 5_000, 10_000, 30_000] as const;

@Injectable({ providedIn: 'root' })
export class CommunicationsRealtimeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(SessionStore);
  private readonly sessionLifecycle = inject(SessionLifecycleService);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly eventSource = new Subject<CommunicationRealtimeEvent>();
  private readonly resyncSource = new Subject<void>();
  private readonly connectionStatus = signal<CommunicationsRealtimeStatus>('idle');
  private readonly recentEventIds = new Set<string>();
  private connection: HubConnection | null = null;
  private activeUserId: string | null = null;
  private transitionVersion = 0;
  private retryAttempt = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly events$ = this.eventSource.asObservable();
  readonly resync$ = this.resyncSource.asObservable();
  readonly status = this.connectionStatus.asReadonly();

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;

    this.sessionLifecycle.ending$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.disconnect();
    });
    effect(() => {
      void this.transitionTo(this.currentTargetUserId());
    });
    this.destroyRef.onDestroy(() => {
      this.transitionVersion++;
      this.cancelRetryTimer();
      this.retryAttempt = 0;
      const connection = this.connection;
      this.connection = null;
      this.activeUserId = null;
      if (connection) void stopConnection(connection);
      this.eventSource.complete();
      this.resyncSource.complete();
    });
  }

  disconnect(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    void this.transitionTo(null);
  }

  private async transitionTo(userId: string | null): Promise<void> {
    const isSameUser = userId !== null && userId === this.activeUserId;
    if (
      isSameUser &&
      (this.retryTimer !== null ||
        (this.connection !== null && this.connection.state !== HubConnectionState.Disconnected))
    ) {
      return;
    }

    const version = ++this.transitionVersion;
    this.cancelRetryTimer();
    if (!isSameUser) this.retryAttempt = 0;
    const previous = this.connection;
    this.connection = null;
    this.activeUserId = null;
    this.recentEventIds.clear();
    this.connectionStatus.set(userId === null ? 'disconnected' : 'connecting');
    if (previous) await stopConnection(previous);
    if (version !== this.transitionVersion) return;
    if (userId === null) return;

    const connection = new HubConnectionBuilder()
      .withUrl(hubPath, {
        accessTokenFactory: () => this.session.accessToken() ?? '',
      })
      .withAutomaticReconnect(boundedReconnectPolicy)
      .configureLogging(LogLevel.None)
      .build();

    connection.on('communicationEvent', (candidate: unknown) => {
      if (connection !== this.connection || this.currentTargetUserId() !== userId) return;
      const event = parseCommunicationEvent(candidate);
      if (event === null || this.recentEventIds.has(event.eventId)) return;
      if (this.recentEventIds.size >= 256) {
        const oldest = this.recentEventIds.values().next().value;
        if (oldest !== undefined) this.recentEventIds.delete(oldest);
      }
      this.recentEventIds.add(event.eventId);
      this.eventSource.next(event);
    });
    connection.onreconnecting(() => {
      if (connection !== this.connection) return;
      const targetUserId = this.currentTargetUserId();
      if (targetUserId !== userId) {
        void this.transitionTo(targetUserId);
        return;
      }
      this.connectionStatus.set('reconnecting');
    });
    connection.onreconnected(() => {
      if (connection !== this.connection) return;
      const targetUserId = this.currentTargetUserId();
      if (targetUserId !== userId) {
        void this.transitionTo(targetUserId);
        return;
      }
      this.resetRetryState();
      this.connectionStatus.set('connected');
      this.resyncSource.next();
    });
    connection.onclose(() => {
      if (connection !== this.connection) return;
      const targetUserId = this.currentTargetUserId();
      if (targetUserId === userId) {
        this.connectionStatus.set('disconnected');
        this.scheduleRetry(userId, connection);
      } else {
        void this.transitionTo(targetUserId);
      }
    });

    this.connection = connection;
    this.activeUserId = userId;
    try {
      await connection.start();
      if (version !== this.transitionVersion || connection !== this.connection) {
        await stopConnection(connection);
        return;
      }
      const targetUserId = this.currentTargetUserId();
      if (targetUserId !== userId) {
        void this.transitionTo(targetUserId);
        return;
      }
      this.resetRetryState();
      this.connectionStatus.set('connected');
    } catch {
      if (version === this.transitionVersion && connection === this.connection) {
        const targetUserId = this.currentTargetUserId();
        if (targetUserId === userId) {
          this.connectionStatus.set('error');
          this.scheduleRetry(userId, connection);
        } else {
          void this.transitionTo(targetUserId);
        }
      }
    }
  }

  private currentTargetUserId(): string | null {
    if (!this.connectivity.isOnline() || !this.session.isAuthenticated()) return null;
    return this.session.identity()?.userId ?? null;
  }

  private scheduleRetry(userId: string, connection: HubConnection): void {
    if (
      this.retryTimer !== null ||
      connection !== this.connection ||
      this.currentTargetUserId() !== userId
    ) {
      return;
    }

    const delay = reconnectDelay(this.retryAttempt++);
    const version = this.transitionVersion;
    const timer = setTimeout(() => {
      if (this.retryTimer !== timer) return;
      this.retryTimer = null;
      if (
        version !== this.transitionVersion ||
        connection !== this.connection ||
        this.currentTargetUserId() !== userId
      ) {
        return;
      }
      void this.transitionTo(userId);
    }, delay);
    this.retryTimer = timer;
  }

  private resetRetryState(): void {
    this.cancelRetryTimer();
    this.retryAttempt = 0;
  }

  private cancelRetryTimer(): void {
    if (this.retryTimer === null) return;
    clearTimeout(this.retryTimer);
    this.retryTimer = null;
  }
}

const reconnectDelay = (attempt: number): number =>
  reconnectDelays[Math.min(attempt, reconnectDelays.length - 1)] ?? 30_000;

const boundedReconnectPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds(context: RetryContext): number | null {
    return reconnectDelays[context.previousRetryCount] ?? null;
  },
};

const stopConnection = async (connection: HubConnection): Promise<void> => {
  try {
    await connection.stop();
  } catch {
    // Local teardown remains complete even if the transport has already disappeared.
  }
};

export const parseCommunicationEvent = (candidate: unknown): CommunicationRealtimeEvent | null => {
  if (!isRecord(candidate) || candidate['schemaVersion'] !== 1) return null;
  if (
    !isString(candidate['eventId']) ||
    !isString(candidate['occurredAt']) ||
    !isString(candidate['eventType']) ||
    !isRecord(candidate['payload'])
  ) {
    return null;
  }
  const payload = candidate['payload'];
  const eventId = candidate['eventId'];
  const occurredAt = candidate['occurredAt'];
  switch (candidate['eventType']) {
    case 'communication.conversation-created':
      if (!hasStrings(payload, 'conversationId', 'createdByUserId', 'courseId')) return null;
      return {
        eventId,
        eventType: 'communication.conversation-created',
        schemaVersion: 1,
        occurredAt,
        payload: {
          conversationId: stringField(payload, 'conversationId'),
          createdByUserId: stringField(payload, 'createdByUserId'),
          courseId: stringField(payload, 'courseId'),
        },
      };
    case 'communication.message-created':
      if (
        !hasStrings(payload, 'messageId', 'conversationId', 'senderUserId') ||
        !isNonNegativeInteger(payload['sequence'])
      ) {
        return null;
      }
      return {
        eventId,
        eventType: 'communication.message-created',
        schemaVersion: 1,
        occurredAt,
        payload: {
          messageId: stringField(payload, 'messageId'),
          conversationId: stringField(payload, 'conversationId'),
          senderUserId: stringField(payload, 'senderUserId'),
          sequence: payload['sequence'],
        },
      };
    case 'communication.conversation-left':
      if (!hasStrings(payload, 'conversationId', 'userId')) return null;
      return {
        eventId,
        eventType: 'communication.conversation-left',
        schemaVersion: 1,
        occurredAt,
        payload: {
          conversationId: stringField(payload, 'conversationId'),
          userId: stringField(payload, 'userId'),
        },
      };
    case 'communication.announcement-created':
      if (
        !hasStrings(payload, 'announcementId', 'courseId', 'createdByUserId') ||
        !isPositiveInteger(payload['version']) ||
        !isNonNegativeInteger(payload['targetCount'])
      ) {
        return null;
      }
      return {
        eventId,
        eventType: 'communication.announcement-created',
        schemaVersion: 1,
        occurredAt,
        payload: {
          announcementId: stringField(payload, 'announcementId'),
          courseId: stringField(payload, 'courseId'),
          createdByUserId: stringField(payload, 'createdByUserId'),
          version: payload['version'],
          targetCount: payload['targetCount'],
        },
      };
    case 'communication.announcement-updated':
      if (
        !hasStrings(payload, 'announcementId', 'courseId', 'updatedByUserId') ||
        !isPositiveInteger(payload['version']) ||
        !isNonNegativeInteger(payload['targetCount'])
      ) {
        return null;
      }
      return {
        eventId,
        eventType: 'communication.announcement-updated',
        schemaVersion: 1,
        occurredAt,
        payload: {
          announcementId: stringField(payload, 'announcementId'),
          courseId: stringField(payload, 'courseId'),
          updatedByUserId: stringField(payload, 'updatedByUserId'),
          version: payload['version'],
          targetCount: payload['targetCount'],
        },
      };
    case 'communication.announcement-deleted':
      if (
        !hasStrings(payload, 'announcementId', 'courseId', 'deletedByUserId') ||
        !isPositiveInteger(payload['version'])
      ) {
        return null;
      }
      return {
        eventId,
        eventType: 'communication.announcement-deleted',
        schemaVersion: 1,
        occurredAt,
        payload: {
          announcementId: stringField(payload, 'announcementId'),
          courseId: stringField(payload, 'courseId'),
          deletedByUserId: stringField(payload, 'deletedByUserId'),
          version: payload['version'],
        },
      };
    default:
      return null;
  }
};

const isRecord = (value: unknown): value is Readonly<Record<string, unknown>> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const isString = (value: unknown): value is string => typeof value === 'string' && value.length > 0;

const hasStrings = (value: Readonly<Record<string, unknown>>, ...keys: string[]): boolean =>
  keys.every((key) => isString(value[key]));

const stringField = (value: Readonly<Record<string, unknown>>, key: string): string =>
  value[key] as string;

const isNonNegativeInteger = (value: unknown): value is number =>
  typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;

const isPositiveInteger = (value: unknown): value is number =>
  isNonNegativeInteger(value) && value > 0;
