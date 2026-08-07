import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { ReplaySubject, Subject } from 'rxjs';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type {
  PublicSearchPage,
  PublicSearchSuggestion,
  SearchRequest,
} from '../../core/api/discovery-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { SearchPageStore } from './search-page.store';

describe('SearchPageStore', () => {
  const routeParams = new ReplaySubject<ReturnType<typeof convertToParamMap>>(1);
  const searchRequests = new Map<string, Subject<PublicSearchPage>>();
  const suggestionRequests = new Map<string, Subject<readonly PublicSearchSuggestion[]>>();
  const api = {
    search: vi.fn((request: SearchRequest) => {
      const response = new Subject<PublicSearchPage>();
      searchRequests.set(request.query, response);
      return response;
    }),
    getSuggestions: vi.fn((query: string) => {
      const response = new Subject<readonly PublicSearchSuggestion[]>();
      suggestionRequests.set(query, response);
      return response;
    }),
  };
  const router = { navigate: vi.fn().mockResolvedValue(true) };

  beforeEach(() => {
    vi.useFakeTimers();
    routeParams.next(convertToParamMap({ q: '' }));
    searchRequests.clear();
    suggestionRequests.clear();
    api.search.mockClear();
    api.getSuggestions.mockClear();
    router.navigate.mockClear();
    TestBed.configureTestingModule({
      providers: [
        SearchPageStore,
        { provide: DiscoveryApiClient, useValue: api },
        { provide: ActivatedRoute, useValue: { queryParamMap: routeParams.asObservable() } },
        { provide: Router, useValue: router },
        { provide: ConnectivityStore, useValue: { isOnline: () => true } },
      ],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('cancels stale result requests and exposes empty and success states', () => {
    const store = TestBed.inject(SearchPageStore);
    expect(store.results().status).toBe('loading');

    routeParams.next(convertToParamMap({ q: 'angular' }));
    const angularRequest = searchRequests.get('angular');
    expect(angularRequest?.observed).toBe(true);

    routeParams.next(convertToParamMap({ q: 'angular signals' }));
    expect(angularRequest?.observed).toBe(false);
    searchRequests.get('angular signals')?.next({
      items: [],
      nextCursor: null,
      hasMore: false,
      correction: null,
    });
    expect(store.results().status).toBe('empty');
  });

  it('debounces suggestions for 250ms, requires two characters, and cancels stale calls', async () => {
    const store = TestBed.inject(SearchPageStore);
    store.updateDraftQuery('a');
    await vi.advanceTimersByTimeAsync(250);
    expect(api.getSuggestions).not.toHaveBeenCalled();

    store.updateDraftQuery('an');
    await vi.advanceTimersByTimeAsync(249);
    expect(api.getSuggestions).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    const first = suggestionRequests.get('an');
    expect(first?.observed).toBe(true);

    store.updateDraftQuery('angular');
    await vi.advanceTimersByTimeAsync(250);
    expect(first?.observed).toBe(false);
    const suggestions = suggestionRequests.get('angular');
    suggestions?.next(
      Array.from({ length: 10 }, (_, index) => ({
        slug: `course-${String(index)}`,
        segments: [{ text: `Angular ${String(index)}`, matched: true }],
      })),
    );
    expect(store.suggestions().items).toHaveLength(8);
    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({ replaceUrl: true }));
  });
});
