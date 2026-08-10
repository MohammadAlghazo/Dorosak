import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';
import type {
  CommentLikeResult,
  CourseReview,
  CourseReviewPage,
  DiscussionComment,
  DiscussionThread,
  DiscussionThreadPage,
} from './engagement-api.types';
import { IdentityApiClient } from './identity-api.client';

@Injectable({ providedIn: 'root' })
export class EngagementApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getCourseReviews(courseId: string, limit = 10): Observable<CourseReviewPage> {
    return this.http
      .get<ApiEnvelope<CourseReviewPage>>(
        `catalog/courses/${encodeURIComponent(courseId)}/reviews`,
        {
          context: new HttpContext()
            .set(API_REQUEST, true)
            .set(PUBLIC_API_REQUEST, true)
            .set(DEADLINE_MS, 15_000),
          params: new HttpParams().set('limit', limit),
        },
      )
      .pipe(map((response) => response.data));
  }

  createCourseReview(courseId: string, rating: number, text: string): Observable<CourseReview> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<CourseReview>>(
          `courses/${encodeURIComponent(courseId)}/reviews`,
          { rating, text: text.trim() || null },
          {
            context: authenticatedMutationContext(),
            headers: new HttpHeaders({ 'Idempotency-Key': globalThis.crypto.randomUUID() }),
          },
        ),
      ),
      map((response) => response.data),
    );
  }

  getMyCourseReview(courseId: string): Observable<CourseReview> {
    return this.http
      .get<ApiEnvelope<CourseReview>>(`courses/${encodeURIComponent(courseId)}/reviews/mine`, {
        context: new HttpContext().set(API_REQUEST, true).set(DEADLINE_MS, 15_000),
      })
      .pipe(map((response) => response.data));
  }

  updateCourseReview(
    courseId: string,
    reviewId: string,
    rating: number,
    text: string,
  ): Observable<CourseReview> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.put<ApiEnvelope<CourseReview>>(
          `courses/${encodeURIComponent(courseId)}/reviews/${encodeURIComponent(reviewId)}`,
          { rating, text: text.trim() || null },
          { context: authenticatedMutationContext() },
        ),
      ),
      map((response) => response.data),
    );
  }

  deleteCourseReview(courseId: string, reviewId: string): Observable<boolean> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.delete<ApiEnvelope<{ completed: boolean }>>(
          `courses/${encodeURIComponent(courseId)}/reviews/${encodeURIComponent(reviewId)}`,
          { context: authenticatedMutationContext() },
        ),
      ),
      map((response) => response.data.completed),
    );
  }

  getDiscussionThreads(
    enrollmentId: string,
    lessonId: string | null,
    cursor: string | null = null,
    limit = 20,
  ): Observable<DiscussionThreadPage> {
    let params = new HttpParams().set('limit', limit);
    if (cursor) params = params.set('cursor', cursor);
    return this.http
      .get<ApiEnvelope<DiscussionThreadPage>>(discussionPath(enrollmentId, lessonId), {
        context: authenticatedReadContext(),
        params,
      })
      .pipe(map((response) => response.data));
  }

  getDiscussionThread(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    commentCursor: string | null = null,
    commentLimit = 50,
  ): Observable<DiscussionThread> {
    let params = new HttpParams().set('commentLimit', commentLimit);
    if (commentCursor) params = params.set('commentCursor', commentCursor);
    return this.http
      .get<ApiEnvelope<DiscussionThread>>(
        `${discussionPath(enrollmentId, lessonId)}/${encodeURIComponent(threadId)}`,
        { context: authenticatedReadContext(), params },
      )
      .pipe(map((response) => response.data));
  }

  createDiscussionThread(
    enrollmentId: string,
    lessonId: string | null,
    title: string,
    body: string,
    idempotencyKey: string,
  ): Observable<DiscussionThread> {
    return this.privateMutation<DiscussionThread>(
      'post',
      discussionPath(enrollmentId, lessonId),
      { title: title.trim(), body: body.trim() },
      idempotencyKey,
    );
  }

  updateDiscussionThread(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    title: string,
    body: string,
  ): Observable<DiscussionThread> {
    return this.privateMutation<DiscussionThread>(
      'put',
      `${discussionPath(enrollmentId, lessonId)}/${encodeURIComponent(threadId)}`,
      { title: title.trim(), body: body.trim() },
    );
  }

  deleteDiscussionThread(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
  ): Observable<boolean> {
    return this.privateMutation<{ completed: boolean }>(
      'delete',
      `${discussionPath(enrollmentId, lessonId)}/${encodeURIComponent(threadId)}`,
      null,
    ).pipe(map((response) => response.completed));
  }

  createDiscussionComment(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    body: string,
    parentCommentId: string | null,
    idempotencyKey: string,
  ): Observable<DiscussionComment> {
    return this.privateMutation<DiscussionComment>(
      'post',
      `${discussionPath(enrollmentId, lessonId)}/${encodeURIComponent(threadId)}/comments`,
      { body: body.trim(), parentCommentId },
      idempotencyKey,
    );
  }

  updateDiscussionComment(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    commentId: string,
    body: string,
  ): Observable<DiscussionComment> {
    return this.privateMutation<DiscussionComment>(
      'put',
      commentPath(enrollmentId, lessonId, threadId, commentId),
      { body: body.trim() },
    );
  }

  deleteDiscussionComment(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    commentId: string,
  ): Observable<boolean> {
    return this.privateMutation<{ completed: boolean }>(
      'delete',
      commentPath(enrollmentId, lessonId, threadId, commentId),
      null,
    ).pipe(map((response) => response.completed));
  }

  setDiscussionCommentLike(
    enrollmentId: string,
    lessonId: string | null,
    threadId: string,
    commentId: string,
    liked: boolean,
  ): Observable<CommentLikeResult> {
    return this.privateMutation<CommentLikeResult>(
      liked ? 'put' : 'delete',
      `${commentPath(enrollmentId, lessonId, threadId, commentId)}/like`,
      null,
    );
  }

  private privateMutation<T>(
    method: 'post' | 'put' | 'delete',
    path: string,
    body: unknown,
    idempotencyKey?: string,
  ): Observable<T> {
    const headers = idempotencyKey
      ? new HttpHeaders({ 'Idempotency-Key': idempotencyKey })
      : new HttpHeaders();
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

const discussionPath = (enrollmentId: string, lessonId: string | null): string => {
  const enrollmentPath = `learning/enrollments/${encodeURIComponent(enrollmentId)}`;
  return lessonId === null
    ? `${enrollmentPath}/discussions`
    : `${enrollmentPath}/lessons/${encodeURIComponent(lessonId)}/discussions`;
};

const commentPath = (
  enrollmentId: string,
  lessonId: string | null,
  threadId: string,
  commentId: string,
): string =>
  `${discussionPath(enrollmentId, lessonId)}/${encodeURIComponent(threadId)}/comments/${encodeURIComponent(commentId)}`;
