import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { API_REQUEST, SKIP_AUTH } from './api-context';
import { AdminPhase6ApiClient } from './admin-phase6-api.client';
import { IdentityApiClient } from './identity-api.client';
import { InstructorApiClient } from './instructor-api.client';
import { TeacherApplicationApiClient } from './teacher-application-api.client';

describe('Phase 6 authenticated API clients', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: () => of(undefined) } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
  });

  it('uses the exact current teacher application routes and authenticated context', async () => {
    const client = TestBed.inject(TeacherApplicationApiClient);
    const currentPromise = firstValueFrom(client.getCurrent());
    const current = controller.expectOne('me/teacher-application');
    expect(current.request.method).toBe('GET');
    expect(current.request.context.get(API_REQUEST)).toBe(true);
    expect(current.request.context.get(SKIP_AUTH)).toBe(false);
    current.flush({ data: application });
    await expect(currentPromise).resolves.toEqual(application);

    const submitPromise = firstValueFrom(
      client.submit({
        headline: 'Instructor',
        biography: 'Biography',
        expertise: 'Databases',
        motivation: 'Teach clearly',
      }),
    );
    const submit = controller.expectOne('me/teacher-application');
    expect(submit.request.method).toBe('POST');
    expect(submit.request.body).toMatchObject({ expertise: 'Databases' });
    submit.flush({ data: application });
    await submitPromise;
  });

  it('captures response ETags and sends If-Match on curriculum autosaves', async () => {
    const client = TestBed.inject(InstructorApiClient);
    const getPromise = firstValueFrom(client.getCurriculum('course-1'));
    const get = controller.expectOne('instructor/courses/course-1/curriculum');
    get.flush({ data: { draftVersion: 4, sections: [] } }, { headers: { ETag: '"v4"' } });
    await expect(getPromise).resolves.toMatchObject({ etag: '"v4"' });

    const savePromise = firstValueFrom(
      client.updateCurriculum(
        'course-1',
        [
          {
            id: 'section-1',
            position: 0,
            title: 'Start',
            lessons: [
              {
                id: 'lesson-1',
                position: 0,
                title: 'Welcome',
                lessonType: 'Article',
                content: '',
              },
            ],
          },
        ],
        '"v4"',
      ),
    );
    const save = controller.expectOne('instructor/courses/course-1/curriculum');
    expect(save.request.method).toBe('PUT');
    expect(save.request.headers.get('If-Match')).toBe('"v4"');
    save.flush(
      { data: { courseId: 'course-1', status: 'Draft', draftVersion: 5 } },
      { headers: { ETag: '"v5"' } },
    );
    await expect(savePromise).resolves.toMatchObject({ etag: '"v5"' });
  });

  it('sends the required audit reason only to high-risk teacher review requests', async () => {
    const client = TestBed.inject(AdminPhase6ApiClient);
    const applicationsPromise = firstValueFrom(
      client.getTeacherApplications('Review teacher cohort'),
    );
    const applications = controller.expectOne(
      (request) => request.url === 'admin/teacher-applications',
    );
    expect(applications.request.headers.get('X-Audit-Reason')).toBe('Review teacher cohort');
    applications.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await applicationsPromise;

    const publicationsPromise = firstValueFrom(client.getPublicationReviews());
    const publications = controller.expectOne(
      (request) => request.url === 'admin/publication-reviews',
    );
    expect(publications.request.headers.has('X-Audit-Reason')).toBe(false);
    publications.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await publicationsPromise;

    const tagsPromise = firstValueFrom(client.getTags());
    const tags = controller.expectOne((request) => request.url === 'admin/catalog/tags');
    expect(tags.request.context.get(SKIP_AUTH)).toBe(false);
    tags.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await tagsPromise;
  });
});

const application = {
  id: 'application-1',
  userId: 'user-1',
  headline: 'Instructor',
  biography: 'Biography',
  expertise: 'Databases',
  motivation: 'Teach clearly',
  status: 'Pending' as const,
  reviewerReason: null,
  submittedAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
};
