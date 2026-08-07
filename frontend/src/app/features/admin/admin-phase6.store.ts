import { DestroyRef, inject, Injectable, signal, type WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { AdminPhase6ApiClient } from '../../core/api/admin-phase6-api.client';
import type {
  Category,
  CategoryUpsertRequest,
  CursorPage,
  PublicationReview,
  Tag,
  TagUpsertRequest,
  TeacherApplication,
} from '../../core/api/phase6-api.types';

export type AdminLoadStatus =
  'idle' | 'loading' | 'empty' | 'success' | 'saving' | 'offline' | 'error';

export interface AdminListState<T> {
  status: AdminLoadStatus;
  items: readonly T[];
  nextCursor: string | null;
  hasMore: boolean;
  errorCode: string | null;
}

export interface TaxonomyState {
  status: AdminLoadStatus;
  categories: readonly Category[];
  tags: readonly Tag[];
  errorCode: string | null;
}

const emptyList = <T>(): AdminListState<T> => ({
  status: 'idle',
  items: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
});

@Injectable()
export class AdminPhase6Store {
  private readonly api = inject(AdminPhase6ApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly teacherApplicationsState =
    signal<AdminListState<TeacherApplication>>(emptyList());
  private readonly publicationReviewsState = signal<AdminListState<PublicationReview>>(emptyList());
  private readonly taxonomyState = signal<TaxonomyState>({
    status: 'idle',
    categories: [],
    tags: [],
    errorCode: null,
  });
  private auditReason = '';

  readonly teacherApplications = this.teacherApplicationsState.asReadonly();
  readonly publicationReviews = this.publicationReviewsState.asReadonly();
  readonly taxonomy = this.taxonomyState.asReadonly();

  loadTeacherApplications(auditReason: string, cursor: string | null = null): void {
    this.auditReason = auditReason.trim();
    const current = this.teacherApplicationsState();
    this.teacherApplicationsState.set({
      ...current,
      status: 'loading',
      errorCode: null,
      ...(cursor === null ? { items: [] } : {}),
    });
    this.api
      .getTeacherApplications(this.auditReason, 20, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          this.setList(this.teacherApplicationsState, page, cursor);
        },
        error: (error: unknown) => {
          this.teacherApplicationsState.set(failureList(error, current));
        },
      });
  }

  reviewTeacherApplication(
    applicationId: string,
    decision: 'start' | 'approve' | 'reject',
    reason: string | null,
  ): void {
    const current = this.teacherApplicationsState();
    this.teacherApplicationsState.set({ ...current, status: 'saving', errorCode: null });
    this.api
      .reviewTeacherApplication(applicationId, decision, reason, this.auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.teacherApplicationsState.set({
            ...this.teacherApplicationsState(),
            status: 'success',
            items: this.teacherApplicationsState().items.map((item) =>
              item.id === updated.id ? updated : item,
            ),
          });
        },
        error: (error: unknown) => {
          this.teacherApplicationsState.set(failureList(error, current));
        },
      });
  }

  loadPublicationReviews(cursor: string | null = null): void {
    const current = this.publicationReviewsState();
    this.publicationReviewsState.set({
      ...current,
      status: 'loading',
      errorCode: null,
      ...(cursor === null ? { items: [] } : {}),
    });
    this.api
      .getPublicationReviews(20, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          this.setList(this.publicationReviewsState, page, cursor);
        },
        error: (error: unknown) => {
          this.publicationReviewsState.set(failureList(error, current));
        },
      });
  }

  reviewPublication(
    reviewId: string,
    decision: 'changesRequested' | 'approve',
    reason: string | null,
  ): void {
    const current = this.publicationReviewsState();
    this.publicationReviewsState.set({ ...current, status: 'saving', errorCode: null });
    this.api
      .reviewPublication(reviewId, decision, reason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.publicationReviewsState.set({
            ...this.publicationReviewsState(),
            status: 'success',
            items: this.publicationReviewsState().items.map((item) =>
              item.id === updated.id ? updated : item,
            ),
          });
        },
        error: (error: unknown) => {
          this.publicationReviewsState.set(failureList(error, current));
        },
      });
  }

  loadTaxonomy(): void {
    this.taxonomyState.set({ ...this.taxonomyState(), status: 'loading', errorCode: null });
    forkJoin({ categories: this.api.getCategories(), tags: this.api.getTags() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ categories, tags }) => {
          this.taxonomyState.set({
            status: categories.items.length + tags.items.length === 0 ? 'empty' : 'success',
            categories: categories.items,
            tags: tags.items,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.taxonomyState.set({
            ...this.taxonomyState(),
            status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
            errorCode: error instanceof ApiProblem ? error.code : null,
          });
        },
      });
  }

  saveCategory(id: string | null, request: CategoryUpsertRequest): void {
    this.taxonomyState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    const operation =
      id === null ? this.api.createCategory(request) : this.api.updateCategory(id, request);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadTaxonomy();
      },
      error: (error: unknown) => {
        this.taxonomyState.update((state) => ({
          ...state,
          status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
          errorCode: error instanceof ApiProblem ? error.code : null,
        }));
      },
    });
  }

  saveTag(id: string | null, request: TagUpsertRequest): void {
    this.taxonomyState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    const operation = id === null ? this.api.createTag(request) : this.api.updateTag(id, request);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadTaxonomy();
      },
      error: (error: unknown) => {
        this.taxonomyState.update((state) => ({
          ...state,
          status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
          errorCode: error instanceof ApiProblem ? error.code : null,
        }));
      },
    });
  }

  private setList<T extends { id: string }>(
    state: WritableSignal<AdminListState<T>>,
    page: CursorPage<T>,
    cursor: string | null,
  ): void {
    const old = state();
    const known = new Set(old.items.map((item) => item.id));
    const items =
      cursor === null
        ? page.items
        : [...old.items, ...page.items.filter((item) => !known.has(item.id))];
    state.set({
      status: items.length === 0 ? 'empty' : 'success',
      items,
      nextCursor: page.nextCursor,
      hasMore: page.hasMore,
      errorCode: null,
    });
  }
}

const failureList = <T>(error: unknown, current: AdminListState<T>): AdminListState<T> => ({
  ...current,
  status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
  errorCode: error instanceof ApiProblem ? error.code : null,
});
