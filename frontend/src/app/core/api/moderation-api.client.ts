import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { IdentityApiClient } from './identity-api.client';
import type {
  AdminContentReportQuery,
  ContentReportPageResponse,
  ContentReportResponse,
  CreateContentReportRequest,
  ModerationActionRequest,
  ModerationCasePageResponse,
  ModerationCaseQuery,
  ModerationCaseResponse,
} from './moderation-api.types';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class ModerationApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  createReport(
    request: CreateContentReportRequest,
    idempotencyKey: string,
  ): Observable<ContentReportResponse> {
    return this.mutation<ContentReportResponse>(
      'reports',
      request,
      new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    );
  }

  getMyReport(reportId: string): Observable<ContentReportResponse> {
    return this.http
      .get<ApiEnvelope<ContentReportResponse>>(`me/reports/${encodeURIComponent(reportId)}`, {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  getAdminReports(query: AdminContentReportQuery = {}): Observable<ContentReportPageResponse> {
    return this.http
      .get<ApiEnvelope<ContentReportPageResponse>>('admin/reports', {
        context: authenticatedReadContext(),
        params: reportParams(query),
      })
      .pipe(map((response) => response.data));
  }

  getModerationCases(query: ModerationCaseQuery = {}): Observable<ModerationCasePageResponse> {
    return this.http
      .get<ApiEnvelope<ModerationCasePageResponse>>('admin/moderation-cases', {
        context: authenticatedReadContext(),
        params: caseParams(query),
      })
      .pipe(map((response) => response.data));
  }

  getModerationCase(caseId: string): Observable<ModerationCaseResponse> {
    return this.http
      .get<ApiEnvelope<ModerationCaseResponse>>(
        `admin/moderation-cases/${encodeURIComponent(caseId)}`,
        { context: authenticatedReadContext() },
      )
      .pipe(map((response) => response.data));
  }

  applyModerationAction(
    caseId: string,
    request: ModerationActionRequest,
    idempotencyKey: string,
    auditReason: string,
  ): Observable<ModerationCaseResponse> {
    return this.mutation<ModerationCaseResponse>(
      `admin/moderation-cases/${encodeURIComponent(caseId)}/actions`,
      {
        action: request.action,
        reason: request.reason.trim(),
        expectedVersion: request.expectedVersion,
      },
      new HttpHeaders({
        'Idempotency-Key': idempotencyKey,
        'X-Audit-Reason': auditReason.trim(),
      }),
    );
  }

  private mutation<T>(path: string, body: unknown, headers: HttpHeaders): Observable<T> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<T>>(path, body, {
          context: authenticatedMutationContext(),
          headers,
        }),
      ),
      map((response) => response.data),
    );
  }
}

const reportParams = (query: AdminContentReportQuery): HttpParams => {
  let params = new HttpParams().set('limit', query.limit ?? 50);
  if (query.status) params = params.set('status', query.status);
  if (query.targetKind) params = params.set('targetKind', query.targetKind);
  if (query.cursor) params = params.set('cursor', query.cursor);
  return params;
};

const caseParams = (query: ModerationCaseQuery): HttpParams => {
  let params = new HttpParams().set('limit', query.limit ?? 50);
  if (query.status) params = params.set('status', query.status);
  if (query.cursor) params = params.set('cursor', query.cursor);
  return params;
};
