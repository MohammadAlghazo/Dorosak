import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, ReplaySubject, Subject } from 'rxjs';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type {
  CatalogCoursesRequest,
  CursorPage,
  PublicCourseSummary,
} from '../../core/api/discovery-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CatalogPageStore } from './catalog-page.store';

describe('CatalogPageStore', () => {
  let routeParams: ReplaySubject<ReturnType<typeof convertToParamMap>>;
  let pageRequests: Map<string, Subject<CursorPage<PublicCourseSummary>>>;
  let api: {
    getCourses: ReturnType<typeof vi.fn>;
    getCategories: ReturnType<typeof vi.fn>;
    getTags: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    routeParams = new ReplaySubject(1);
    pageRequests = new Map();
    api = {
      getCourses: vi.fn((request: CatalogCoursesRequest) => {
        const response = new Subject<CursorPage<PublicCourseSummary>>();
        pageRequests.set(request.cursor ?? 'first', response);
        return response;
      }),
      getCategories: vi.fn(() => of([])),
      getTags: vi.fn(() => of([])),
    };
    TestBed.configureTestingModule({
      providers: [
        CatalogPageStore,
        { provide: DiscoveryApiClient, useValue: api },
        { provide: ActivatedRoute, useValue: { queryParamMap: routeParams.asObservable() } },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: ConnectivityStore, useValue: { isOnline: () => true } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('passes the opaque cursor through and appends only new releases', () => {
    routeParams.next(convertToParamMap({ sort: 'newest' }));
    const store = TestBed.inject(CatalogPageStore);
    pageRequests.get('first')?.next({
      items: [course('release-1')],
      nextCursor: 'opaque.HMAC.value',
      hasMore: true,
    });
    expect(store.state().status).toBe('success');

    store.loadMore();
    expect(api.getCourses).toHaveBeenLastCalledWith(
      expect.objectContaining({ cursor: 'opaque.HMAC.value', limit: 24 }),
    );
    pageRequests.get('opaque.HMAC.value')?.next({
      items: [course('release-1'), course('release-2')],
      nextCursor: null,
      hasMore: false,
    });

    expect(store.state().items.map((item) => item.releaseId)).toEqual(['release-1', 'release-2']);
    expect(store.state().hasMore).toBe(false);
  });

  it('uses URL filters as the source of truth and exposes empty refresh state', () => {
    routeParams.next(convertToParamMap({ category: 'technology', sort: 'unsupported' }));
    const store = TestBed.inject(CatalogPageStore);
    expect(api.getCourses).toHaveBeenCalledWith(
      expect.objectContaining({ category: 'technology', sort: 'newest', cursor: null }),
    );
    pageRequests.get('first')?.next({ items: [], nextCursor: null, hasMore: false });
    expect(store.state().status).toBe('empty');
  });
});

const course = (releaseId: string): PublicCourseSummary => ({
  courseId: `course-${releaseId}`,
  releaseId,
  slug: `course-${releaseId}`,
  title: 'Released course',
  summary: 'Summary',
  language: 'en',
  level: 'beginner',
  durationMinutes: 60,
  instructors: [],
  categories: [],
  tags: [],
  price: null,
});
