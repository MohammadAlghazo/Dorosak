import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import {
  catchError,
  combineLatest,
  distinctUntilChanged,
  forkJoin,
  map,
  of,
  startWith,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type {
  CatalogFilters,
  PublicCategory,
  PublicCourseSummary,
  PublicLoadStatus,
  PublicTaxonomyTerm,
} from '../../core/api/discovery-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import {
  catalogFilterParams,
  defaultCatalogFilters,
  parseCatalogFilters,
  sameCatalogFilters,
} from './catalog-query';

export interface CatalogPageState {
  status: PublicLoadStatus;
  items: readonly PublicCourseSummary[];
  nextCursor: string | null;
  hasMore: boolean;
  errorCode: string | null;
  traceId: string | null;
}

interface TaxonomyState {
  status: PublicLoadStatus;
  categories: readonly PublicCategory[];
  tags: readonly PublicTaxonomyTerm[];
}

const initialCatalogState: CatalogPageState = {
  status: 'idle',
  items: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
  traceId: null,
};

@Injectable()
export class CatalogPageStore {
  private readonly api = inject(DiscoveryApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reloadRequests = new Subject<number>();
  private readonly taxonomyReloadRequests = new Subject<number>();
  private readonly pageState = signal<CatalogPageState>(initialCatalogState);
  private readonly taxonomyState = signal<TaxonomyState>({
    status: 'idle',
    categories: [],
    tags: [],
  });
  private readonly activeFilters = signal<CatalogFilters>(defaultCatalogFilters);
  private reloadVersion = 0;
  private taxonomyReloadVersion = 0;
  private moreRequest: Subscription | null = null;

  readonly state = this.pageState.asReadonly();
  readonly filters = this.activeFilters.asReadonly();
  readonly categories = computed(() => this.taxonomyState().categories);
  readonly tags = computed(() => this.taxonomyState().tags);
  readonly taxonomyStatus = computed(() => this.taxonomyState().status);
  readonly loadingMore = signal(false);
  readonly loadMoreFailed = signal(false);
  readonly activeFilterCount = computed(() => {
    const filters = this.activeFilters();
    return [
      filters.category,
      filters.tag,
      filters.language,
      filters.level,
      filters.price,
      filters.duration,
      filters.instructor,
    ].filter(Boolean).length;
  });

  constructor() {
    const filters = this.route.queryParamMap.pipe(
      map(parseCatalogFilters),
      distinctUntilChanged(sameCatalogFilters),
    );
    combineLatest([filters, this.reloadRequests.pipe(startWith(0))])
      .pipe(
        tap(([nextFilters]) => {
          this.moreRequest?.unsubscribe();
          this.loadingMore.set(false);
          this.loadMoreFailed.set(false);
          this.activeFilters.set(nextFilters);
          const previous = this.pageState();
          this.pageState.set({
            ...previous,
            status: previous.items.length > 0 ? 'refreshing' : 'loading',
            errorCode: null,
            traceId: null,
          });
        }),
        switchMap(([nextFilters]) => {
          if (!this.connectivity.isOnline()) return of(failedCatalogState('offline'));
          return this.api.getCourses({ ...nextFilters, cursor: null, limit: 24 }).pipe(
            map((page): CatalogPageState => ({
              status: page.items.length === 0 ? 'empty' : 'success',
              items: page.items,
              nextCursor: page.nextCursor,
              hasMore: page.hasMore,
              errorCode: null,
              traceId: null,
            })),
            catchError((error: unknown) => of(failedCatalogState(errorStatus(error), error))),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.pageState.set(state);
      });

    this.taxonomyReloadRequests
      .pipe(
        startWith(0),
        tap(() => {
          const current = this.taxonomyState();
          this.taxonomyState.set({
            ...current,
            status: current.categories.length ? 'refreshing' : 'loading',
          });
        }),
        switchMap(() => {
          if (!this.connectivity.isOnline()) {
            return of<TaxonomyState>({ status: 'offline', categories: [], tags: [] });
          }
          return forkJoin({ categories: this.api.getCategories(), tags: this.api.getTags() }).pipe(
            map(({ categories, tags }): TaxonomyState => ({
              status: 'success',
              categories,
              tags,
            })),
            catchError(() => of<TaxonomyState>({ status: 'error', categories: [], tags: [] })),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.taxonomyState.set(state);
      });
  }

  setFilters(filters: CatalogFilters): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: catalogFilterParams(filters),
    });
  }

  clearFilters(): void {
    this.setFilters(defaultCatalogFilters);
  }

  retry(): void {
    this.reloadRequests.next(++this.reloadVersion);
  }

  retryTaxonomy(): void {
    this.taxonomyReloadRequests.next(++this.taxonomyReloadVersion);
  }

  loadMore(): void {
    const current = this.pageState();
    if (!current.hasMore || !current.nextCursor || this.loadingMore()) return;
    if (!this.connectivity.isOnline()) {
      this.loadMoreFailed.set(true);
      return;
    }

    this.loadingMore.set(true);
    this.loadMoreFailed.set(false);
    this.moreRequest = this.api
      .getCourses({ ...this.activeFilters(), cursor: current.nextCursor, limit: 24 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          const latest = this.pageState();
          const knownIds = new Set(latest.items.map((item) => item.releaseId));
          this.pageState.set({
            ...latest,
            status: 'success',
            items: [...latest.items, ...page.items.filter((item) => !knownIds.has(item.releaseId))],
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
          });
          this.loadingMore.set(false);
        },
        error: () => {
          this.loadingMore.set(false);
          this.loadMoreFailed.set(true);
        },
      });
  }
}

const failedCatalogState = (status: 'error' | 'offline', error?: unknown): CatalogPageState => ({
  ...initialCatalogState,
  status,
  errorCode: error instanceof ApiProblem ? error.code : null,
  traceId: error instanceof ApiProblem ? error.traceId : null,
});

const errorStatus = (error: unknown): 'error' | 'offline' =>
  error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error';
