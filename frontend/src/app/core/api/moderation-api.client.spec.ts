import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { IdentityApiClient } from './identity-api.client';
import { ModerationApiClient } from './moderation-api.client';

describe('ModerationApiClient', () => {
  let client: ModerationApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(ModerationApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('creates a report with the caller-owned idempotency key', async () => {
    const promise = firstValueFrom(
      client.createReport(
        { commentId: 'comment/1', reason: 'Harassment', details: 'Repeated insults' },
        'report-key-1',
      ),
    );
    const request = http.expectOne('reports');

    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('report-key-1');
    expect(request.request.body).toEqual({
      commentId: 'comment/1',
      reason: 'Harassment',
      details: 'Repeated insults',
    });
    request.flush({ data: contentReport });
    await expect(promise).resolves.toEqual(contentReport);
  });

  it('loads the current user report using an encoded id', async () => {
    const promise = firstValueFrom(client.getMyReport('report/1'));
    const request = http.expectOne('me/reports/report%2F1');

    expect(request.request.method).toBe('GET');
    request.flush({ data: contentReport });
    await expect(promise).resolves.toEqual(contentReport);
  });

  it('sends report filters and cursor pagination', async () => {
    const promise = firstValueFrom(
      client.getAdminReports({
        status: 'Open',
        targetKind: 'Comment',
        limit: 25,
        cursor: 'next/report',
      }),
    );
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'admin/reports' &&
        candidate.params.get('status') === 'Open' &&
        candidate.params.get('targetKind') === 'Comment' &&
        candidate.params.get('limit') === '25' &&
        candidate.params.get('cursor') === 'next/report',
    );

    expect(request.request.method).toBe('GET');
    request.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await expect(promise).resolves.toEqual({ items: [], nextCursor: null, hasMore: false });
  });

  it('loads the case queue and a concrete case', async () => {
    const pagePromise = firstValueFrom(
      client.getModerationCases({ status: 'InReview', limit: 10, cursor: 'case-cursor' }),
    );
    const pageRequest = http.expectOne(
      (candidate) =>
        candidate.url === 'admin/moderation-cases' &&
        candidate.params.get('status') === 'InReview' &&
        candidate.params.get('limit') === '10' &&
        candidate.params.get('cursor') === 'case-cursor',
    );
    pageRequest.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await pagePromise;

    const detailPromise = firstValueFrom(client.getModerationCase('case/1'));
    const detailRequest = http.expectOne('admin/moderation-cases/case%2F1');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({ data: moderationCase });
    await expect(detailPromise).resolves.toEqual(moderationCase);
  });

  it('applies an audited action with a stable caller-owned key', async () => {
    const promise = firstValueFrom(
      client.applyModerationAction(
        'case/1',
        { action: 'HideContent', reason: 'Violates the discussion policy', expectedVersion: 4 },
        'action-key-1',
        'Reviewing a learner safety report',
      ),
    );
    const request = http.expectOne('admin/moderation-cases/case%2F1/actions');

    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('action-key-1');
    expect(request.request.headers.get('X-Audit-Reason')).toBe('Reviewing a learner safety report');
    expect(request.request.body).toEqual({
      action: 'HideContent',
      reason: 'Violates the discussion policy',
      expectedVersion: 4,
    });
    request.flush({ data: moderationCase });
    await expect(promise).resolves.toEqual(moderationCase);
  });
});

const contentReport = {
  id: 'report-1',
  targetKind: 'Comment' as const,
  targetId: 'comment-1',
  reason: 'Harassment' as const,
  details: 'Repeated insults',
  status: 'Open' as const,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
  closedAt: null,
};

const adminReport = {
  report: contentReport,
  reporterUserId: 'user-1',
  reporterName: 'Learner',
  caseId: 'case-1',
  caseStatus: 'Open' as const,
};

const moderationCase = {
  case: {
    id: 'case-1',
    reportId: 'report-1',
    status: 'Open' as const,
    assignedToUserId: null,
    assignedToName: null,
    version: 1,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
    closedAt: null,
    report: adminReport,
  },
  actions: [],
  targetPreview: {
    status: 'Published',
    title: 'Discussion comment',
    body: 'Reported comment body',
    authorName: 'Learner',
  },
};
