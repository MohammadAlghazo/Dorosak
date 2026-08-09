import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
import { authenticatedMutationContext } from './phase6-api.helpers';
import type { CourseReview, CourseReviewPage } from './engagement-api.types';
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
}
