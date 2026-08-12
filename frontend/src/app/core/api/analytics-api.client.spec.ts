import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_REQUEST, SKIP_AUTH } from './api-context';
import { AnalyticsApiClient } from './analytics-api.client';
import type { AdminAnalyticsOverview } from './analytics-api.types';

describe('AnalyticsApiClient', () => {
  it('reads the permission-gated overview through authenticated API context', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const controller = TestBed.inject(HttpTestingController);
    const response = firstValueFrom(TestBed.inject(AnalyticsApiClient).getAdminOverview());

    const request = controller.expectOne('admin/analytics/overview');
    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.context.get(SKIP_AUTH)).toBe(false);
    request.flush({ data: overview });

    await expect(response).resolves.toEqual(overview);
    controller.verify();
  });
});

export const overview: AdminAnalyticsOverview = {
  generatedAt: '2030-01-02T03:04:05Z',
  totalUsers: 42,
  activeUsers: 40,
  totalCourses: 8,
  publishedCourses: 5,
  totalEnrollments: 27,
  completedEnrollments: 11,
  completedDemoOrders: 16,
  activeDemoSubscriptions: 9,
  issuedCertificates: 11,
  activeCertificates: 10,
  pendingPublicationReviews: 2,
  openModerationCases: 3,
  pendingOutboxMessages: 4,
  retryingOutboxMessages: 1,
};
