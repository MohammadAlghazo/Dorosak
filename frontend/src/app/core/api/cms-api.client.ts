import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
import { IdentityApiClient } from './identity-api.client';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';
import type {
  AdminCms,
  AuditLogPage,
  CmsFaq,
  CmsPage,
  PortfolioSettings,
  PublicPortfolioSettings,
  PublicCmsFaq,
  PublicCmsPage,
} from './cms-api.types';

@Injectable({ providedIn: 'root' })
export class CmsApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getPublicPage(slug: string): Observable<PublicCmsPage> {
    return this.http
      .get<ApiEnvelope<PublicCmsPage>>(`pages/${encodeURIComponent(slug)}`, {
        context: publicContext(),
      })
      .pipe(map((response) => response.data));
  }

  getFaqs(): Observable<readonly PublicCmsFaq[]> {
    return this.http
      .get<ApiEnvelope<readonly PublicCmsFaq[]>>('faqs', { context: publicContext() })
      .pipe(map((response) => response.data));
  }

  getPublicSettings(): Observable<PublicPortfolioSettings> {
    return this.http
      .get<ApiEnvelope<PublicPortfolioSettings>>('portfolio-settings', { context: publicContext() })
      .pipe(map((response) => response.data));
  }

  getAdminCms(): Observable<AdminCms> {
    return this.http
      .get<ApiEnvelope<AdminCms>>('admin/cms', { context: authenticatedReadContext() })
      .pipe(map((response) => response.data));
  }

  savePageDraft(
    slug: string,
    request: {
      expectedVersion: number;
      titleAr: string;
      titleEn: string;
      bodyAr: string;
      bodyEn: string;
    },
    auditReason: string,
  ): Observable<CmsPage> {
    return this.mutation<CmsPage>(
      'put',
      `admin/cms/pages/${encodeURIComponent(slug)}/draft`,
      request,
      auditReason,
    );
  }

  publishPage(slug: string, expectedVersion: number, auditReason: string): Observable<CmsPage> {
    return this.mutation<CmsPage>(
      'post',
      `admin/cms/pages/${encodeURIComponent(slug)}/publish`,
      { expectedVersion },
      auditReason,
    );
  }

  createFaqDraft(
    request: {
      expectedVersion: number;
      displayOrder: number;
      questionAr: string;
      questionEn: string;
      answerAr: string;
      answerEn: string;
    },
    auditReason: string,
  ): Observable<CmsFaq> {
    return this.mutation<CmsFaq>('post', 'admin/cms/faqs', request, auditReason);
  }

  saveFaqDraft(
    faqId: string,
    request: {
      expectedVersion: number;
      displayOrder: number;
      questionAr: string;
      questionEn: string;
      answerAr: string;
      answerEn: string;
    },
    auditReason: string,
  ): Observable<CmsFaq> {
    return this.mutation<CmsFaq>(
      'put',
      `admin/cms/faqs/${encodeURIComponent(faqId)}/draft`,
      request,
      auditReason,
    );
  }

  publishFaq(faqId: string, expectedVersion: number, auditReason: string): Observable<CmsFaq> {
    return this.mutation<CmsFaq>(
      'post',
      `admin/cms/faqs/${encodeURIComponent(faqId)}/publish`,
      { expectedVersion },
      auditReason,
    );
  }

  getSettings(): Observable<PortfolioSettings> {
    return this.http
      .get<ApiEnvelope<PortfolioSettings>>('admin/settings', {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  updateSettings(
    request: {
      featuredCourseLimit: number;
      showPortfolioNotice: boolean;
      noticeAr: string;
      noticeEn: string;
      expectedVersion: number;
    },
    auditReason: string,
  ): Observable<PortfolioSettings> {
    return this.mutation<PortfolioSettings>('put', 'admin/settings', request, auditReason);
  }

  getAuditLogs(
    auditReason: string,
    limit = 50,
    cursor: string | null = null,
    action: string | null = null,
  ): Observable<AuditLogPage> {
    let params = new HttpParams().set('limit', limit);
    if (cursor) params = params.set('cursor', cursor);
    if (action) params = params.set('action', action);
    return this.http
      .get<ApiEnvelope<AuditLogPage>>('admin/audit-logs', {
        context: authenticatedReadContext(),
        headers: auditHeaders(auditReason),
        params,
      })
      .pipe(map((response) => response.data));
  }

  private mutation<T>(
    method: 'post' | 'put',
    path: string,
    body: unknown,
    auditReason: string,
  ): Observable<T> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.request<ApiEnvelope<T>>(method, path, {
          body,
          context: authenticatedMutationContext(),
          headers: auditHeaders(auditReason),
        }),
      ),
      map((response) => response.data),
    );
  }
}

const auditHeaders = (reason: string): HttpHeaders =>
  new HttpHeaders({ 'X-Audit-Reason': reason.trim() });

const publicContext = (): HttpContext =>
  new HttpContext().set(API_REQUEST, true).set(PUBLIC_API_REQUEST, true).set(DEADLINE_MS, 15_000);
