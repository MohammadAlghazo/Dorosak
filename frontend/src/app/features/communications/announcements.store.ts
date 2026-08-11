import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type { Announcement } from '../../core/api/communications-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';

export type AnnouncementsStatus =
  'idle' | 'loading' | 'loadingMore' | 'success' | 'empty' | 'offline' | 'error';

export interface AnnouncementsState {
  readonly status: AnnouncementsStatus;
  readonly courseId: string | null;
  readonly items: readonly Announcement[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly errorCode: string | null;
}

export interface AnnouncementActionState {
  readonly status: 'idle' | 'saving' | 'success' | 'conflict' | 'offline' | 'error';
  readonly operation: 'create' | 'update' | 'delete' | null;
  readonly announcementId: string | null;
  readonly errorCode: string | null;
}

const emptyAnnouncements = (): AnnouncementsState => ({
  status: 'idle',
  courseId: null,
  items: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
});

const idleAction = (): AnnouncementActionState => ({
  status: 'idle',
  operation: null,
  announcementId: null,
  errorCode: null,
});

@Injectable()
export class AnnouncementsStore {
  private readonly api = inject(CommunicationsApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly realtime = inject(CommunicationsRealtimeService);
  private readonly announcementState = signal<AnnouncementsState>(emptyAnnouncements());
  private readonly actionState = signal<AnnouncementActionState>(idleAction());
  private listRequest: Subscription | null = null;
  private listVersion = 0;

  readonly state = this.announcementState.asReadonly();
  readonly action = this.actionState.asReadonly();

  constructor() {
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (
        (event.eventType === 'communication.announcement-created' ||
          event.eventType === 'communication.announcement-updated' ||
          event.eventType === 'communication.announcement-deleted') &&
        event.payload.courseId === this.announcementState().courseId
      ) {
        this.refresh();
      }
    });
    this.realtime.resync$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refresh();
    });
  }

  load(courseId: string, cursor: string | null = null): void {
    const current = this.announcementState();
    const continuing = cursor !== null && current.courseId === courseId;
    if (continuing && (!current.hasMore || current.status === 'loadingMore')) return;
    this.listRequest?.unsubscribe();
    const version = ++this.listVersion;
    this.announcementState.set({
      status: continuing ? 'loadingMore' : 'loading',
      courseId,
      items: continuing ? current.items : [],
      nextCursor: continuing ? current.nextCursor : null,
      hasMore: continuing && current.hasMore,
      errorCode: null,
    });
    if (!this.connectivity.isOnline()) {
      this.announcementState.update((state) => ({ ...state, status: 'offline' }));
      return;
    }
    this.listRequest = this.api
      .getAnnouncements(courseId, 20, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (version !== this.listVersion) return;
          const items = mergeAnnouncements(
            continuing ? this.announcementState().items : [],
            page.items,
          );
          this.announcementState.set({
            status: items.length === 0 ? 'empty' : 'success',
            courseId,
            items,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          if (version !== this.listVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID' && cursor !== null) {
            this.load(courseId);
            return;
          }
          this.announcementState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  loadMore(): void {
    const state = this.announcementState();
    if (state.courseId !== null && state.nextCursor !== null) {
      this.load(state.courseId, state.nextCursor);
    }
  }

  refresh(): void {
    const courseId = this.announcementState().courseId;
    if (courseId !== null) this.load(courseId);
  }

  create(courseId: string, title: string, body: string, idempotencyKey: string): void {
    if (!this.beginAction('create', null)) return;
    this.api
      .createAnnouncement(courseId, { title: title.trim(), body: body.trim() }, idempotencyKey)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (announcement) => {
          this.announcementState.update((state) => ({
            ...state,
            status: 'success',
            items: mergeAnnouncements(state.items, [announcement]),
          }));
          this.actionState.set({
            status: 'success',
            operation: 'create',
            announcementId: announcement.id,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.failAction(error, 'create', null);
        },
      });
  }

  update(
    courseId: string,
    announcementId: string,
    title: string,
    body: string,
    expectedVersion: number,
    idempotencyKey: string,
  ): void {
    if (!this.beginAction('update', announcementId)) return;
    this.api
      .updateAnnouncement(
        courseId,
        announcementId,
        { title: title.trim(), body: body.trim(), expectedVersion },
        idempotencyKey,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (announcement) => {
          this.announcementState.update((state) => ({
            ...state,
            items: mergeAnnouncements(state.items, [announcement]),
          }));
          this.actionState.set({
            status: 'success',
            operation: 'update',
            announcementId,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.failAction(error, 'update', announcementId);
        },
      });
  }

  delete(courseId: string, announcementId: string, expectedVersion: number): void {
    if (!this.beginAction('delete', announcementId)) return;
    this.api
      .deleteAnnouncement(courseId, announcementId, expectedVersion)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.announcementState.update((state) => {
            const items = state.items.filter((item) => item.id !== announcementId);
            return { ...state, items, status: items.length === 0 ? 'empty' : state.status };
          });
          this.actionState.set({
            status: 'success',
            operation: 'delete',
            announcementId,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.failAction(error, 'delete', announcementId);
        },
      });
  }

  resetAction(): void {
    if (this.actionState().status !== 'saving') this.actionState.set(idleAction());
  }

  private beginAction(
    operation: NonNullable<AnnouncementActionState['operation']>,
    announcementId: string | null,
  ): boolean {
    if (this.actionState().status === 'saving') return false;
    if (!this.connectivity.isOnline()) {
      this.actionState.set({ status: 'offline', operation, announcementId, errorCode: 'HTTP.0' });
      return false;
    }
    this.actionState.set({ status: 'saving', operation, announcementId, errorCode: null });
    return true;
  }

  private failAction(
    error: unknown,
    operation: NonNullable<AnnouncementActionState['operation']>,
    announcementId: string | null,
  ): void {
    this.actionState.set({
      status: isAnnouncementConflict(error) ? 'conflict' : isOffline(error) ? 'offline' : 'error',
      operation,
      announcementId,
      errorCode: problemCode(error),
    });
  }
}

export const isAnnouncementConflict = (error: unknown): error is ApiProblem =>
  error instanceof ApiProblem &&
  error.status === 409 &&
  error.code === 'ANNOUNCEMENT.VERSION_CONFLICT';

const mergeAnnouncements = (
  current: readonly Announcement[],
  incoming: readonly Announcement[],
): readonly Announcement[] => {
  const byId = new Map(current.map((item) => [item.id, item]));
  for (const item of incoming) byId.set(item.id, item);
  return [...byId.values()].sort(
    (left, right) => Date.parse(right.createdAt) - Date.parse(left.createdAt),
  );
};

const isOffline = (error: unknown): boolean => error instanceof ApiProblem && error.status === 0;
const problemCode = (error: unknown): string | null =>
  error instanceof ApiProblem ? error.code : null;
