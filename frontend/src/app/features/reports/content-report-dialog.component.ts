import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { ElementRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiProblem } from '../../core/api/api-problem';
import { ModerationApiClient } from '../../core/api/moderation-api.client';
import type {
  ContentReportReason,
  ContentReportResponse,
  CreateContentReportRequest,
} from '../../core/api/moderation-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { requiredValidator } from '../auth/auth-form.helpers';

type ReportStatus = 'idle' | 'saving' | 'success' | 'offline' | 'conflict' | 'error';
type ReportReasonControl = ContentReportReason | '';

@Component({
  selector: 'drs-content-report-dialog',
  imports: [ReactiveFormsModule],
  template: `
    <dialog #reportDialog aria-labelledby="content-report-title" (cancel)="cancel($event)">
      <form class="dialog-content report-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="report-heading">
          <p>{{ locale.locale() === 'ar' ? 'سلامة المجتمع' : 'Community safety' }}</p>
          <h2 id="content-report-title">
            {{ locale.locale() === 'ar' ? 'الإبلاغ عن التعليق' : 'Report comment' }}
          </h2>
        </div>
        @if (state().status === 'success') {
          <div class="report-success" role="status">
            <strong>
              {{ locale.locale() === 'ar' ? 'تم استلام البلاغ' : 'Report received' }}
            </strong>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'احتفظ برقم البلاغ كمرجع عند الحاجة.'
                  : 'Keep the report ID as a reference if you need support.'
              }}
            </p>
            <code>{{ state().report?.id }}</code>
          </div>
        } @else {
          <p class="report-intro">
            {{
              locale.locale() === 'ar'
                ? 'اختر السبب الأقرب. لا تُدرج بيانات شخصية إضافية في التفاصيل.'
                : 'Choose the closest reason. Do not add unnecessary personal data in the details.'
            }}
          </p>
          <label for="content-report-reason">
            {{ locale.locale() === 'ar' ? 'سبب البلاغ' : 'Report reason' }}
          </label>
          <select
            id="content-report-reason"
            formControlName="reason"
            [attr.aria-invalid]="form.controls.reason.touched && form.controls.reason.invalid"
          >
            <option value="" disabled>
              {{ locale.locale() === 'ar' ? 'اختر سبباً' : 'Select a reason' }}
            </option>
            <option value="Spam">{{ reasonLabel('Spam') }}</option>
            <option value="Harassment">{{ reasonLabel('Harassment') }}</option>
            <option value="HateSpeech">{{ reasonLabel('HateSpeech') }}</option>
            <option value="Misinformation">{{ reasonLabel('Misinformation') }}</option>
            <option value="Copyright">{{ reasonLabel('Copyright') }}</option>
            <option value="PersonalData">{{ reasonLabel('PersonalData') }}</option>
            <option value="Other">{{ reasonLabel('Other') }}</option>
          </select>
          @if (form.controls.reason.touched && form.controls.reason.invalid) {
            <p class="field-error">
              {{ locale.locale() === 'ar' ? 'اختر سبب البلاغ.' : 'Select a report reason.' }}
            </p>
          }
          <div class="report-label-row">
            <label for="content-report-details">
              {{
                form.controls.reason.value === 'Other'
                  ? locale.locale() === 'ar'
                    ? 'التفاصيل (مطلوبة)'
                    : 'Details (required)'
                  : locale.locale() === 'ar'
                    ? 'التفاصيل (اختيارية)'
                    : 'Details (optional)'
              }}
            </label>
            <small>{{ form.controls.details.value.length }}/2000</small>
          </div>
          <textarea
            id="content-report-details"
            formControlName="details"
            rows="5"
            maxlength="2000"
            [attr.aria-invalid]="detailsInvalid()"
            aria-describedby="content-report-details-help"
          ></textarea>
          <small id="content-report-details-help" class="field-help">
            {{
              locale.locale() === 'ar'
                ? 'عند اختيار «سبب آخر»، اكتب 10 أحرف على الأقل.'
                : 'For “Other”, provide at least 10 characters.'
            }}
          </small>
          @if (detailsInvalid()) {
            <p class="field-error">
              {{
                locale.locale() === 'ar'
                  ? 'اكتب تفاصيل من 10 أحرف على الأقل لهذا السبب.'
                  : 'Provide at least 10 characters for this reason.'
              }}
            </p>
          }
          @switch (state().status) {
            @case ('offline') {
              <p class="field-error" role="alert">
                {{
                  locale.locale() === 'ar'
                    ? 'أنت غير متصل. سيبقى النموذج جاهزاً لإعادة المحاولة.'
                    : 'You are offline. The form remains ready to retry.'
                }}
              </p>
            }
            @case ('conflict') {
              <div class="report-conflict" role="alert">
                <strong>
                  {{
                    locale.locale() === 'ar' ? 'يوجد بلاغ مفتوح بالفعل' : 'A report is already open'
                  }}
                </strong>
                <p>
                  {{
                    locale.locale() === 'ar'
                      ? 'لا تحتاج إلى إرسال بلاغ آخر عن التعليق نفسه.'
                      : 'You do not need to submit another report for this comment.'
                  }}
                </p>
              </div>
            }
            @case ('error') {
              <p class="field-error" role="alert">
                {{ errorLabel(state().errorCode) }}
                @if (state().errorCode) {
                  <code>{{ state().errorCode }}</code>
                }
              </p>
            }
          }
        }
        <div class="dialog-actions">
          <button
            class="secondary-button"
            type="button"
            [disabled]="state().status === 'saving'"
            (click)="close()"
          >
            {{
              state().status === 'success' || state().status === 'conflict'
                ? locale.locale() === 'ar'
                  ? 'إغلاق'
                  : 'Close'
                : locale.locale() === 'ar'
                  ? 'إلغاء'
                  : 'Cancel'
            }}
          </button>
          @if (state().status !== 'success' && state().status !== 'conflict') {
            <button
              class="primary-button"
              type="submit"
              [disabled]="form.invalid || state().status === 'saving'"
            >
              {{
                state().status === 'saving'
                  ? locale.locale() === 'ar'
                    ? 'جار إرسال البلاغ…'
                    : 'Sending report…'
                  : state().status === 'offline' || state().status === 'error'
                    ? locale.locale() === 'ar'
                      ? 'إعادة المحاولة'
                      : 'Retry report'
                    : locale.locale() === 'ar'
                      ? 'إرسال البلاغ'
                      : 'Submit report'
              }}
            </button>
          }
        </div>
      </form>
    </dialog>
  `,
  styles: `
    .report-form {
      display: grid;
      gap: var(--space-3);
      margin: 0;
      padding: var(--space-5);
    }
    .report-heading p {
      margin: 0;
      color: var(--color-brand);
      font-size: 0.78rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }
    .report-heading h2 {
      margin-block: var(--space-2) 0;
    }
    .report-intro,
    .report-label-row small {
      color: var(--color-muted);
    }
    .report-form label {
      font-weight: 700;
    }
    .report-form select,
    .report-form textarea {
      inline-size: 100%;
      min-block-size: 48px;
      padding: var(--space-3);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
    }
    .report-form textarea {
      resize: vertical;
    }
    .report-form [aria-invalid='true'] {
      border-color: var(--color-danger);
    }
    .report-label-row,
    .dialog-actions {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
    }
    .report-success,
    .report-conflict {
      padding: var(--space-4);
      border-inline-start: 4px solid var(--color-success);
      background: var(--color-subtle);
    }
    .report-conflict {
      border-inline-start-color: var(--color-warning);
    }
    .report-success p,
    .report-conflict p {
      margin-block: var(--space-2);
    }
    .dialog-actions {
      justify-content: end;
      margin-block-start: var(--space-3);
    }
    .dialog-actions .secondary-button {
      margin-block-start: 0;
    }
    code {
      direction: ltr;
      unicode-bidi: isolate;
    }
    @media (max-width: 480px) {
      .dialog-actions {
        align-items: stretch;
        flex-direction: column-reverse;
      }
      .dialog-actions button {
        inline-size: 100%;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContentReportDialogComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly form = new FormGroup({
    reason: new FormControl<ReportReasonControl>('', {
      nonNullable: true,
      validators: [requiredValidator],
    }),
    details: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(2000)],
    }),
  });
  protected readonly state = signal<{
    status: ReportStatus;
    report: ContentReportResponse | null;
    errorCode: string | null;
  }>({ status: 'idle', report: null, errorCode: null });
  private readonly api = inject(ModerationApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('reportDialog');
  private commentId: string | null = null;
  private submitted = false;
  private requestSignature: string | null = null;
  private idempotencyKey: string | null = null;

  openForComment(commentId: string): void {
    this.commentId = commentId;
    this.submitted = false;
    this.requestSignature = null;
    this.idempotencyKey = null;
    this.form.reset({ reason: '', details: '' });
    this.state.set({ status: 'idle', report: null, errorCode: null });
    this.dialog().nativeElement.showModal();
  }

  protected submit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();
    const commentId = this.commentId;
    const reason = this.form.controls.reason.value;
    const details = this.form.controls.details.value.trim();
    if (commentId === null || reason === '' || this.form.invalid || this.detailsInvalid()) return;

    if (!this.connectivity.isOnline()) {
      this.state.set({ status: 'offline', report: null, errorCode: null });
      return;
    }
    const request: CreateContentReportRequest = details
      ? { commentId, reason, details }
      : { commentId, reason };
    const signature = JSON.stringify(request);
    if (this.requestSignature !== signature || this.idempotencyKey === null) {
      this.requestSignature = signature;
      this.idempotencyKey = globalThis.crypto.randomUUID();
    }

    this.state.set({ status: 'saving', report: null, errorCode: null });
    this.api
      .createReport(request, this.idempotencyKey)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (report) => {
          this.requestSignature = null;
          this.idempotencyKey = null;
          this.state.set({ status: 'success', report, errorCode: null });
        },
        error: (error: unknown) => {
          if (!isTransientProblem(error)) {
            this.requestSignature = null;
            this.idempotencyKey = null;
          }
          this.state.set({
            status: isOfflineProblem(error)
              ? 'offline'
              : isDuplicateReport(error)
                ? 'conflict'
                : 'error',
            report: null,
            errorCode: error instanceof ApiProblem ? error.code : null,
          });
        },
      });
  }

  protected detailsInvalid(): boolean {
    return (
      this.submitted &&
      this.form.controls.reason.value === 'Other' &&
      this.form.controls.details.value.trim().length < 10
    );
  }

  protected close(): void {
    if (this.state().status === 'saving') return;
    this.dialog().nativeElement.close();
    this.resetDialog();
  }

  protected cancel(event: Event): void {
    if (this.state().status === 'saving') {
      event.preventDefault();
      return;
    }
    this.resetDialog();
  }

  protected reasonLabel(reason: ContentReportReason): string {
    const labels: Record<ContentReportReason, readonly [string, string]> = {
      Spam: ['محتوى مزعج', 'Spam'],
      Harassment: ['مضايقة', 'Harassment'],
      HateSpeech: ['خطاب كراهية', 'Hate speech'],
      Misinformation: ['معلومات مضللة', 'Misinformation'],
      Copyright: ['حقوق نشر', 'Copyright'],
      PersonalData: ['بيانات شخصية', 'Personal data'],
      Other: ['سبب آخر', 'Other'],
    };
    return labels[reason][this.locale.locale() === 'ar' ? 0 : 1];
  }

  protected errorLabel(code: string | null): string {
    if (this.locale.locale() === 'ar') {
      return code === 'HTTP.408'
        ? 'انتهت مهلة الطلب. يمكنك إعادة المحاولة بأمان.'
        : 'تعذر إرسال البلاغ. تحقق من البيانات وحاول مرة أخرى.';
    }
    return code === 'HTTP.408'
      ? 'The request timed out. It is safe to retry.'
      : 'The report could not be submitted. Check the details and try again.';
  }

  private resetDialog(): void {
    this.commentId = null;
    this.submitted = false;
    this.requestSignature = null;
    this.idempotencyKey = null;
    this.form.reset({ reason: '', details: '' });
    this.state.set({ status: 'idle', report: null, errorCode: null });
  }
}

const isOfflineProblem = (error: unknown): boolean =>
  error instanceof ApiProblem && error.status === 0;

const isDuplicateReport = (error: unknown): boolean =>
  error instanceof ApiProblem && error.status === 409 && error.code === 'REPORT.ALREADY_OPEN';

const isTransientProblem = (error: unknown): boolean =>
  error instanceof ApiProblem && [0, 408, 429, 502, 503, 504].includes(error.status);
