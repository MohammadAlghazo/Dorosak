import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import {
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type {
  PublicLoadStatus,
  PublicSearchResult,
  PublicSearchSuggestion,
} from '../../core/api/discovery-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { defaultCatalogFilters } from '../catalog/catalog-query';

export interface SearchResultsState {
  status: PublicLoadStatus;
  items: readonly PublicSearchResult[];
  correction: string | null;
  errorCode: string | null;
  traceId: string | null;
}

interface SuggestionsState {
  status: PublicLoadStatus;
  items: readonly PublicSearchSuggestion[];
}

const initialResults: SearchResultsState = {
  status: 'idle',
  items: [],
  correction: null,
  errorCode: null,
  traceId: null,
};

@Injectable()
export class SearchPageStore {
  private readonly api = inject(DiscoveryApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly suggestionRequests = new Subject<string>();
  private readonly reloadRequests = new Subject<void>();
  private readonly activeQuery = signal('');
  private readonly resultsState = signal<SearchResultsState>(initialResults);
  private readonly suggestionsState = signal<SuggestionsState>({ status: 'idle', items: [] });

  readonly query = this.activeQuery.asReadonly();
  readonly results = this.resultsState.asReadonly();
  readonly suggestions = this.suggestionsState.asReadonly();

  constructor() {
    const queries = this.route.queryParamMap
      .pipe(
        map((params) => safeQuery(params.get('q'))),
        distinctUntilChanged(),
      );
    combineLatest([queries, this.reloadRequests.pipe(startWith(undefined))])
      .pipe(
        map(([query]) => query),
        tap((query) => {
          this.activeQuery.set(query);
          const current = this.resultsState();
          this.resultsState.set({
            ...current,
            status: current.items.length > 0 ? 'refreshing' : 'loading',
            correction: null,
            errorCode: null,
            traceId: null,
          });
        }),
        switchMap((query) => {
          if (!this.connectivity.isOnline()) return of(searchFailure('offline'));
          return this.api
            .search({
              ...defaultCatalogFilters,
              query,
              sort: query ? 'relevance' : 'newest',
              cursor: null,
              limit: 20,
            })
            .pipe(
              map(
                (page): SearchResultsState => ({
                  status: page.items.length === 0 ? 'empty' : 'success',
                  items: page.items,
                  correction: page.correction,
                  errorCode: null,
                  traceId: null,
                }),
              ),
              catchError((error: unknown) => of(searchFailure(errorStatus(error), error))),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.resultsState.set(state);
      });

    this.suggestionRequests
      .pipe(
        map(safeQuery),
        debounceTime(250),
        distinctUntilChanged(),
        tap((query) => {
          this.navigateToQuery(query, true);
        }),
        switchMap((query) => {
          if (query.length < 2) {
            return of<SuggestionsState>({ status: 'idle', items: [] });
          }
          if (!this.connectivity.isOnline()) {
            return of<SuggestionsState>({ status: 'offline', items: [] });
          }
          this.suggestionsState.set({ status: 'loading', items: [] });
          return this.api.getSuggestions(query).pipe(
            map(
              (items): SuggestionsState => ({
                status: items.length > 0 ? 'success' : 'empty',
                items: items.slice(0, 8),
              }),
            ),
            catchError((error: unknown) =>
              of<SuggestionsState>({ status: errorStatus(error), items: [] }),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.suggestionsState.set(state);
      });
  }

  updateDraftQuery(query: string): void {
    this.suggestionRequests.next(query);
  }

  submitQuery(query: string): void {
    const normalized = safeQuery(query);
    this.navigateToQuery(normalized, false);
    if (normalized.length >= 2) this.suggestionRequests.next(normalized);
  }

  useCorrection(query: string): void {
    this.navigateToQuery(safeQuery(query), false);
  }

  retry(): void {
    this.reloadRequests.next();
  }

  private navigateToQuery(query: string, replaceUrl: boolean): void {
    if (query === this.activeQuery()) return;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: query || null },
      replaceUrl,
    });
  }
}

export const safeQuery = (value: string | null): string => {
  const normalized = value?.trim().replace(/\s+/gu, ' ') ?? '';
  return normalized.slice(0, 200);
};

const searchFailure = (
  status: 'error' | 'offline',
  error?: unknown,
): SearchResultsState => ({
  ...initialResults,
  status,
  errorCode: error instanceof ApiProblem ? error.code : null,
  traceId: error instanceof ApiProblem ? error.traceId : null,
});

const errorStatus = (error: unknown): 'error' | 'offline' =>
  error instanceof ApiProblem && error.status === 0 ? 'offline' : 'error';
