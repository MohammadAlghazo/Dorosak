import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ApiProblem } from '../../../core/api/api-problem';
import { ModerationApiClient } from '../../../core/api/moderation-api.client';
import type { ModerationCaseResponse } from '../../../core/api/moderation-api.types';
import { ConnectivityStore } from '../../../core/pwa/connectivity.store';
import { ModerationStore } from './moderation.store';

describe('ModerationStore', () => {
  let api: ReturnType<typeof createApi>;
  let store: ModerationStore;
  let online: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    api = createApi();
    online = signal(true);
    TestBed.configureTestingModule({
      providers: [
        ModerationStore,
        { provide: ModerationApiClient, useValue: api },
        { provide: ConnectivityStore, useValue: { isOnline: online.asReadonly() } },
      ],
    });
    store = TestBed.inject(ModerationStore);
  });

  it('paginates cases without duplicating an item', () => {
    api.getModerationCases
      .mockReturnValueOnce(
        of({ items: [caseResponse.case], nextCursor: 'cursor-1', hasMore: true }),
      )
      .mockReturnValueOnce(
        of({
          items: [caseResponse.case, secondCase],
          nextCursor: null,
          hasMore: false,
        }),
      );

    store.loadQueue({ kind: 'cases', status: 'Open', targetKind: null });
    store.loadMore();

    expect(api.getModerationCases).toHaveBeenLastCalledWith({
      status: 'Open',
      limit: 20,
      cursor: 'cursor-1',
    });
    expect(store.queue().cases.map((item) => item.id)).toEqual(['case-1', 'case-2']);
  });

  it('does not update a case before a destructive action succeeds', () => {
    const action = new Subject<ModerationCaseResponse>();
    api.getModerationCase.mockReturnValue(of(caseResponse));
    api.applyModerationAction.mockReturnValue(action);
    store.loadCase('case-1');

    store.applyAction(
      'case-1',
      { action: 'Dismiss', reason: 'The report is not supported by evidence', expectedVersion: 1 },
      'action-key',
      'Reviewing the reported discussion content',
    );

    expect(store.action().status).toBe('saving');
    expect(store.detail().value?.case.status).toBe('Open');

    action.next(closedCaseResponse);
    expect(store.action().status).toBe('success');
    expect(store.detail().value?.case.status).toBe('Dismissed');
  });

  it('keeps the loaded case unchanged when the server reports a conflict', () => {
    api.getModerationCase.mockReturnValue(of(caseResponse));
    api.applyModerationAction.mockReturnValue(
      throwError(
        () =>
          new ApiProblem(
            422,
            'MODERATION.CASE_NOT_OPEN',
            null,
            null,
            null,
            {},
            'The case changed.',
          ),
      ),
    );
    store.loadCase('case-1');

    store.applyAction(
      'case-1',
      { action: 'StartReview', reason: 'Starting the required content review', expectedVersion: 1 },
      'action-key',
      'Reviewing the reported discussion content',
    );

    expect(store.action().status).toBe('conflict');
    expect(store.detail().value).toEqual(caseResponse);
  });

  it('marks an ambiguous server failure as retryable for idempotent replay', () => {
    api.getModerationCase.mockReturnValue(of(caseResponse));
    api.applyModerationAction.mockReturnValue(
      throwError(
        () =>
          new ApiProblem(
            503,
            'DEPENDENCY.UNAVAILABLE',
            null,
            null,
            null,
            {},
            'The response is ambiguous.',
          ),
      ),
    );
    store.loadCase('case-1');

    store.applyAction(
      'case-1',
      { action: 'StartReview', reason: 'Starting the required content review', expectedVersion: 1 },
      'stable-action-key',
      'Reviewing the reported discussion content',
    );

    expect(store.action()).toMatchObject({
      status: 'error',
      errorCode: 'DEPENDENCY.UNAVAILABLE',
      retryable: true,
    });
  });

  it('does not call the API while offline', () => {
    online.set(false);

    store.loadQueue();

    expect(store.queue().status).toBe('offline');
    expect(api.getModerationCases).not.toHaveBeenCalled();
  });

  it('loads a pending route case after an in-flight action settles', () => {
    const action = new Subject<ModerationCaseResponse>();
    api.getModerationCase
      .mockReturnValueOnce(of(caseResponse))
      .mockReturnValueOnce(of(secondCaseResponse));
    api.applyModerationAction.mockReturnValue(action);
    store.loadCase('case-1');
    store.applyAction(
      'case-1',
      { action: 'Dismiss', reason: 'The report is not supported by evidence', expectedVersion: 1 },
      'action-key',
      'Reviewing the reported discussion content',
    );

    store.loadCase('case-2');
    expect(api.getModerationCase).toHaveBeenCalledTimes(1);
    expect(store.detail()).toEqual({ status: 'loading', value: null, errorCode: null });
    action.next(closedCaseResponse);

    expect(api.getModerationCase).toHaveBeenLastCalledWith('case-2');
    expect(store.detail().value?.case.id).toBe('case-2');
  });

  it('restarts pagination when the server rejects an expired cursor', () => {
    api.getModerationCases
      .mockReturnValueOnce(
        throwError(
          () => new ApiProblem(422, 'CURSOR.INVALID', null, null, null, {}, 'The cursor expired.'),
        ),
      )
      .mockReturnValueOnce(of({ items: [caseResponse.case], nextCursor: null, hasMore: false }));
    const filters = { kind: 'cases' as const, status: 'Open' as const, targetKind: null };

    store.loadQueue(filters, 'expired-cursor');
    store.retryQueue();

    expect(api.getModerationCases).toHaveBeenLastCalledWith({
      status: 'Open',
      limit: 20,
      cursor: null,
    });
    expect(store.queue().status).toBe('success');
  });
});

const createApi = () => ({
  getAdminReports: vi.fn<ModerationApiClient['getAdminReports']>(() =>
    of({ items: [], nextCursor: null, hasMore: false }),
  ),
  getModerationCases: vi.fn<ModerationApiClient['getModerationCases']>(() =>
    of({ items: [], nextCursor: null, hasMore: false }),
  ),
  getModerationCase: vi.fn<ModerationApiClient['getModerationCase']>(() => of(caseResponse)),
  applyModerationAction: vi.fn<ModerationApiClient['applyModerationAction']>(() =>
    of(caseResponse),
  ),
});

const report = {
  report: {
    id: 'report-1',
    targetKind: 'Comment' as const,
    targetId: 'comment-1',
    reason: 'Spam' as const,
    details: 'Repeated links',
    status: 'Open' as const,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
    closedAt: null,
  },
  reporterUserId: 'user-1',
  reporterName: 'Learner',
  caseId: 'case-1',
  caseStatus: 'Open' as const,
};

const caseResponse: ModerationCaseResponse = {
  case: {
    id: 'case-1',
    reportId: 'report-1',
    status: 'Open',
    assignedToUserId: null,
    assignedToName: null,
    version: 1,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
    closedAt: null,
    report,
  },
  actions: [],
  targetPreview: {
    status: 'Published',
    title: 'Discussion comment',
    body: 'Reported comment body',
    authorName: 'Learner',
  },
};

const secondCase = {
  ...caseResponse.case,
  id: 'case-2',
  reportId: 'report-2',
  report: {
    ...report,
    caseId: 'case-2',
    report: { ...report.report, id: 'report-2' },
  },
};

const secondCaseResponse: ModerationCaseResponse = {
  ...caseResponse,
  case: secondCase,
};

const closedCaseResponse: ModerationCaseResponse = {
  ...caseResponse,
  case: {
    ...caseResponse.case,
    status: 'Dismissed',
    closedAt: '2030-01-02T00:00:00Z',
    report: {
      ...caseResponse.case.report,
      caseStatus: 'Dismissed',
      report: {
        ...caseResponse.case.report.report,
        status: 'Dismissed',
        closedAt: '2030-01-02T00:00:00Z',
      },
    },
  },
};
