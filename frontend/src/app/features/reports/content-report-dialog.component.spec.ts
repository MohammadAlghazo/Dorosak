import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { ModerationApiClient } from '../../core/api/moderation-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { ContentReportDialogComponent } from './content-report-dialog.component';

describe('ContentReportDialogComponent', () => {
  let fixture: ComponentFixture<ContentReportDialogComponent>;
  let api: ReturnType<typeof createApi>;

  beforeEach(async () => {
    api = createApi();
    await TestBed.configureTestingModule({
      imports: [ContentReportDialogComponent],
      providers: [
        { provide: ModerationApiClient, useValue: api },
        { provide: LocaleService, useValue: { locale: signal<'ar' | 'en'>('en').asReadonly() } },
        { provide: ConnectivityStore, useValue: { isOnline: signal(true).asReadonly() } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ContentReportDialogComponent);
    fixture.detectChanges();
    const dialog = root().querySelector<HTMLDialogElement>('dialog');
    if (!dialog) throw new Error('Report dialog not found.');
    Object.defineProperties(dialog, {
      showModal: { value: vi.fn(), configurable: true },
      close: { value: vi.fn(), configurable: true },
    });
  });

  it('reuses the same idempotency key when a timed-out report is retried', () => {
    const firstAttempt = new Subject<typeof report>();
    api.createReport.mockReturnValueOnce(firstAttempt).mockReturnValueOnce(of(report));
    fixture.componentInstance.openForComment('comment-1');
    chooseReason('Harassment');

    submit();
    firstAttempt.error(new ApiProblem(408, 'HTTP.408', null, null, null, {}, 'Request timed out.'));
    fixture.detectChanges();
    submit();

    expect(api.createReport).toHaveBeenCalledTimes(2);
    expect(api.createReport.mock.calls[1]?.[1]).toBe(api.createReport.mock.calls[0]?.[1]);
    expect(api.createReport.mock.calls[0]?.[0]).toEqual({
      commentId: 'comment-1',
      reason: 'Harassment',
    });
  });

  it('requires details when the selected reason is Other', () => {
    fixture.componentInstance.openForComment('comment-1');
    chooseReason('Other');

    submit();
    fixture.detectChanges();

    expect(api.createReport).not.toHaveBeenCalled();
    expect(root().textContent).toContain('Provide at least 10 characters');
  });

  const root = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const chooseReason = (reason: string): void => {
    const select = root().querySelector<HTMLSelectElement>('#content-report-reason');
    if (!select) throw new Error('Report reason control not found.');
    select.value = reason;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const submit = (): void => {
    root().querySelector<HTMLFormElement>('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };
});

const createApi = () => ({
  createReport: vi.fn<ModerationApiClient['createReport']>(() => of(report)),
});

const report = {
  id: 'report-1',
  targetKind: 'Comment' as const,
  targetId: 'comment-1',
  reason: 'Harassment' as const,
  details: '',
  status: 'Open' as const,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
  closedAt: null,
};
