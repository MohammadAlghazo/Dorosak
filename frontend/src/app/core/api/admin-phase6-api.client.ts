import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, switchMap } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import type { CourseRelease } from './learning-api.types';
import { IdentityApiClient } from './identity-api.client';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';
import type {
  Category,
  CategoryUpsertRequest,
  CursorPage,
  PublicationReview,
  Tag,
  TagUpsertRequest,
  TeacherApplication,
} from './phase6-api.types';

@Injectable({ providedIn: 'root' })
export class AdminPhase6ApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getTeacherApplications(
    auditReason: string,
    limit = 20,
    cursor: string | null = null,
  ): Observable<CursorPage<TeacherApplication>> {
    return this.paged<TeacherApplication>('admin/teacher-applications', limit, cursor, auditReason);
  }

  reviewTeacherApplication(
    applicationId: string,
    decision: 'start' | 'approve' | 'reject',
    reason: string | null,
    auditReason: string,
  ): Observable<TeacherApplication> {
    return this.mutation<TeacherApplication>(
      'post',
      `admin/teacher-applications/${encodeURIComponent(applicationId)}/review`,
      { decision, reason },
      auditReason,
    );
  }

  getPublicationReviews(
    limit = 20,
    cursor: string | null = null,
  ): Observable<CursorPage<PublicationReview>> {
    return this.paged<PublicationReview>('admin/publication-reviews', limit, cursor);
  }

  reviewPublication(
    reviewId: string,
    decision: 'changesRequested' | 'approve',
    reason: string | null,
  ): Observable<PublicationReview> {
    return this.mutation<PublicationReview>(
      'post',
      `admin/publication-reviews/${encodeURIComponent(reviewId)}/decision`,
      { decision, reason },
    );
  }

  publishCourse(courseId: string, auditReason: string): Observable<CourseRelease> {
    return this.releaseMutation(courseId, 'publish', auditReason);
  }

  unpublishCourse(courseId: string, auditReason: string): Observable<CourseRelease> {
    return this.releaseMutation(courseId, 'unpublish', auditReason);
  }

  getCategories(limit = 100, cursor: string | null = null): Observable<CursorPage<Category>> {
    return this.paged<Category>('admin/catalog/categories', limit, cursor);
  }

  getTags(limit = 100, cursor: string | null = null): Observable<CursorPage<Tag>> {
    return this.paged<Tag>('admin/catalog/tags', limit, cursor);
  }

  createCategory(request: CategoryUpsertRequest): Observable<Category> {
    return this.mutation<Category>('post', 'admin/catalog/categories', request);
  }

  updateCategory(categoryId: string, request: CategoryUpsertRequest): Observable<Category> {
    return this.mutation<Category>(
      'put',
      `admin/catalog/categories/${encodeURIComponent(categoryId)}`,
      request,
    );
  }

  createTag(request: TagUpsertRequest): Observable<Tag> {
    return this.mutation<Tag>('post', 'admin/catalog/tags', request);
  }

  updateTag(tagId: string, request: TagUpsertRequest): Observable<Tag> {
    return this.mutation<Tag>('put', `admin/catalog/tags/${encodeURIComponent(tagId)}`, request);
  }

  private paged<T>(
    path: string,
    limit: number,
    cursor: string | null,
    auditReason?: string,
  ): Observable<CursorPage<T>> {
    let params = new HttpParams().set('limit', limit);
    if (cursor !== null) params = params.set('cursor', cursor);
    const headers = auditReason === undefined ? new HttpHeaders() : auditHeaders(auditReason);
    return this.http
      .get<ApiEnvelope<CursorPage<T>>>(path, {
        context: authenticatedReadContext(),
        headers,
        params,
      })
      .pipe(map((response) => response.data));
  }

  private mutation<T>(
    method: 'post' | 'put',
    path: string,
    body: unknown,
    auditReason?: string,
  ): Observable<T> {
    const headers = auditReason === undefined ? new HttpHeaders() : auditHeaders(auditReason);
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

  private releaseMutation(
    courseId: string,
    operation: 'publish' | 'unpublish',
    auditReason: string,
  ): Observable<CourseRelease> {
    const headers = auditHeaders(auditReason).set(
      'Idempotency-Key',
      globalThis.crypto.randomUUID(),
    );
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<CourseRelease>>(
          `admin/courses/${encodeURIComponent(courseId)}/${operation}`,
          null,
          { context: authenticatedMutationContext(), headers },
        ),
      ),
      map((response) => response.data),
    );
  }
}

const auditHeaders = (reason: string): HttpHeaders =>
  new HttpHeaders({ 'X-Audit-Reason': reason.trim() });
