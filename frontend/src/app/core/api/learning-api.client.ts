import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, switchMap } from 'rxjs';
import { IdentityApiClient } from './identity-api.client';
import type { ApiEnvelope } from './api-envelope';
import type { UploadSession } from './media-api.types';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';
import type {
  AssignmentVersion,
  AssignmentSubmission,
  CourseLearner,
  CreateAssignmentVersionRequest,
  CreateQuizVersionRequest,
  Enrollment,
  LearningLesson,
  LearningManifest,
  LearningNote,
  Progress,
  QuizAnswerInput,
  QuizAttempt,
  QuizVersion,
  UpdateProgressRequest,
} from './learning-api.types';

@Injectable({ providedIn: 'root' })
export class LearningApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  enroll(courseId: string): Observable<Enrollment> {
    return this.mutation<Enrollment>(
      'post',
      `courses/${encodeURIComponent(courseId)}/enroll`,
      null,
      idempotencyHeaders(globalThis.crypto.randomUUID()),
    );
  }

  getEnrollments(): Observable<readonly Enrollment[]> {
    return this.read<readonly Enrollment[]>('me/enrollments');
  }

  getManifest(enrollmentId: string): Observable<LearningManifest> {
    return this.read<LearningManifest>(
      `learning/enrollments/${encodeURIComponent(enrollmentId)}/manifest`,
    );
  }

  getLesson(enrollmentId: string, lessonId: string): Observable<LearningLesson> {
    return this.read<LearningLesson>(lessonPath(enrollmentId, lessonId));
  }

  updateProgress(
    enrollmentId: string,
    lessonId: string,
    request: UpdateProgressRequest,
  ): Observable<Progress> {
    return this.mutation<Progress>(
      'put',
      `${lessonPath(enrollmentId, lessonId)}/progress`,
      request,
    );
  }

  getNotes(enrollmentId: string, lessonId: string): Observable<readonly LearningNote[]> {
    return this.read<readonly LearningNote[]>(`${lessonPath(enrollmentId, lessonId)}/notes`);
  }

  createNote(enrollmentId: string, lessonId: string, text: string): Observable<LearningNote> {
    return this.mutation<LearningNote>('post', `${lessonPath(enrollmentId, lessonId)}/notes`, {
      text,
    });
  }

  updateNote(
    enrollmentId: string,
    lessonId: string,
    noteId: string,
    text: string,
  ): Observable<LearningNote> {
    return this.mutation<LearningNote>(
      'put',
      `${lessonPath(enrollmentId, lessonId)}/notes/${encodeURIComponent(noteId)}`,
      { text },
    );
  }

  deleteNote(enrollmentId: string, lessonId: string, noteId: string): Observable<boolean> {
    return this.mutation<{ completed: boolean }>(
      'delete',
      `${lessonPath(enrollmentId, lessonId)}/notes/${encodeURIComponent(noteId)}`,
      null,
    ).pipe(map((response) => response.completed));
  }

  addBookmark(enrollmentId: string, lessonId: string): Observable<void> {
    return this.mutation<unknown>(
      'put',
      `${lessonPath(enrollmentId, lessonId)}/bookmark`,
      null,
    ).pipe(map(() => undefined));
  }

  deleteBookmark(enrollmentId: string, lessonId: string): Observable<void> {
    return this.mutation<unknown>(
      'delete',
      `${lessonPath(enrollmentId, lessonId)}/bookmark`,
      null,
    ).pipe(map(() => undefined));
  }

  markRecentlyViewed(enrollmentId: string, lessonId: string): Observable<void> {
    return this.mutation<unknown>(
      'post',
      `${lessonPath(enrollmentId, lessonId)}/recently-viewed`,
      null,
    ).pipe(map(() => undefined));
  }

  startQuiz(enrollmentId: string, quizVersionId: string): Observable<QuizAttempt> {
    return this.mutation<QuizAttempt>(
      'post',
      `${quizPath(enrollmentId, quizVersionId)}/attempts`,
      null,
      idempotencyHeaders(globalThis.crypto.randomUUID()),
    );
  }

  submitQuiz(
    enrollmentId: string,
    quizVersionId: string,
    attemptId: string,
    answers: readonly QuizAnswerInput[],
  ): Observable<QuizAttempt> {
    return this.mutation<QuizAttempt>(
      'post',
      `${quizPath(enrollmentId, quizVersionId)}/attempts/${encodeURIComponent(attemptId)}/submit`,
      { answers },
      idempotencyHeaders(globalThis.crypto.randomUUID()),
    );
  }

  submitAssignment(
    enrollmentId: string,
    assignmentVersionId: string,
    text: string,
  ): Observable<AssignmentSubmission> {
    return this.mutation<AssignmentSubmission>(
      'post',
      `learning/enrollments/${encodeURIComponent(enrollmentId)}/assignments/${encodeURIComponent(assignmentVersionId)}/submissions`,
      { text },
      idempotencyHeaders(globalThis.crypto.randomUUID()),
    );
  }

  getAssignmentSubmission(
    enrollmentId: string,
    assignmentVersionId: string,
    submissionId: string,
  ): Observable<AssignmentSubmission> {
    return this.read<AssignmentSubmission>(
      `${assignmentPath(enrollmentId, assignmentVersionId)}/submissions/${encodeURIComponent(submissionId)}`,
    );
  }

  getCurrentAssignmentSubmission(
    enrollmentId: string,
    assignmentVersionId: string,
  ): Observable<AssignmentSubmission> {
    return this.read<AssignmentSubmission>(
      `${assignmentPath(enrollmentId, assignmentVersionId)}/submissions/current`,
    );
  }

  createAssignmentFile(
    enrollmentId: string,
    assignmentVersionId: string,
    request: {
      submissionId: string;
      clientFileId: string;
      expectedBytes: number;
      fileName: string;
      contentType: string;
    },
    idempotencyKey: string,
  ): Observable<UploadSession> {
    return this.mutation<UploadSession>(
      'post',
      `${assignmentPath(enrollmentId, assignmentVersionId)}/files`,
      request,
      idempotencyHeaders(idempotencyKey),
    );
  }

  getCourseLearners(courseId: string): Observable<readonly CourseLearner[]> {
    return this.read<readonly CourseLearner[]>(
      `instructor/courses/${encodeURIComponent(courseId)}/learners`,
    );
  }

  createQuizVersion(
    courseId: string,
    lessonId: string,
    request: CreateQuizVersionRequest,
  ): Observable<QuizVersion> {
    return this.mutation<QuizVersion>(
      'post',
      `instructor/courses/${encodeURIComponent(courseId)}/lessons/${encodeURIComponent(lessonId)}/quizzes/versions`,
      request,
    );
  }

  markQuizReady(courseId: string, versionId: string): Observable<QuizVersion> {
    return this.mutation<QuizVersion>(
      'post',
      `instructor/courses/${encodeURIComponent(courseId)}/quizzes/versions/${encodeURIComponent(versionId)}/ready`,
      null,
    );
  }

  createAssignmentVersion(
    courseId: string,
    lessonId: string,
    request: CreateAssignmentVersionRequest,
  ): Observable<AssignmentVersion> {
    return this.mutation<AssignmentVersion>(
      'post',
      `instructor/courses/${encodeURIComponent(courseId)}/lessons/${encodeURIComponent(lessonId)}/assignments/versions`,
      request,
    );
  }

  markAssignmentReady(courseId: string, versionId: string): Observable<AssignmentVersion> {
    return this.mutation<AssignmentVersion>(
      'post',
      `instructor/courses/${encodeURIComponent(courseId)}/assignments/versions/${encodeURIComponent(versionId)}/ready`,
      null,
    );
  }

  private read<T>(path: string): Observable<T> {
    return this.http
      .get<ApiEnvelope<T>>(path, { context: authenticatedReadContext() })
      .pipe(map((response) => response.data));
  }

  private mutation<T>(
    method: 'post' | 'put' | 'delete',
    path: string,
    body: unknown,
    headers = new HttpHeaders(),
  ): Observable<T> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.request<ApiEnvelope<T>>(method, path, {
          body,
          context: authenticatedMutationContext(),
          headers,
        }),
      ),
      map((response) => response.data),
    );
  }
}

const lessonPath = (enrollmentId: string, lessonId: string): string =>
  `learning/enrollments/${encodeURIComponent(enrollmentId)}/lessons/${encodeURIComponent(lessonId)}`;

const quizPath = (enrollmentId: string, quizVersionId: string): string =>
  `learning/enrollments/${encodeURIComponent(enrollmentId)}/quizzes/${encodeURIComponent(quizVersionId)}`;

const assignmentPath = (enrollmentId: string, assignmentVersionId: string): string =>
  `learning/enrollments/${encodeURIComponent(enrollmentId)}/assignments/${encodeURIComponent(assignmentVersionId)}`;

const idempotencyHeaders = (key: string): HttpHeaders =>
  new HttpHeaders({ 'Idempotency-Key': key });
