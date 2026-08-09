import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { EngagementApiClient } from './engagement-api.client';
import { IdentityApiClient } from './identity-api.client';

describe('EngagementApiClient', () => {
  let client: EngagementApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(EngagementApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('creates one idempotent course review', async () => {
    const promise = firstValueFrom(client.createCourseReview('course-1', 5, 'Useful course'));
    const request = http.expectOne('courses/course-1/reviews');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.body).toEqual({ rating: 5, text: 'Useful course' });
    request.flush({ data: review });
    await expect(promise).resolves.toEqual(review);
  });
});

const review = {
  id: 'review-1',
  courseId: 'course-1',
  userId: 'user-1',
  authorName: 'Learner',
  rating: 5,
  text: 'Useful course',
  status: 'Published' as const,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
};
