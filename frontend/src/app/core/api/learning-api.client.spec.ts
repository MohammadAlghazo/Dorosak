import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { IdentityApiClient } from './identity-api.client';
import { LearningApiClient } from './learning-api.client';
import type { LearningManifest } from './learning-api.types';

describe('LearningApiClient', () => {
  let client: LearningApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(LearningApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('enrolls idempotently and reads a release-pinned manifest', async () => {
    const enrollPromise = firstValueFrom(client.enroll('course-1'));
    const enroll = http.expectOne('courses/course-1/enroll');
    expect(enroll.request.method).toBe('POST');
    expect(enroll.request.headers.get('Idempotency-Key')).toBeTruthy();
    enroll.flush({ data: enrollment });
    await expect(enrollPromise).resolves.toEqual(enrollment);

    const manifestPromise = firstValueFrom(client.getManifest('enrollment-1'));
    const manifestRequest = http.expectOne('learning/enrollments/enrollment-1/manifest');
    expect(manifestRequest.request.method).toBe('GET');
    manifestRequest.flush({ data: manifest });
    await expect(manifestPromise).resolves.toEqual(manifest);
  });

  it('sends monotonic progress and never asks the GET retry interceptor to retry mutations', async () => {
    const progressPromise = firstValueFrom(
      client.updateProgress('enrollment-1', 'lesson-1', {
        clientCommandId: 'command-1',
        sequence: 7,
        positionSeconds: 90,
        watchedIntervals: [{ startSeconds: 0, endSeconds: 90 }],
        completionIntent: true,
      }),
    );
    const progress = http.expectOne('learning/enrollments/enrollment-1/lessons/lesson-1/progress');
    expect(progress.request.method).toBe('PUT');
    expect(progress.request.body).toMatchObject({ sequence: 7, completionIntent: true });
    progress.flush({
      data: {
        enrollmentId: 'enrollment-1',
        lessonId: 'lesson-1',
        lastSequence: 7,
        positionSeconds: 90,
        isCompleted: true,
        completedAt: '2030-01-01T00:00:00Z',
        applied: true,
      },
    });
    await expect(progressPromise).resolves.toMatchObject({ isCompleted: true, applied: true });
  });

  it('uses idempotency keys for quiz and assignment submissions', async () => {
    const quizPromise = firstValueFrom(
      client.submitQuiz('enrollment-1', 'quiz-1', 'attempt-1', [
        { questionId: 'question-1', textAnswer: null, selectedOptionIds: ['option-1'] },
      ]),
    );
    const quiz = http.expectOne(
      'learning/enrollments/enrollment-1/quizzes/quiz-1/attempts/attempt-1/submit',
    );
    expect(quiz.request.headers.get('Idempotency-Key')).toBeTruthy();
    quiz.flush({
      data: {
        id: 'attempt-1',
        enrollmentId: 'enrollment-1',
        quizVersionId: 'quiz-1',
        attemptNumber: 1,
        status: 'Graded',
        startedAt: '2030-01-01T00:00:00Z',
        expiresAt: null,
        submittedAt: '2030-01-01T00:01:00Z',
        score: 100,
        passed: true,
        questions: [],
      },
    });
    await quizPromise;

    const assignmentPromise = firstValueFrom(
      client.submitAssignment('enrollment-1', 'assignment-1', 'My response'),
    );
    const assignment = http.expectOne(
      'learning/enrollments/enrollment-1/assignments/assignment-1/submissions',
    );
    expect(assignment.request.headers.get('Idempotency-Key')).toBeTruthy();
    assignment.flush({
      data: {
        id: 'submission-1',
        enrollmentId: 'enrollment-1',
        assignmentVersionId: 'assignment-1',
        submissionNumber: 1,
        text: 'My response',
        submittedAt: '2030-01-01T00:00:00Z',
        score: null,
        feedback: null,
        gradeRevisionNumber: 0,
      },
    });
    await assignmentPromise;
  });
});

const enrollment = {
  id: 'enrollment-1',
  courseId: 'course-1',
  releaseId: 'release-1',
  status: 'Active' as const,
  enrolledAt: '2030-01-01T00:00:00Z',
  title: 'Pinned learning',
  slug: 'pinned-learning',
};

const manifest: LearningManifest = {
  enrollmentId: 'enrollment-1',
  courseId: 'course-1',
  releaseId: 'release-1',
  status: 'Active',
  locale: 'en',
  title: 'Pinned learning',
  slug: 'pinned-learning',
  sections: [],
  nextLessonId: null,
};
