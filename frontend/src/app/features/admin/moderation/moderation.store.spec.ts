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
      { action: 'Dismiss', reason: 'The report is not supported by evidence' },
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
      { action: 'StartReview', reason: 'Starting the required content review' },
      'action-key',
      'Reviewing the reported discussion content',
    );

    expect(store.action().status).toBe('conflict');
    expect(store.detail().value).toEqual(caseResponse);
  });

  it('does not call the API while offline', () => {
    online.set(false);

    store.loadQueue();

    expect(store.queue().status).toBe('offline');
    expect(api.getModerationCases).not.toHaveBeenCalled();
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
