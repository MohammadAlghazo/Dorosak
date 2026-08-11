import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import { SessionStore } from '../../core/auth/session.store';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';

export type NotificationBadgeStatus = 'idle' | 'loading' | 'success' | 'offline' | 'error';

export interface NotificationBadgeState {
  readonly status: NotificationBadgeStatus;
  readonly count: number;
  readonly latestSequence: number;
  readonly errorCode: string | null;
}

const emptyBadge = (): NotificationBadgeState => ({
  status: 'idle',
  count: 0,
  latestSequence: 0,
  errorCode: null,
});

@Injectable({ providedIn: 'root' })
export class NotificationBadgeStore {
  private readonly api = inject(CommunicationsApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly realtime = inject(CommunicationsRealtimeService);
  private readonly session = inject(SessionStore);
  private readonly badgeState = signal<NotificationBadgeState>(emptyBadge());
  private request: Subscription | null = null;
  private requestVersion = 0;
  private accountUserId: string | null = null;

  readonly state = this.badgeState.asReadonly();
  readonly count = computed(() => this.badgeState().count);
  readonly unreadCount = this.count;

  constructor() {
    effect(() => {
      const userId = this.session.isAuthenticated()
        ? (this.session.identity()?.userId ?? null)
        : null;
      const allowed = this.session.hasPermission('Notification.ReadOwn');
      const online = this.connectivity.isOnline();
      if (userId !== this.accountUserId) {
        this.accountUserId = userId;
        this.clear();
      }
      const status = this.badgeState().status;
      if (userId !== null && allowed && online && (status === 'idle' || status === 'offline')) {
        this.load();
      }
      if (userId !== null && allowed && !online && status !== 'offline') {
        this.badgeState.update((state) => ({ ...state, status: 'offline', errorCode: null }));
      }
    });
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.accountUserId !== null) this.load();
    });
    this.realtime.resync$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.accountUserId !== null) this.load();
    });
  }

  load(): void {
    if (
      this.accountUserId === null ||
      !this.session.hasPermission('Notification.ReadOwn') ||
      !this.connectivity.isOnline()
    ) {
      return;
    }
    this.request?.unsubscribe();
    const version = ++this.requestVersion;
    this.badgeState.update((state) => ({ ...state, status: 'loading', errorCode: null }));
    this.request = this.api
      .getNotificationUnreadCount()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          if (version !== this.requestVersion) return;
          this.badgeState.set({
            status: 'success',
            count: result.count,
            latestSequence: result.latestSequence,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          if (version !== this.requestVersion) return;
          this.badgeState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  synchronize(count: number, latestSequence: number): void {
    this.badgeState.set({
      status: 'success',
      count,
      latestSequence,
      errorCode: null,
    });
  }

  markOneRead(): void {
    this.badgeState.update((state) => ({ ...state, count: Math.max(0, state.count - 1) }));
  }

  rollbackOneRead(): void {
    this.badgeState.update((state) => ({ ...state, count: state.count + 1 }));
  }

  markAllRead(updatedCount: number, throughSequence: number): void {
    this.badgeState.update((state) => ({
      ...state,
      count: Math.max(0, state.count - updatedCount),
      latestSequence: Math.max(state.latestSequence, throughSequence),
    }));
  }

  private clear(): void {
    this.requestVersion++;
    this.request?.unsubscribe();
    this.request = null;
    this.badgeState.set(emptyBadge());
  }
}

const isOffline = (error: unknown): boolean => error instanceof ApiProblem && error.status === 0;
const problemCode = (error: unknown): string | null =>
  error instanceof ApiProblem ? error.code : null;
