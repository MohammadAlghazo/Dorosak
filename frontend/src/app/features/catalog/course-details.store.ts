import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import {
  catchError,
  distinctUntilChanged,
  map,
  of,
  Subject,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type { PublicCourseDetail, PublicLoadStatus } from '../../core/api/discovery-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';

type CourseDetailStatus = PublicLoadStatus | 'notFound';

export interface CourseDetailState {
  status: CourseDetailStatus;
  course: PublicCourseDetail | null;
  errorCode: string | null;
  traceId: string | null;
}

const initialState: CourseDetailState = {
  status: 'idle',
  course: null,
  errorCode: null,
  traceId: null,
};

@Injectable()
export class CourseDetailsStore {
  private readonly api = inject(DiscoveryApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly locale = inject(LocaleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reloadRequests = new Subject<number>();
  private readonly detailState = signal<CourseDetailState>(initialState);
  private reloadVersion = 0;

  readonly state = this.detailState.asReadonly();

  constructor() {
    const slugs = this.route.paramMap.pipe(
      map((params) => params.get('slug')?.trim() ?? ''),
      distinctUntilChanged(),
    );
    slugs
      .pipe(
        switchMap((slug) =>
          this.reloadRequests.pipe(
            startWith(0),
            map(() => slug),
          ),
        ),
        tap(() => {
          this.detailState.set({ ...initialState, status: 'loading' });
        }),
        switchMap((slug) => {
          if (!slug) return of<CourseDetailState>({ ...initialState, status: 'notFound' });
          if (!this.connectivity.isOnline()) {
            return of<CourseDetailState>({ ...initialState, status: 'offline' });
          }
          return this.api.getCourse(slug).pipe(
            map((course): CourseDetailState => {
              if (!hasRelease(course) || course.locale !== this.locale.locale()) {
                return { ...initialState, status: 'notFound' };
              }
              if (course.slug !== slug) {
                void this.router.navigate(['..', course.slug], {
                  relativeTo: this.route,
                  replaceUrl: true,
                });
              }
              return { status: 'success', course, errorCode: null, traceId: null };
            }),
            catchError((error: unknown) => of(detailFailure(error))),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.detailState.set(state);
      });
  }

  retry(): void {
    this.reloadRequests.next(++this.reloadVersion);
  }
}

const detailFailure = (error: unknown): CourseDetailState => {
  if (error instanceof ApiProblem && error.status === 404) {
    return { ...initialState, status: 'notFound' };
  }
  return {
    ...initialState,
    status: error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error',
    errorCode: error instanceof ApiProblem ? error.code : null,
    traceId: error instanceof ApiProblem ? error.traceId : null,
  };
};

const hasRelease = (course: PublicCourseDetail): boolean => {
  const releaseId: unknown = course.releaseId;
  return typeof releaseId === 'string' && releaseId.trim().length > 0;
};
