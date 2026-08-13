import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, map, of, Subject, startWith, switchMap, tap } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type { PublicCourseSummary, PublicLoadStatus } from '../../core/api/discovery-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { PublicPortfolioSettingsStore } from '../cms/public-portfolio-settings.store';

export interface FeaturedCoursesState {
  status: PublicLoadStatus;
  items: readonly PublicCourseSummary[];
  errorCode: string | null;
}

@Injectable()
export class FeaturedCoursesStore {
  private readonly api = inject(DiscoveryApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly portfolio = inject(PublicPortfolioSettingsStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reloadRequests = new Subject<void>();
  private readonly featuredState = signal<FeaturedCoursesState>({
    status: 'idle',
    items: [],
    errorCode: null,
  });

  readonly state = this.featuredState.asReadonly();

  constructor() {
    combineLatest([this.reloadRequests.pipe(startWith(undefined)), this.portfolio.settings$])
      .pipe(
        tap(() => {
          const current = this.featuredState();
          this.featuredState.set({
            ...current,
            status: current.items.length > 0 ? 'refreshing' : 'loading',
            errorCode: null,
          });
        }),
        switchMap(([, settings]) => {
          if (!this.connectivity.isOnline()) return of<FeaturedCoursesState>(failure('offline'));
          return this.api.getFeatured(settings.featuredCourseLimit).pipe(
            map(mapFeaturedState),
            catchError((error: unknown) => of(failure(errorStatus(error), error))),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.featuredState.set(state);
      });
  }

  retry(): void {
    this.reloadRequests.next();
  }
}

const mapFeaturedState = (items: readonly PublicCourseSummary[]): FeaturedCoursesState => ({
  status: items.length > 0 ? 'success' : 'empty',
  items,
  errorCode: null,
});

const failure = (status: 'error' | 'offline', error?: unknown): FeaturedCoursesState => ({
  status,
  items: [],
  errorCode: error instanceof ApiProblem ? error.code : null,
});

const errorStatus = (error: unknown): 'error' | 'offline' =>
  error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error';
