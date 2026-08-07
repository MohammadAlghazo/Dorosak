import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiProblem } from '../../core/api/api-problem';
import { InstructorApiClient } from '../../core/api/instructor-api.client';
import type { CourseSummary } from '../../core/api/phase6-api.types';

export type CourseListStatus =
  'idle' | 'loading' | 'empty' | 'success' | 'loadingMore' | 'offline' | 'error';

export interface InstructorCoursesState {
  status: CourseListStatus;
  items: readonly CourseSummary[];
  nextCursor: string | null;
  hasMore: boolean;
  errorCode: string | null;
}

const initialState: InstructorCoursesState = {
  status: 'idle',
  items: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
};

@Injectable()
export class InstructorCoursesStore {
  private readonly api = inject(InstructorApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly currentState = signal<InstructorCoursesState>(initialState);

  readonly state = this.currentState.asReadonly();

  load(): void {
    this.currentState.set({ ...initialState, status: 'loading' });
    this.api
      .getCourses()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          this.currentState.set({
            status: page.items.length === 0 ? 'empty' : 'success',
            items: page.items,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.currentState.set(failureState(error));
        },
      });
  }

  loadMore(): void {
    const current = this.currentState();
    if (!current.hasMore || current.nextCursor === null || current.status === 'loadingMore') return;
    this.currentState.set({ ...current, status: 'loadingMore', errorCode: null });
    this.api
      .getCourses(20, current.nextCursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          const known = new Set(this.currentState().items.map((course) => course.id));
          this.currentState.set({
            status: 'success',
            items: [
              ...this.currentState().items,
              ...page.items.filter((course) => !known.has(course.id)),
            ],
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.currentState.set({
            ...this.currentState(),
            status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
            errorCode: error instanceof ApiProblem ? error.code : null,
          });
        },
      });
  }
}

const failureState = (error: unknown): InstructorCoursesState => ({
  ...initialState,
  status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
  errorCode: error instanceof ApiProblem ? error.code : null,
});
