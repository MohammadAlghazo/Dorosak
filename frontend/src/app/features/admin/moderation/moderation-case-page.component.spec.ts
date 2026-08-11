import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiProblem } from '../../../core/api/api-problem';
import { ModerationApiClient } from '../../../core/api/moderation-api.client';
import type { ModerationCaseResponse } from '../../../core/api/moderation-api.types';
import { LocaleService } from '../../../core/i18n/locale.service';
import { ConnectivityStore } from '../../../core/pwa/connectivity.store';
import { ModerationCasePageComponent } from './moderation-case-page.component';

describe('ModerationCasePageComponent', () => {
  let fixture: ComponentFixture<ModerationCasePageComponent>;
  let api: ReturnType<typeof createApi>;
  let locale: ReturnType<typeof signal<'ar' | 'en'>>;

  beforeEach(async () => {
    api = createApi();
    locale = signal<'ar' | 'en'>('en');
    await TestBed.configureTestingModule({
      imports: [ModerationCasePageComponent],
      providers: [
        provideRouter([]),
        { provide: ModerationApiClient, useValue: api },
        { provide: LocaleService, useValue: { locale: locale.asReadonly() } },
        { provide: ConnectivityStore, useValue: { isOnline: signal(true).asReadonly() } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ModerationCasePageComponent);
    fixture.componentRef.setInput('caseId', 'case-1');
    fixture.detectChanges();
  });

  it('closes and resets an action dialog when the route changes to another case', async () => {
    const dialog = root().querySelector<HTMLDialogElement>('dialog');
    if (!dialog) throw new Error('Moderation action dialog not found.');
    const showModal = vi.fn(() => {
      dialog.setAttribute('open', '');
    });
    const close = vi.fn(() => {
      dialog.removeAttribute('open');
    });
    Object.defineProperties(dialog, {
      showModal: { value: showModal, configurable: true },
      close: { value: close, configurable: true },
    });

    root().querySelector<HTMLButtonElement>('.case-action')?.click();
    fixture.detectChanges();
    expect(showModal).toHaveBeenCalledOnce();
    expect(dialog.hasAttribute('open')).toBe(true);

    fixture.componentRef.setInput('caseId', 'case-2');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(close).toHaveBeenCalledOnce();
    expect(dialog.hasAttribute('open')).toBe(false);
    expect(api.getModerationCase).toHaveBeenLastCalledWith('case-2');
    expect(root().textContent).toContain('case-2');
    expect(document.activeElement).toBe(root().querySelector('#case-title'));
  });

  it('localizes lifecycle statuses and isolates dynamic names in Arabic', () => {
    const base = createCase('case-localized', 'report-localized');
    const localizedCase: ModerationCaseResponse = {
      ...base,
      case: { ...base.case, assignedToName: 'English Reviewer' },
      targetPreview: {
        ...base.targetPreview,
        status: 'ReadyToPublish',
        authorName: 'English Author',
      },
    };
    api.getModerationCase.mockReturnValue(of(localizedCase));
    locale.set('ar');

    fixture.componentRef.setInput('caseId', 'case-localized');
    fixture.detectChanges();

    expect(root().textContent).toContain('جاهز للنشر');
    const isolatedValues = [...root().querySelectorAll<HTMLElement>('span[dir="auto"]')].map(
      (element) => element.textContent.trim(),
    );
    expect(isolatedValues).toContain('English Reviewer');
    expect(isolatedValues).toContain('English Author');
  });

  it('reuses the action idempotency key after an ambiguous server failure', () => {
    api.applyModerationAction
      .mockReturnValueOnce(
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
      )
      .mockReturnValueOnce(of(caseOne));
    const dialog = root().querySelector<HTMLDialogElement>('dialog');
    if (!dialog) throw new Error('Moderation action dialog not found.');
    Object.defineProperty(dialog, 'showModal', { value: vi.fn(), configurable: true });
    root().querySelector<HTMLButtonElement>('.case-action')?.click();
    fixture.detectChanges();
    const textareas = root().querySelectorAll<HTMLTextAreaElement>('textarea');
    setValue(textareas[0], 'Starting the synthetic moderation review.');
    setValue(textareas[1], 'Synthetic moderation audit reason.');
    const confirmation = root().querySelector<HTMLInputElement>('#moderation-confirm');
    if (!confirmation) throw new Error('Moderation confirmation control not found.');
    confirmation.checked = true;
    confirmation.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    submit();
    submit();

    expect(api.applyModerationAction).toHaveBeenCalledTimes(2);
    expect(api.applyModerationAction.mock.calls[1]?.[2]).toBe(
      api.applyModerationAction.mock.calls[0]?.[2],
    );
  });

  const root = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const setValue = (control: HTMLTextAreaElement | undefined, value: string): void => {
    if (!control) throw new Error('Moderation textarea not found.');
    control.value = value;
    control.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  const submit = (): void => {
    root().querySelector<HTMLFormElement>('dialog form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };
});

const createApi = () => ({
  getAdminReports: vi.fn<ModerationApiClient['getAdminReports']>(() =>
    of({ items: [], nextCursor: null, hasMore: false }),
  ),
  getModerationCases: vi.fn<ModerationApiClient['getModerationCases']>(() =>
    of({ items: [], nextCursor: null, hasMore: false }),
  ),
  getModerationCase: vi.fn<ModerationApiClient['getModerationCase']>((caseId) =>
    of(caseId === 'case-2' ? caseTwo : caseOne),
  ),
  applyModerationAction: vi.fn<ModerationApiClient['applyModerationAction']>(() => of(caseOne)),
});

const caseOne = createCase('case-1', 'report-1');
const caseTwo = createCase('case-2', 'report-2');

function createCase(caseId: string, reportId: string): ModerationCaseResponse {
  return {
    case: {
      id: caseId,
      reportId,
      status: 'Open',
      assignedToUserId: null,
      assignedToName: null,
      version: 1,
      createdAt: '2030-01-01T00:00:00Z',
      updatedAt: '2030-01-01T00:00:00Z',
      closedAt: null,
      report: {
        report: {
          id: reportId,
          targetKind: 'Comment',
          targetId: 'comment-1',
          reason: 'Spam',
          details: 'Synthetic report details',
          status: 'Open',
          createdAt: '2030-01-01T00:00:00Z',
          updatedAt: '2030-01-01T00:00:00Z',
          closedAt: null,
        },
        reporterUserId: 'user-1',
        reporterName: 'Learner',
        caseId,
        caseStatus: 'Open',
      },
    },
    actions: [],
    targetPreview: {
      status: 'Published',
      title: 'Discussion comment',
      body: 'Synthetic reported comment',
      authorName: 'Learner',
    },
  };
}
