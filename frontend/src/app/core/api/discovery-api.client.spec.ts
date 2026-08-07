import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { apiMetadataInterceptor, bearerInterceptor } from './api.interceptors';
import { DiscoveryApiClient } from './discovery-api.client';
import type { PublicCourseSummary } from './discovery-api.types';
import { isAnonymousPublicReadUrl } from './public-transfer-cache';
import { RuntimeConfigService } from './runtime-config.service';

describe('DiscoveryApiClient', () => {
  let client: DiscoveryApiClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiMetadataInterceptor, bearerInterceptor])),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: { apiUrl: (path: string) => `/api/v1/${path}` },
        },
      ],
    });
    client = TestBed.inject(DiscoveryApiClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
  });

  it('sends allow-listed public filters without credentials and drops non-release rows', async () => {
    TestBed.inject(SessionStore).establish(authSession());
    const resultPromise = firstValueFrom(
      client.getCourses({
        category: 'technology',
        tag: 'angular',
        language: 'en',
        level: 'intermediate',
        price: 'paid',
        duration: 'medium',
        instructor: 'instructor-1',
        sort: 'popular',
        cursor: 'opaque-cursor',
        limit: 24,
      }),
    );

    const request = controller.expectOne(
      (candidate) => candidate.url === '/api/v1/catalog/courses',
    );
    expect(request.request.credentials).toBe('omit');
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.params.keys().sort()).toEqual([
      'categoryCode',
      'cursor',
      'duration',
      'instructor',
      'language',
      'level',
      'limit',
      'price',
      'sort',
      'tag',
    ]);
    request.flush({
      data: {
        items: [course('release-1'), course('')],
        nextCursor: 'next-opaque-cursor',
        hasMore: true,
      },
    });

    await expect(resultPromise).resolves.toMatchObject({
      items: [{ releaseId: 'release-1' }],
      nextCursor: 'next-opaque-cursor',
      hasMore: true,
    });
  });

  it('enforces the public suggestion limit and keeps highlight segments as data', async () => {
    const resultPromise = firstValueFrom(client.getSuggestions('angular'));
    const request = controller.expectOne('/api/v1/search/suggestions?q=angular&limit=8');
    expect(request.request.credentials).toBe('omit');
    request.flush({
      data: Array.from({ length: 10 }, (_, index) => ({
        slug: `course-${String(index)}`,
        segments: [{ text: '<script>', matched: index === 0 }],
      })),
    });

    const suggestions = await resultPromise;
    expect(suggestions).toHaveLength(8);
    expect(suggestions[0]?.segments).toEqual([{ text: '<script>', matched: true }]);
  });

  it('adapts plain-text suggestion contracts into safe segments', async () => {
    const resultPromise = firstValueFrom(client.getSuggestions('gul'));
    const request = controller.expectOne('/api/v1/search/suggestions?q=gul&limit=8');
    request.flush({ data: ['Angular'] });

    await expect(resultPromise).resolves.toEqual([
      {
        slug: null,
        segments: [
          { text: 'An', matched: false },
          { text: 'gul', matched: true },
          { text: 'ar', matched: false },
        ],
      },
    ]);
  });

  it('maps paged taxonomy localizations for the active route locale', async () => {
    const resultPromise = firstValueFrom(client.getCategories());
    const request = controller.expectOne('/api/v1/catalog/categories?limit=100');
    request.flush({
      data: {
        items: [
          {
            id: 'category-1',
            code: 'technology',
            parentId: null,
            displayOrder: 1,
            localizations: [
              { locale: 'ar', name: 'التقنية' },
              { locale: 'en', name: 'Technology' },
            ],
          },
        ],
        nextCursor: null,
        hasMore: false,
      },
    });

    await expect(resultPromise).resolves.toEqual([
      { id: 'category-1', code: 'technology', name: 'التقنية', parentId: null },
    ]);
  });
});

describe('public transfer cache allow-list', () => {
  it('includes anonymous discovery reads and excludes personalized recommendations', () => {
    expect(isAnonymousPublicReadUrl('/api/v1/catalog/courses?sort=newest')).toBe(true);
    expect(isAnonymousPublicReadUrl('/api/v1/catalog/courses/angular-basics')).toBe(true);
    expect(isAnonymousPublicReadUrl('/api/v1/search/suggestions?q=an')).toBe(true);
    expect(isAnonymousPublicReadUrl('/api/v1/catalog/recommendations')).toBe(false);
    expect(isAnonymousPublicReadUrl('/api/v1/me/enrollments')).toBe(false);
  });
});

const course = (releaseId: string): PublicCourseSummary => ({
  courseId: 'course-1',
  releaseId,
  slug: 'angular-basics',
  title: 'Angular basics',
  summary: 'A released course',
  language: 'en',
  level: 'intermediate',
  durationMinutes: 180,
  instructors: [{ id: 'instructor-1', displayName: 'Teacher' }],
  categories: [{ id: 'category-1', code: 'technology', name: 'Technology' }],
  tags: [{ id: 'tag-1', code: 'angular', name: 'Angular' }],
  price: { type: 'paid', amount: '19.00', currency: 'USD' },
});

const authSession = () => ({
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  identity: {
    userId: 'user-1',
    sessionId: 'session-1',
    displayName: 'Learner',
    email: 'learner@example.test',
    emailVerified: true,
    mfaEnabled: false,
    authenticatedAt: '2029-12-31T23:50:00Z',
    recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
    authorizationVersion: 1,
    roles: ['Student'],
    permissions: [],
    authenticationMethods: ['pwd'],
  },
});
