import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type { CommunicationNotification } from '../../core/api/communications-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';
import { NotificationBadgeStore } from './notification-badge.store';

export type NotificationsStatus =
  'idle' | 'loading' | 'loadingMore' | 'resyncing' | 'success' | 'empty' | 'offline' | 'error';

export interface NotificationsState {
  readonly status: NotificationsStatus;
  readonly items: readonly CommunicationNotification[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly latestSequence: number;
  readonly unreadCount: number;
  readonly errorCode: string | null;
}

export interface NotificationMutationState {
  readonly pendingReadIds: ReadonlySet<string>;
  readonly markingAll: boolean;
  readonly errorCode: string | null;
}

const emptyNotifications = (): NotificationsState => ({
  status: 'idle',
  items: [],
  nextCursor: null,
  hasMore: false,
  latestSequence: 0,
  unreadCount: 0,
  errorCode: null,
});

@Injectable()
export class NotificationsStore {
  private readonly api = inject(CommunicationsApiClient);
  private readonly badge = inject(NotificationBadgeStore);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly realtime = inject(CommunicationsRealtimeService);
  private readonly notificationState = signal<NotificationsState>(emptyNotifications());
  private readonly mutationState = signal<NotificationMutationState>({
    pendingReadIds: new Set(),
    markingAll: false,
    errorCode: null,
  });
  private listRequest: Subscription | null = null;
  private listVersion = 0;
  private initialized = false;
  private resyncing = false;
  private resyncQueued = false;

  readonly state = this.notificationState.asReadonly();
  readonly mutation = this.mutationState.asReadonly();

  constructor() {
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.initialized) this.resync();
      else this.resyncQueued = true;
    });
    this.realtime.resync$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.initialized) this.resync();
      else this.resyncQueued = true;
    });
  }

  load(): void {
    this.loadPage(null);
  }

  loadMore(): void {
    const state = this.notificationState();
    if (
      !state.hasMore ||
      state.nextCursor === null ||
      state.status === 'loading' ||
      state.status === 'loadingMore'
    ) {
      return;
    }
    this.loadPage(state.nextCursor);
  }

  retry(): void {
    this.load();
  }

  resync(): void {
    if (!this.initialized) {
      this.load();
      return;
    }
    if (this.resyncing) {
      this.resyncQueued = true;
      return;
    }
    if (!this.connectivity.isOnline()) {
      this.notificationState.update((state) => ({ ...state, status: 'offline' }));
      return;
    }
    this.resyncing = true;
    this.resyncQueued = false;
    const version = ++this.listVersion;
    const afterSequence = this.notificationState().latestSequence;
    this.notificationState.update((state) => ({ ...state, status: 'resyncing', errorCode: null }));
    this.loadResyncPage(afterSequence, null, [], version);
  }

  markRead(notificationId: string): void {
    const state = this.notificationState();
    const notification = state.items.find((item) => item.id === notificationId);
    if (
      notification?.isRead !== false ||
      this.mutationState().pendingReadIds.has(notificationId) ||
      !this.connectivity.isOnline()
    ) {
      if (!this.connectivity.isOnline()) {
        this.mutationState.update((mutation) => ({ ...mutation, errorCode: 'HTTP.0' }));
      }
      return;
    }

    const optimisticReadAt = new Date().toISOString();
    const shouldRollback = notification.readAt === null;
    this.notificationState.set({
      ...state,
      items: state.items.map((item) =>
        item.id === notificationId ? { ...item, isRead: true, readAt: optimisticReadAt } : item,
      ),
      unreadCount: Math.max(0, state.unreadCount - 1),
    });
    this.mutationState.update((mutation) => ({
      ...mutation,
      pendingReadIds: new Set([...mutation.pendingReadIds, notificationId]),
      errorCode: null,
    }));
    this.badge.markOneRead();
    this.api
      .markNotificationRead(notificationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.notificationState.update((current) => ({
            ...current,
            items: mergeNotifications(current.items, [updated]),
          }));
          this.finishRead(notificationId, null);
        },
        error: (error: unknown) => {
          const canRollback = this.notificationState().items.some(
            (item) => item.id === notificationId && item.readAt === optimisticReadAt,
          );
          this.notificationState.update((current) => ({
            ...current,
            items: current.items.map((item) => {
              if (item.id !== notificationId || item.readAt !== optimisticReadAt) return item;
              return { ...item, isRead: false, readAt: null };
            }),
            unreadCount: current.unreadCount + (shouldRollback && canRollback ? 1 : 0),
          }));
          if (shouldRollback && canRollback) this.badge.rollbackOneRead();
          this.finishRead(notificationId, problemCode(error));
        },
      });
  }

  markAllRead(): void {
    const state = this.notificationState();
    if (state.unreadCount === 0 || this.mutationState().markingAll) return;
    if (!this.connectivity.isOnline()) {
      this.mutationState.update((mutation) => ({ ...mutation, errorCode: 'HTTP.0' }));
      return;
    }
    this.mutationState.update((mutation) => ({
      ...mutation,
      markingAll: true,
      errorCode: null,
    }));
    this.api
      .markAllNotificationsRead()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const readAt = new Date().toISOString();
          this.notificationState.update((current) => ({
            ...current,
            items: current.items.map((item) =>
              !item.isRead && item.sequence <= result.throughSequence
                ? { ...item, isRead: true, readAt }
                : item,
            ),
            unreadCount: Math.max(0, current.unreadCount - result.updatedCount),
          }));
          this.badge.markAllRead(result.updatedCount, result.throughSequence);
          this.mutationState.update((mutation) => ({
            ...mutation,
            markingAll: false,
            errorCode: null,
          }));
        },
        error: (error: unknown) => {
          this.mutationState.update((mutation) => ({
            ...mutation,
            markingAll: false,
            errorCode: problemCode(error),
          }));
        },
      });
  }

  private loadPage(cursor: string | null): void {
    this.listRequest?.unsubscribe();
    const version = ++this.listVersion;
    const current = this.notificationState();
    const continuing = cursor !== null;
    this.notificationState.set({
      ...current,
      status: continuing ? 'loadingMore' : 'loading',
      items: continuing ? current.items : [],
      nextCursor: continuing ? current.nextCursor : null,
      hasMore: continuing && current.hasMore,
      errorCode: null,
    });
    if (!this.connectivity.isOnline()) {
      this.notificationState.update((state) => ({ ...state, status: 'offline' }));
      return;
    }
    this.listRequest = this.api
      .getNotifications(20, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (version !== this.listVersion) return;
          const items = mergeNotifications(
            continuing ? this.notificationState().items : [],
            page.items,
          );
          this.initialized = true;
          this.notificationState.set({
            status: items.length === 0 ? 'empty' : 'success',
            items,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            latestSequence: page.latestSequence,
            unreadCount: page.unreadCount,
            errorCode: null,
          });
          this.badge.synchronize(page.unreadCount, page.latestSequence);
          if (this.resyncQueued) this.resync();
        },
        error: (error: unknown) => {
          if (version !== this.listVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID' && cursor !== null) {
            this.load();
            return;
          }
          this.notificationState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  private loadResyncPage(
    afterSequence: number,
    cursor: string | null,
    incoming: readonly CommunicationNotification[],
    version: number,
  ): void {
    this.listRequest = this.api
      .getNotifications(100, cursor, afterSequence)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (version !== this.listVersion) return;
          const accumulated = mergeNotifications(incoming, page.items);
          if (page.hasMore && page.nextCursor !== null) {
            this.loadResyncPage(afterSequence, page.nextCursor, accumulated, version);
            return;
          }
          const items = mergeNotifications(this.notificationState().items, accumulated);
          this.notificationState.update((state) => ({
            ...state,
            status: items.length === 0 ? 'empty' : 'success',
            items,
            latestSequence: Math.max(state.latestSequence, page.latestSequence),
            unreadCount: page.unreadCount,
            errorCode: null,
          }));
          this.badge.synchronize(page.unreadCount, page.latestSequence);
          this.completeResync();
        },
        error: (error: unknown) => {
          if (version !== this.listVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID' && cursor !== null) {
            this.loadResyncPage(afterSequence, null, [], version);
            return;
          }
          this.notificationState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
          this.completeResync();
        },
      });
  }

  private completeResync(): void {
    this.resyncing = false;
    if (this.resyncQueued) this.resync();
  }

  private finishRead(notificationId: string, errorCode: string | null): void {
    this.mutationState.update((mutation) => {
      const pendingReadIds = new Set(mutation.pendingReadIds);
      pendingReadIds.delete(notificationId);
      return { ...mutation, pendingReadIds, errorCode };
    });
  }
}

const mergeNotifications = (
  current: readonly CommunicationNotification[],
  incoming: readonly CommunicationNotification[],
): readonly CommunicationNotification[] => {
  const byId = new Map(current.map((item) => [item.id, item]));
  for (const item of incoming) byId.set(item.id, item);
  return [...byId.values()].sort((left, right) => right.sequence - left.sequence);
};

const isOffline = (error: unknown): boolean => error instanceof ApiProblem && error.status === 0;
const problemCode = (error: unknown): string | null =>
  error instanceof ApiProblem ? error.code : null;
