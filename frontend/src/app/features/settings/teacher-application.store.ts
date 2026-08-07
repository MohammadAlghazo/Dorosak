import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiProblem } from '../../core/api/api-problem';
import { TeacherApplicationApiClient } from '../../core/api/teacher-application-api.client';
import type {
  TeacherApplication,
  TeacherApplicationRequest,
} from '../../core/api/phase6-api.types';

export type TeacherApplicationViewStatus =
  'idle' | 'loading' | 'empty' | 'success' | 'submitting' | 'withdrawing' | 'offline' | 'error';

export interface TeacherApplicationState {
  status: TeacherApplicationViewStatus;
  application: TeacherApplication | null;
  errorCode: string | null;
}

const initialState: TeacherApplicationState = {
  status: 'idle',
  application: null,
  errorCode: null,
};

@Injectable()
export class TeacherApplicationStore {
  private readonly api = inject(TeacherApplicationApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly currentState = signal<TeacherApplicationState>(initialState);

  readonly state = this.currentState.asReadonly();

  load(): void {
    this.currentState.set({ ...this.currentState(), status: 'loading', errorCode: null });
    this.api
      .getCurrent()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.currentState.set({ status: 'success', application, errorCode: null });
        },
        error: (error: unknown) => {
          if (error instanceof ApiProblem && error.status === 404) {
            this.currentState.set({ ...initialState, status: 'empty' });
            return;
          }
          this.currentState.set(failureState(error));
        },
      });
  }

  submit(request: TeacherApplicationRequest): void {
    if (this.currentState().status === 'submitting') return;
    this.currentState.set({ ...this.currentState(), status: 'submitting', errorCode: null });
    this.api
      .submit(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.currentState.set({ status: 'success', application, errorCode: null });
        },
        error: (error: unknown) => {
          this.currentState.set(failureState(error, this.currentState().application));
        },
      });
  }

  withdraw(): void {
    if (this.currentState().status === 'withdrawing') return;
    this.currentState.set({ ...this.currentState(), status: 'withdrawing', errorCode: null });
    this.api
      .withdraw()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.currentState.set({ status: 'success', application, errorCode: null });
        },
        error: (error: unknown) => {
          this.currentState.set(failureState(error, this.currentState().application));
        },
      });
  }
}

const failureState = (
  error: unknown,
  application: TeacherApplication | null = null,
): TeacherApplicationState => ({
  status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
  application,
  errorCode: error instanceof ApiProblem ? error.code : null,
});
