import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { ReplaySubject, Subject } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { DiscoveryApiClient } from '../../core/api/discovery-api.client';
import type { PublicCourseDetail } from '../../core/api/discovery-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CourseDetailsStore } from './course-details.store';

describe('CourseDetailsStore', () => {
  let routeParams: ReplaySubject<ReturnType<typeof convertToParamMap>>;
  let detailRequests: Map<string, Subject<PublicCourseDetail>>;
  let router: { navigate: ReturnType<typeof vi.fn> };
  let route: { paramMap: ReturnType<typeof routeParams.asObservable> };

  beforeEach(() => {
    routeParams = new ReplaySubject(1);
    detailRequests = new Map();
    router = { navigate: vi.fn().mockResolvedValue(true) };
    route = { paramMap: routeParams.asObservable() };
    TestBed.configureTestingModule({
      providers: [
        CourseDetailsStore,
        {
          provide: DiscoveryApiClient,
          useValue: {
            getCourse: vi.fn((slug: string) => {
              const response = new Subject<PublicCourseDetail>();
              detailRequests.set(slug, response);
              return response;
            }),
          },
        },
        { provide: ActivatedRoute, useValue: route },
        { provide: Router, useValue: router },
        { provide: LocaleService, useValue: { locale: () => 'en' } },
        { provide: ConnectivityStore, useValue: { isOnline: () => true } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('maps API 404 to a release-safe not-found state', () => {
    routeParams.next(convertToParamMap({ slug: 'missing-course' }));
    const store = TestBed.inject(CourseDetailsStore);
    detailRequests
      .get('missing-course')
      ?.error(new ApiProblem(404, 'CATALOG.COURSE_NOT_FOUND', null, null, null, {}, 'Missing'));
    expect(store.state()).toMatchObject({ status: 'notFound', course: null });
  });

  it('replaces a historical route with the current localized slug', () => {
    routeParams.next(convertToParamMap({ slug: 'old-slug' }));
    const store = TestBed.inject(CourseDetailsStore);
    detailRequests.get('old-slug')?.next(courseDetail());

    expect(store.state().status).toBe('success');
    expect(router.navigate).toHaveBeenCalledWith(['..', 'current-slug'], {
      relativeTo: route,
      replaceUrl: true,
    });
  });
});

const courseDetail = (): PublicCourseDetail => ({
  courseId: 'course-1',
  releaseId: 'release-1',
  locale: 'en',
  defaultLocale: 'en',
  slug: 'current-slug',
  title: 'Current course',
  summary: 'Summary',
  description: 'Description',
  language: 'en',
  level: 'beginner',
  durationMinutes: 60,
  instructors: [],
  categories: [],
  tags: [],
  price: null,
  learningOutcomes: [],
  localizations: [
    { locale: 'en', slug: 'current-slug' },
    { locale: 'ar', slug: 'current-slug-ar' },
  ],
});
