import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { TeacherApplicationApiClient } from '../../core/api/teacher-application-api.client';
import { TeacherApplicationStore } from './teacher-application.store';

describe('TeacherApplicationStore', () => {
  it('treats the current-application 404 as an empty submission state', () => {
    TestBed.configureTestingModule({
      providers: [
        TeacherApplicationStore,
        {
          provide: TeacherApplicationApiClient,
          useValue: {
            getCurrent: () =>
              throwError(
                () =>
                  new ApiProblem(
                    404,
                    'TEACHER_APPLICATION.NOT_FOUND',
                    null,
                    null,
                    null,
                    {},
                    'Not found',
                  ),
              ),
          },
        },
      ],
    });

    const store = TestBed.inject(TeacherApplicationStore);
    store.load();
    expect(store.state()).toMatchObject({ status: 'empty', application: null });
  });

  it('replaces the empty state with the submitted application', () => {
    const submit = vi.fn(() => of(application));
    TestBed.configureTestingModule({
      providers: [
        TeacherApplicationStore,
        {
          provide: TeacherApplicationApiClient,
          useValue: { getCurrent: () => of(application), submit },
        },
      ],
    });

    const store = TestBed.inject(TeacherApplicationStore);
    store.submit({
      headline: 'Instructor',
      biography: 'Biography',
      expertise: 'Databases',
      motivation: 'Teach clearly',
    });
    expect(submit).toHaveBeenCalledOnce();
    expect(store.state()).toMatchObject({ status: 'success', application });
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
