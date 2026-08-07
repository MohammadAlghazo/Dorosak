import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiProblem } from '../../core/api/api-problem';
import { InstructorApiClient } from '../../core/api/instructor-api.client';
import type {
  CourseDetails,
  CourseMetadataRequest,
  Curriculum,
  PublicationStatus,
  SectionInput,
} from '../../core/api/phase6-api.types';

export type EditorStatus =
  'idle' | 'loading' | 'success' | 'saving' | 'conflict' | 'offline' | 'error';

export interface EditorState<T> {
  status: EditorStatus;
  value: T | null;
  etag: string | null;
  conflictEtag: string | null;
  errorCode: string | null;
}

const emptyState = <T>(): EditorState<T> => ({
  status: 'idle',
  value: null,
  etag: null,
  conflictEtag: null,
  errorCode: null,
});

@Injectable()
export class CourseEditorStore {
  private readonly api = inject(InstructorApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly courseState = signal<EditorState<CourseDetails>>(emptyState());
  private readonly curriculumState = signal<EditorState<Curriculum>>(emptyState());
  private readonly publicationState = signal<EditorState<PublicationStatus>>(emptyState());
  private pendingMetadata: { courseId: string; request: CourseMetadataRequest } | undefined;
  private pendingCurriculum: { courseId: string; sections: readonly SectionInput[] } | undefined;

  readonly course = this.courseState.asReadonly();
  readonly curriculum = this.curriculumState.asReadonly();
  readonly publication = this.publicationState.asReadonly();
  readonly hasConflict = computed(
    () => this.courseState().status === 'conflict' || this.curriculumState().status === 'conflict',
  );

  loadCourse(courseId: string): void {
    this.courseState.set({ ...this.courseState(), status: 'loading', errorCode: null });
    this.api
      .getCourse(courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.courseState.set(successState(result.value, result.etag));
        },
        error: (error: unknown) => {
          this.courseState.set(failureState(error, this.courseState()));
        },
      });
  }

  saveMetadata(courseId: string, request: CourseMetadataRequest): void {
    const current = this.courseState();
    if (current.status === 'saving') {
      this.pendingMetadata = { courseId, request };
      return;
    }
    if (current.etag === null || current.status === 'conflict') return;
    this.courseState.set({ ...current, status: 'saving', errorCode: null });
    this.api
      .updateCourseMetadata(courseId, request, current.etag)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const pending = this.pendingMetadata;
          this.pendingMetadata = undefined;
          if (pending) {
            this.courseState.set({
              ...current,
              status: 'success',
              etag: result.etag,
              conflictEtag: null,
              errorCode: null,
            });
            this.saveMetadata(pending.courseId, pending.request);
          } else {
            this.loadCourse(courseId);
          }
        },
        error: (error: unknown) => {
          this.pendingMetadata = undefined;
          this.courseState.set(failureState(error, current));
        },
      });
  }

  loadCurriculum(courseId: string): void {
    this.curriculumState.set({ ...this.curriculumState(), status: 'loading', errorCode: null });
    this.api
      .getCurriculum(courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.curriculumState.set(successState(result.value, result.etag));
        },
        error: (error: unknown) => {
          this.curriculumState.set(failureState(error, this.curriculumState()));
        },
      });
  }

  saveCurriculum(courseId: string, sections: readonly SectionInput[]): void {
    const current = this.curriculumState();
    if (current.status === 'saving') {
      this.pendingCurriculum = { courseId, sections };
      return;
    }
    if (current.etag === null || current.status === 'conflict') return;
    this.curriculumState.set({ ...current, status: 'saving', errorCode: null });
    this.api
      .updateCurriculum(courseId, sections, current.etag)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const pending = this.pendingCurriculum;
          this.pendingCurriculum = undefined;
          if (pending) {
            this.curriculumState.set({
              ...current,
              status: 'success',
              etag: result.etag,
              conflictEtag: null,
              errorCode: null,
            });
            this.saveCurriculum(pending.courseId, pending.sections);
          } else {
            this.loadCurriculum(courseId);
          }
        },
        error: (error: unknown) => {
          this.pendingCurriculum = undefined;
          this.curriculumState.set(failureState(error, current));
        },
      });
  }

  loadPublication(courseId: string): void {
    this.publicationState.set({
      ...this.publicationState(),
      status: 'loading',
      errorCode: null,
    });
    this.api
      .getPublicationStatus(courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.publicationState.set(successState(result.value, result.etag));
        },
        error: (error: unknown) => {
          this.publicationState.set(failureState(error, this.publicationState()));
        },
      });
  }

  requestPublication(courseId: string): void {
    this.mutatePublication(courseId, 'request');
  }

  withdrawPublication(courseId: string): void {
    this.mutatePublication(courseId, 'withdraw');
  }

  private mutatePublication(courseId: string, action: 'request' | 'withdraw'): void {
    const current = this.publicationState();
    if (current.status === 'saving') return;
    this.publicationState.set({ ...current, status: 'saving', errorCode: null });
    const request =
      action === 'request'
        ? this.api.requestPublication(courseId)
        : this.api.withdrawPublication(courseId);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.publicationState.set(successState(result.value, result.etag));
      },
      error: (error: unknown) => {
        this.publicationState.set(failureState(error, current));
      },
    });
  }
}

export const isVersionConflict = (error: unknown): error is ApiProblem =>
  error instanceof ApiProblem && error.status === 412 && error.code === 'COURSE.VERSION_CONFLICT';

const successState = <T>(value: T, etag: string): EditorState<T> => ({
  status: 'success',
  value,
  etag,
  conflictEtag: null,
  errorCode: null,
});

const failureState = <T>(error: unknown, current: EditorState<T>): EditorState<T> => {
  if (isVersionConflict(error)) {
    return {
      ...current,
      status: 'conflict',
      conflictEtag: error.etag,
      errorCode: error.code,
    };
  }
  return {
    ...current,
    status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
    conflictEtag: null,
    errorCode: error instanceof ApiProblem ? error.code : null,
  };
};
