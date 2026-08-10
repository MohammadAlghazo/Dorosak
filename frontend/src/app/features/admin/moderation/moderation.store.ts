import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';
import { ModerationApiClient } from '../../../core/api/moderation-api.client';
import type {
  AdminContentReportResponse,
  ContentReportTargetKind,
  ModerationActionRequest,
  ModerationCaseResponse,
  ModerationCaseSummaryResponse,
  ModerationWorkflowStatus,
} from '../../../core/api/moderation-api.types';
import { ApiProblem } from '../../../core/api/api-problem';
import { ConnectivityStore } from '../../../core/pwa/connectivity.store';

export type ModerationQueueKind = 'cases' | 'reports';
export type ModerationQueueStatus =
  'idle' | 'loading' | 'loadingMore' | 'success' | 'empty' | 'offline' | 'error';

export interface ModerationQueueFilters {
  readonly kind: ModerationQueueKind;
  readonly status: ModerationWorkflowStatus | null;
  readonly targetKind: ContentReportTargetKind | null;
}

export interface ModerationQueueState {
  readonly status: ModerationQueueStatus;
  readonly kind: ModerationQueueKind;
  readonly cases: readonly ModerationCaseSummaryResponse[];
  readonly reports: readonly AdminContentReportResponse[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly errorCode: string | null;
}

export type ModerationDetailStatus = 'idle' | 'loading' | 'success' | 'offline' | 'error';

export interface ModerationDetailState {
  readonly status: ModerationDetailStatus;
  readonly value: ModerationCaseResponse | null;
  readonly errorCode: string | null;
}

export type ModerationActionStatus =
  'idle' | 'saving' | 'success' | 'offline' | 'conflict' | 'error';

export interface ModerationActionState {
  readonly status: ModerationActionStatus;
  readonly errorCode: string | null;
}

const defaultFilters: ModerationQueueFilters = {
  kind: 'cases',
  status: null,
  targetKind: null,
};

const emptyQueue = (): ModerationQueueState => ({
  status: 'idle',
  kind: 'cases',
  cases: [],
  reports: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
});

@Injectable()
export class ModerationStore {
  private readonly api = inject(ModerationApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly queueState = signal<ModerationQueueState>(emptyQueue());
  private readonly detailState = signal<ModerationDetailState>({
    status: 'idle',
    value: null,
    errorCode: null,
  });
  private readonly moderationActionState = signal<ModerationActionState>({
    status: 'idle',
    errorCode: null,
  });
  private filters = defaultFilters;
  private failedCursor: string | null = null;
  private queueVersion = 0;
  private detailVersion = 0;
  private queueSubscription: Subscription | null = null;
  private detailSubscription: Subscription | null = null;
  private pendingCaseId: string | null = null;

  readonly queue = this.queueState.asReadonly();
  readonly detail = this.detailState.asReadonly();
  readonly action = this.moderationActionState.asReadonly();

  loadQueue(filters: ModerationQueueFilters = this.filters, cursor: string | null = null): void {
    this.filters = { ...filters };
    this.failedCursor = cursor;
    this.queueSubscription?.unsubscribe();
    const version = ++this.queueVersion;
    const current = this.queueState();
    const continuing = cursor !== null && current.kind === filters.kind;
    const pending: ModerationQueueState = {
      status: continuing ? 'loadingMore' : 'loading',
      kind: filters.kind,
      cases: continuing ? current.cases : [],
      reports: continuing ? current.reports : [],
      nextCursor: continuing ? current.nextCursor : null,
      hasMore: continuing && current.hasMore,
      errorCode: null,
    };
    this.queueState.set(pending);

    if (!this.connectivity.isOnline()) {
      this.queueState.set({ ...pending, status: 'offline' });
      return;
    }

    this.queueSubscription =
      filters.kind === 'cases'
        ? this.api
            .getModerationCases({ status: filters.status, limit: 20, cursor })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (page) => {
                if (version !== this.queueVersion) return;
                const cases = mergeById(cursor === null ? [] : this.queueState().cases, page.items);
                this.failedCursor = null;
                this.queueState.set({
                  status: cases.length === 0 ? 'empty' : 'success',
                  kind: 'cases',
                  cases,
                  reports: [],
                  nextCursor: page.nextCursor,
                  hasMore: page.hasMore,
                  errorCode: null,
                });
              },
              error: (error: unknown) => {
                this.failQueue(error, version);
              },
            })
        : this.api
            .getAdminReports({
              status: filters.status,
              targetKind: filters.targetKind,
              limit: 20,
              cursor,
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (page) => {
                if (version !== this.queueVersion) return;
                const reports = mergeReports(
                  cursor === null ? [] : this.queueState().reports,
                  page.items,
                );
                this.failedCursor = null;
                this.queueState.set({
                  status: reports.length === 0 ? 'empty' : 'success',
                  kind: 'reports',
                  cases: [],
                  reports,
                  nextCursor: page.nextCursor,
                  hasMore: page.hasMore,
                  errorCode: null,
                });
              },
              error: (error: unknown) => {
                this.failQueue(error, version);
              },
            });
  }

  loadMore(): void {
    const state = this.queueState();
    if (
      !state.hasMore ||
      state.nextCursor === null ||
      state.status === 'loading' ||
      state.status === 'loadingMore'
    ) {
      return;
    }
    this.loadQueue(this.filters, state.nextCursor);
  }

  retryQueue(): void {
    this.loadQueue(this.filters, this.failedCursor);
  }

  loadCase(caseId: string): void {
    if (this.moderationActionState().status === 'saving') {
      this.pendingCaseId = caseId;
      return;
    }
    this.pendingCaseId = null;
    this.detailSubscription?.unsubscribe();
    const version = ++this.detailVersion;
    this.moderationActionState.set({ status: 'idle', errorCode: null });
    this.detailState.set({ status: 'loading', value: null, errorCode: null });
    if (!this.connectivity.isOnline()) {
      this.detailState.set({ status: 'offline', value: null, errorCode: null });
      return;
    }

    this.detailSubscription = this.api
      .getModerationCase(caseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (value) => {
          if (version !== this.detailVersion) return;
          this.detailState.set({ status: 'success', value, errorCode: null });
        },
        error: (error: unknown) => {
          if (version !== this.detailVersion) return;
          this.detailState.set({
            status: isOfflineProblem(error) ? 'offline' : 'error',
            value: null,
            errorCode: problemCode(error),
          });
        },
      });
  }

  applyAction(
    caseId: string,
    request: ModerationActionRequest,
    idempotencyKey: string,
    auditReason: string,
  ): void {
    const current = this.detailState();
    if (this.moderationActionState().status === 'saving' || current.value?.case.id !== caseId) {
      return;
    }
    if (!this.connectivity.isOnline()) {
      this.moderationActionState.set({ status: 'offline', errorCode: null });
      return;
    }

    this.moderationActionState.set({ status: 'saving', errorCode: null });
    this.api
      .applyModerationAction(caseId, request, idempotencyKey, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (value) => {
          const currentCase = this.detailState().value?.case.id;
          if (currentCase === caseId) {
            this.detailState.set({ status: 'success', value, errorCode: null });
            this.moderationActionState.set({ status: 'success', errorCode: null });
          } else {
            this.moderationActionState.set({ status: 'idle', errorCode: null });
          }
          const pendingCaseId = this.pendingCaseId;
          this.pendingCaseId = null;
          if (pendingCaseId !== null && pendingCaseId !== caseId) this.loadCase(pendingCaseId);
        },
        error: (error: unknown) => {
          if (this.detailState().value?.case.id === caseId) {
            this.moderationActionState.set({
              status: isModerationConflict(error)
                ? 'conflict'
                : isOfflineProblem(error)
                  ? 'offline'
                  : 'error',
              errorCode: problemCode(error),
            });
          } else {
            this.moderationActionState.set({ status: 'idle', errorCode: null });
          }
          const pendingCaseId = this.pendingCaseId;
          this.pendingCaseId = null;
          if (pendingCaseId !== null && pendingCaseId !== caseId) this.loadCase(pendingCaseId);
        },
      });
  }

  resetAction(): void {
    if (this.moderationActionState().status !== 'saving') {
      this.moderationActionState.set({ status: 'idle', errorCode: null });
    }
  }

  private failQueue(error: unknown, version: number): void {
    if (version !== this.queueVersion) return;
    if (problemCode(error) === 'CURSOR.INVALID') this.failedCursor = null;
    this.queueState.update((state) => ({
      ...state,
      status: isOfflineProblem(error) ? 'offline' : 'error',
      errorCode: problemCode(error),
    }));
  }
}

export const isModerationConflict = (error: unknown): error is ApiProblem =>
  error instanceof ApiProblem &&
  (error.status === 409 ||
    error.status === 412 ||
    [
      'MODERATION.CASE_NOT_OPEN',
      'MODERATION.CASE_NOT_IN_REVIEW',
      'MODERATION.CASE_CLOSED',
      'REPORT.NOT_OPEN',
      'REPORT.ALREADY_CLOSED',
    ].includes(error.code));

const isOfflineProblem = (error: unknown): boolean =>
  error instanceof ApiProblem && error.status === 0;

const problemCode = (error: unknown): string | null =>
  error instanceof ApiProblem ? error.code : null;

const mergeById = <T extends { readonly id: string }>(
  current: readonly T[],
  incoming: readonly T[],
): readonly T[] => {
  const ids = new Set(current.map((item) => item.id));
  const items = [...current];
  for (const item of incoming) {
    if (ids.has(item.id)) continue;
    ids.add(item.id);
    items.push(item);
  }
  return items;
};

const mergeReports = (
  current: readonly AdminContentReportResponse[],
  incoming: readonly AdminContentReportResponse[],
): readonly AdminContentReportResponse[] => {
  const ids = new Set(current.map((item) => item.report.id));
  const items = [...current];
  for (const item of incoming) {
    if (ids.has(item.report.id)) continue;
    ids.add(item.report.id);
    items.push(item);
  }
  return items;
};
