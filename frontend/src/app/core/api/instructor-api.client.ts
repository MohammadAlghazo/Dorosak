import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, switchMap } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { IdentityApiClient } from './identity-api.client';
import {
  authenticatedMutationContext,
  authenticatedReadContext,
  unwrapVersioned,
} from './phase6-api.helpers';
import type {
  CourseCreateRequest,
  CourseDetails,
  CourseMetadataRequest,
  CourseMutation,
  CourseSummary,
  Curriculum,
  CursorPage,
  PublicationStatus,
  SectionInput,
  VersionedResult,
} from './phase6-api.types';

@Injectable({ providedIn: 'root' })
export class InstructorApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getCourses(limit = 20, cursor: string | null = null): Observable<CursorPage<CourseSummary>> {
    let params = new HttpParams().set('limit', limit);
    if (cursor !== null) params = params.set('cursor', cursor);
    return this.http
      .get<ApiEnvelope<CursorPage<CourseSummary>>>('instructor/courses', {
        context: authenticatedReadContext(),
        params,
      })
      .pipe(map((response) => response.data));
  }

  createCourse(request: CourseCreateRequest): Observable<VersionedResult<CourseMutation>> {
    return this.versionedMutation('post', 'instructor/courses', request);
  }

  getCourse(courseId: string): Observable<VersionedResult<CourseDetails>> {
    return this.http
      .get<ApiEnvelope<CourseDetails>>(`instructor/courses/${encodeURIComponent(courseId)}`, {
        context: authenticatedReadContext(),
        observe: 'response',
      })
      .pipe(map(unwrapVersioned));
  }

  updateCourseMetadata(
    courseId: string,
    request: CourseMetadataRequest,
    etag: string,
  ): Observable<VersionedResult<CourseMutation>> {
    return this.versionedMutation(
      'patch',
      `instructor/courses/${encodeURIComponent(courseId)}`,
      request,
      etag,
    );
  }

  getCurriculum(courseId: string): Observable<VersionedResult<Curriculum>> {
    return this.http
      .get<ApiEnvelope<Curriculum>>(
        `instructor/courses/${encodeURIComponent(courseId)}/curriculum`,
        { context: authenticatedReadContext(), observe: 'response' },
      )
      .pipe(map(unwrapVersioned));
  }

  updateCurriculum(
    courseId: string,
    sections: readonly SectionInput[],
    etag: string,
  ): Observable<VersionedResult<CourseMutation>> {
    return this.versionedMutation(
      'put',
      `instructor/courses/${encodeURIComponent(courseId)}/curriculum`,
      { sections },
      etag,
    );
  }

  getPublicationStatus(courseId: string): Observable<VersionedResult<PublicationStatus>> {
    return this.http
      .get<ApiEnvelope<PublicationStatus>>(
        `instructor/courses/${encodeURIComponent(courseId)}/publication-status`,
        { context: authenticatedReadContext(), observe: 'response' },
      )
      .pipe(map(unwrapVersioned));
  }

  requestPublication(courseId: string): Observable<VersionedResult<PublicationStatus>> {
    return this.versionedMutation(
      'post',
      `instructor/courses/${encodeURIComponent(courseId)}/publication-requests`,
      null,
    );
  }

  withdrawPublication(courseId: string): Observable<VersionedResult<PublicationStatus>> {
    const path = `instructor/courses/${encodeURIComponent(courseId)}/publication-requests/current`;
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.delete<ApiEnvelope<PublicationStatus>>(path, {
          context: authenticatedMutationContext(),
          observe: 'response',
        }),
      ),
      map(unwrapVersioned),
    );
  }

  private versionedMutation<T extends { draftVersion: number }>(
    method: 'patch' | 'post' | 'put',
    path: string,
    body: unknown,
    etag?: string,
  ): Observable<VersionedResult<T>> {
    const context = authenticatedMutationContext();
    const headers = etag === undefined ? undefined : new HttpHeaders({ 'If-Match': etag });
    const options = responseOptions(context, headers);
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() => {
        if (method === 'post') {
          return this.http.post<ApiEnvelope<T>>(path, body, options);
        }
        if (method === 'patch') {
          return this.http.patch<ApiEnvelope<T>>(path, body, options);
        }
        return this.http.put<ApiEnvelope<T>>(path, body, options);
      }),
      map((response) => unwrapVersioned(response)),
    );
  }
}

const responseOptions = (
  context: ReturnType<typeof authenticatedMutationContext>,
  headers?: HttpHeaders,
) =>
  headers === undefined
    ? { context, observe: 'response' as const }
    : { context, headers, observe: 'response' as const };
