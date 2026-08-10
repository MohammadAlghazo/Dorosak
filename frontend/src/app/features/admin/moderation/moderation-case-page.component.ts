import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type ValidatorFn,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import type {
  ContentReportReason,
  ContentReportTargetKind,
  ModerationActionType,
  ModerationCaseResponse,
  ModerationWorkflowStatus,
} from '../../../core/api/moderation-api.types';
import { LocaleService } from '../../../core/i18n/locale.service';
import { requiredValidator } from '../../auth/auth-form.helpers';
import {
  moderationActionLabel,
  moderationStatusLabel,
  moderationTargetLabel,
  reportReasonLabel,
} from './moderation-copy';
import { ModerationStore } from './moderation.store';

@Component({
  selector: 'drs-moderation-case-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [ModerationStore],
  template: `
    <section
      class="workflow-page workflow-page-wide moderation-case-page"
      aria-labelledby="case-title"
    >
      <a class="back-link" [routerLink]="['../']">
        {{ locale.locale() === 'ar' ? 'العودة إلى الطابور' : 'Back to queue' }}
      </a>

      @switch (store.detail().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جار تحميل القضية…' : 'Loading case…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert state-alert" role="alert">
            <span>
              {{
                locale.locale() === 'ar'
                  ? 'تفاصيل القضية غير متاحة دون اتصال.'
                  : 'Case details are unavailable offline.'
              }}
            </span>
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert state-alert" role="alert">
            <span>
              {{
                locale.locale() === 'ar' ? 'تعذر تحميل القضية.' : 'The case could not be loaded.'
              }}
              @if (store.detail().errorCode) {
                <code>{{ store.detail().errorCode }}</code>
              }
            </span>
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
      }

      @if (store.detail().value; as moderationCase) {
        <header class="workflow-heading case-heading">
          <div>
            <p class="identity-kicker">
              {{ locale.locale() === 'ar' ? 'مراجعة قضية' : 'Case review' }}
            </p>
            <h1 id="case-title">
              {{ targetLabel(moderationCase.case.report.report.targetKind) }}
            </h1>
            <p>
              <code>{{ moderationCase.case.id }}</code>
              ·
              {{ locale.locale() === 'ar' ? 'الإصدار' : 'Version' }}
              {{ moderationCase.case.version }}
            </p>
          </div>
          <span class="status-chip case-status" [attr.data-status]="moderationCase.case.status">
            {{ statusLabel(moderationCase.case.status) }}
          </span>
        </header>

        @if (store.action().status === 'success') {
          <div class="form-success" role="status">
            {{
              locale.locale() === 'ar'
                ? 'تم تسجيل الإجراء وتحديث القضية من الخادم.'
                : 'The action was recorded and the case was updated by the server.'
            }}
          </div>
        }

        <div class="case-layout">
          <article class="workflow-card report-card" aria-labelledby="reported-content-title">
            <div class="workflow-card-heading">
              <div>
                <p class="eyebrow">
                  {{ locale.locale() === 'ar' ? 'البلاغ الأصلي' : 'Original report' }}
                </p>
                <h2 id="reported-content-title">
                  {{ reasonLabel(moderationCase.case.report.report.reason) }}
                </h2>
              </div>
              <span
                class="status-chip"
                [attr.data-status]="moderationCase.case.report.report.status"
              >
                {{ statusLabel(moderationCase.case.report.report.status) }}
              </span>
            </div>
            <dl class="detail-grid">
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'المبلّغ' : 'Reporter' }}</dt>
                <dd>
                  {{ moderationCase.case.report.reporterName }}
                  <code>{{ moderationCase.case.report.reporterUserId }}</code>
                </dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الهدف' : 'Target' }}</dt>
                <dd>
                  {{ targetLabel(moderationCase.case.report.report.targetKind) }}
                  <code>{{ moderationCase.case.report.report.targetId }}</code>
                </dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'تاريخ البلاغ' : 'Reported' }}</dt>
                <dd>
                  <time [attr.datetime]="moderationCase.case.report.report.createdAt">{{
                    formatDate(moderationCase.case.report.report.createdAt)
                  }}</time>
                </dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'آخر تحديث' : 'Updated' }}</dt>
                <dd>
                  <time [attr.datetime]="moderationCase.case.updatedAt">{{
                    formatDate(moderationCase.case.updatedAt)
                  }}</time>
                </dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'المسند إلى' : 'Assigned to' }}</dt>
                <dd>
                  {{
                    moderationCase.case.assignedToName ??
                      (locale.locale() === 'ar' ? 'غير مسندة' : 'Unassigned')
                  }}
                </dd>
              </div>
            </dl>
            @if (moderationCase.case.report.report.details) {
              <div class="report-note">
                <strong>{{ locale.locale() === 'ar' ? 'تفاصيل البلاغ' : 'Report details' }}</strong>
                <p>{{ moderationCase.case.report.report.details }}</p>
              </div>
            }
            <div class="target-preview" aria-labelledby="target-preview-title">
              <div class="workflow-card-heading">
                <div>
                  <p class="eyebrow">
                    {{ locale.locale() === 'ar' ? 'المعاينة الحالية' : 'Current target preview' }}
                  </p>
                  <h3 id="target-preview-title" dir="auto">
                    {{ moderationCase.targetPreview.title }}
                  </h3>
                </div>
                <span class="status-chip" [attr.data-status]="moderationCase.targetPreview.status">
                  {{ moderationCase.targetPreview.status }}
                </span>
              </div>
              @if (moderationCase.targetPreview.body) {
                <p class="target-preview-body" dir="auto">{{ moderationCase.targetPreview.body }}</p>
              } @else {
                <p class="muted">
                  {{ locale.locale() === 'ar' ? 'لا يوجد نص للمعاينة.' : 'No target body is available.' }}
                </p>
              }
              @if (moderationCase.targetPreview.authorName) {
                <p class="muted" dir="auto">
                  {{ locale.locale() === 'ar' ? 'الكاتب:' : 'Author:' }}
                  {{ moderationCase.targetPreview.authorName }}
                </p>
              }
            </div>
            <p class="privacy-note">
              {{
                locale.locale() === 'ar'
                  ? 'اعتمد على المعرّف والحالة وسجل التدقيق. لا تعرض بيانات الهدف خارج صلاحية المراجعة.'
                  : 'Use the identifier, status, and audit trail. Do not expose target data outside the review permission.'
              }}
            </p>
          </article>

          <aside class="workflow-card action-card" aria-labelledby="available-actions-title">
            <p class="eyebrow">{{ locale.locale() === 'ar' ? 'قرار موثق' : 'Audited decision' }}</p>
            <h2 id="available-actions-title">
              {{ locale.locale() === 'ar' ? 'الإجراءات المتاحة' : 'Available actions' }}
            </h2>
            <p class="muted action-requirement">
              {{
                locale.locale() === 'ar'
                  ? 'يتطلب تسجيل أي إجراء جلسة مدير حديثة بالمصادقة الثنائية.'
                  : 'Recording an action requires a recent admin MFA session.'
              }}
            </p>
            @if (actionOptions(moderationCase).length) {
              <div class="action-list">
                @for (action of actionOptions(moderationCase); track action) {
                  <button
                    type="button"
                    class="case-action"
                    [class.case-action-danger]="isDestructive(action)"
                    [disabled]="store.action().status === 'saving'"
                    (click)="openAction(action, actionDialog)"
                  >
                    {{ actionLabel(action) }}
                  </button>
                }
              </div>
            } @else {
              <p class="muted">
                {{
                  locale.locale() === 'ar'
                    ? 'أُغلقت هذه القضية. لا توجد إجراءات أخرى.'
                    : 'This case is closed. No further actions are available.'
                }}
              </p>
            }
          </aside>
        </div>

        <section class="workflow-card audit-timeline" aria-labelledby="audit-title">
          <div class="workflow-card-heading">
            <div>
              <p class="eyebrow">
                {{ locale.locale() === 'ar' ? 'سجل غير قابل للتجاهل' : 'Accountable record' }}
              </p>
              <h2 id="audit-title">
                {{ locale.locale() === 'ar' ? 'سجل الإجراءات' : 'Action history' }}
              </h2>
            </div>
            <span class="muted">{{ moderationCase.actions.length }}</span>
          </div>
          @if (!moderationCase.actions.length) {
            <p class="muted">
              {{
                locale.locale() === 'ar'
                  ? 'لم تُسجل إجراءات بعد.'
                  : 'No actions have been recorded yet.'
              }}
            </p>
          } @else {
            <ol class="audit-list">
              @for (action of moderationCase.actions; track action.id) {
                <li>
                  <div>
                    <strong>{{ actionLabel(action.actionType) }}</strong>
                    <span>{{ action.actorName }} · {{ formatDate(action.createdAt) }}</span>
                  </div>
                  <p>{{ action.reason }}</p>
                </li>
              }
            </ol>
          }
        </section>
      }

      <dialog #actionDialog aria-labelledby="action-dialog-title" (cancel)="cancelAction($event)">
        <form
          class="dialog-content workflow-form"
          [formGroup]="actionForm"
          (ngSubmit)="submitAction()"
          novalidate
        >
          <h2 id="action-dialog-title">
            {{ selectedAction() ? actionLabel(selectedAction()!) : '' }}
          </h2>
          <p class="dialog-warning">
            {{
              locale.locale() === 'ar'
                ? 'لن تتغير القضية في الواجهة قبل تأكيد الخادم. اكتب سبب القرار وسبب التدقيق بوضوح.'
                : 'The case will not change in the UI until the server confirms it. Provide a clear decision and audit reason.'
            }}
          </p>
          <label for="moderation-action-reason">
            {{ locale.locale() === 'ar' ? 'سبب الإجراء' : 'Action reason' }}
          </label>
          <textarea
            id="moderation-action-reason"
            formControlName="reason"
            rows="4"
            minlength="8"
            maxlength="1000"
            [attr.aria-invalid]="
              actionForm.controls.reason.touched && actionForm.controls.reason.invalid
            "
          ></textarea>
          @if (actionForm.controls.reason.touched && actionForm.controls.reason.invalid) {
            <p class="field-error">
              {{
                locale.locale() === 'ar'
                  ? 'اكتب سبباً من 8 أحرف على الأقل.'
                  : 'Enter at least 8 characters.'
              }}
            </p>
          }
          <label for="moderation-audit-reason">
            {{ locale.locale() === 'ar' ? 'سبب التدقيق' : 'Audit reason' }}
          </label>
          <textarea
            id="moderation-audit-reason"
            formControlName="auditReason"
            rows="3"
            minlength="8"
            maxlength="1000"
            aria-describedby="moderation-audit-help"
            [attr.aria-invalid]="
              actionForm.controls.auditReason.touched && actionForm.controls.auditReason.invalid
            "
          ></textarea>
          <small id="moderation-audit-help" class="field-help">
            {{
              locale.locale() === 'ar'
                ? 'يُرسل هذا السبب في X-Audit-Reason ويُسجل مع القرار.'
                : 'This value is sent as X-Audit-Reason and recorded with the decision.'
            }}
          </small>
          @if (actionForm.controls.auditReason.touched && actionForm.controls.auditReason.invalid) {
            <p class="field-error">
              {{
                locale.locale() === 'ar'
                  ? 'سبب التدقيق مطلوب (8 أحرف على الأقل).'
                  : 'Audit reason requires at least 8 characters.'
              }}
            </p>
          }
          <label class="checkbox-label" for="moderation-confirm">
            <input id="moderation-confirm" type="checkbox" formControlName="confirmed" />
            <span>
              {{
                locale.locale() === 'ar'
                  ? 'أؤكد أن هذا الإجراء مقصود ويمكن تدقيقه.'
                  : 'I confirm this action is intentional and auditable.'
              }}
            </span>
          </label>
          @if (store.action().status === 'offline') {
            <p class="field-error" role="alert">
              {{
                locale.locale() === 'ar'
                  ? 'أنت غير متصل. أعد المحاولة عند عودة الاتصال.'
                  : 'You are offline. Retry when connected.'
              }}
            </p>
          } @else if (store.action().status === 'conflict') {
            <div class="conflict-panel" role="alert">
              <h3>{{ locale.locale() === 'ar' ? 'تغيّرت القضية' : 'The case changed' }}</h3>
              <p>
                {{
                  locale.locale() === 'ar'
                    ? 'لم يُطبّق الإجراء على نسخة قديمة. أغلق النافذة وأعد تحميل القضية قبل اتخاذ قرار جديد.'
                    : 'The action was not applied to a stale version. Close this dialog and reload the case before deciding again.'
                }}
              </p>
              @if (store.action().errorCode) {
                <code>{{ store.action().errorCode }}</code>
              }
            </div>
          } @else if (store.action().status === 'error') {
            <p class="field-error" role="alert">
              {{ actionErrorLabel(store.action().errorCode) }}
              @if (store.action().errorCode) {
                <code>{{ store.action().errorCode }}</code>
              }
            </p>
          } @else if (store.action().status === 'success') {
            <p class="form-success" role="status">
              {{ locale.locale() === 'ar' ? 'تم حفظ الإجراء.' : 'Action saved.' }}
            </p>
          }
          <div class="dialog-actions">
            @if (store.action().status === 'conflict') {
              <button
                class="danger-button"
                type="button"
                (click)="reloadFromConflict(actionDialog)"
              >
                {{ locale.locale() === 'ar' ? 'إغلاق وإعادة التحميل' : 'Close and reload' }}
              </button>
            } @else {
              <button
                class="secondary-button"
                type="button"
                [disabled]="store.action().status === 'saving'"
                (click)="closeAction(actionDialog)"
              >
                {{ locale.locale() === 'ar' ? 'إلغاء' : 'Cancel' }}
              </button>
              <button
                class="primary-button"
                type="submit"
                [disabled]="
                  actionForm.invalid ||
                  selectedAction() === null ||
                  store.action().status === 'saving' ||
                  store.action().status === 'success'
                "
              >
                {{
                  store.action().status === 'saving'
                    ? locale.locale() === 'ar'
                      ? 'جار تسجيل الإجراء…'
                      : 'Recording action…'
                    : store.action().status === 'error' || store.action().status === 'offline'
                      ? locale.locale() === 'ar'
                        ? 'إعادة المحاولة'
                        : 'Retry action'
                      : locale.locale() === 'ar'
                        ? 'تأكيد الإجراء'
                        : 'Confirm action'
                }}
              </button>
            }
          </div>
        </form>
      </dialog>
    </section>
  `,
  styles: `
    .case-heading,
    .state-alert {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-4);
    }
    .case-heading {
      align-items: end;
    }
    .case-heading h1 {
      margin-block-end: var(--space-2);
    }
    .case-heading p:last-child {
      margin: 0;
      color: var(--color-muted);
    }
    .case-status {
      font-size: 1rem;
    }
    .case-layout {
      display: grid;
      grid-template-columns: minmax(0, 1.45fr) minmax(16rem, 0.55fr);
      gap: var(--space-5);
    }
    .case-layout .workflow-card,
    .audit-timeline {
      margin-block-end: var(--space-5);
    }
    .report-card h2,
    .action-card h2,
    .audit-timeline h2 {
      margin-block-start: 0;
    }
    .detail-grid dd {
      display: grid;
      gap: var(--space-1);
    }
    .report-note {
      margin-block: var(--space-4);
      padding: var(--space-4);
      background: var(--color-subtle);
      border-inline-start: 4px solid var(--color-brand);
    }
    .report-note p,
    .privacy-note {
      margin-block: var(--space-2) 0;
      white-space: pre-wrap;
    }
    .privacy-note,
    .muted {
      color: var(--color-muted);
    }
    .action-card {
      align-self: start;
    }
    .action-requirement {
      line-height: 1.6;
    }
    .action-list {
      display: grid;
      gap: var(--space-3);
    }
    .case-action {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-block-size: 46px;
      padding-inline: var(--space-3);
      color: var(--color-brand);
      background: transparent;
      border: 1px solid var(--color-brand);
      border-radius: var(--radius-1);
      font-weight: 700;
    }
    .case-action-danger {
      color: var(--color-danger);
      border-color: color-mix(in srgb, var(--color-danger) 65%, var(--color-border));
    }
    .case-action:disabled {
      cursor: wait;
      opacity: 0.55;
    }
    .audit-timeline .workflow-card-heading {
      margin-block-end: var(--space-3);
    }
    .audit-list {
      display: grid;
      gap: var(--space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .audit-list li {
      padding-inline-start: var(--space-4);
      border-inline-start: 3px solid var(--color-brand);
    }
    .audit-list li > div {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      justify-content: space-between;
    }
    .audit-list span {
      color: var(--color-muted);
      font-size: 0.85rem;
    }
    .audit-list p {
      margin-block: var(--space-2) 0;
      white-space: pre-wrap;
    }
    .dialog-warning {
      margin-block: 0 var(--space-2);
      color: var(--color-muted);
      line-height: 1.6;
    }
    .state-alert {
      align-items: start;
    }
    .status-chip[data-status='Open'] {
      color: var(--color-warning);
    }
    .status-chip[data-status='InReview'] {
      color: var(--color-link);
    }
    .status-chip[data-status='Resolved'] {
      color: var(--color-success);
    }
    code {
      direction: ltr;
      unicode-bidi: isolate;
    }
    @media (max-width: 760px) {
      .case-layout {
        grid-template-columns: 1fr;
      }
      .action-card {
        order: -1;
      }
    }
    @media (max-width: 540px) {
      .case-heading,
      .state-alert {
        align-items: stretch;
        flex-direction: column;
      }
      .case-status {
        align-self: start;
      }
      .dialog-actions {
        align-items: stretch;
        flex-direction: column;
      }
    .dialog-actions button {
        inline-size: 100%;
      }
    }
    .target-preview {
      display: grid;
      gap: var(--space-3);
      margin-block-start: var(--space-4);
      padding: var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
      background: var(--color-subtle);
    }
    .target-preview h3,
    .target-preview p {
      margin-block: 0;
    }
    .target-preview-body {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModerationCasePageComponent {
  readonly caseId = input.required<string>();
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(ModerationStore);
  protected readonly selectedAction = signal<ModerationActionType | null>(null);
  protected readonly actionForm = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, trimmedLengthValidator, Validators.maxLength(1000)],
    }),
    auditReason: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, trimmedLengthValidator, Validators.maxLength(1000)],
    }),
    confirmed: new FormControl(false, {
      nonNullable: true,
      validators: [requiredTrueValidator],
    }),
  });
  private actionSignature: string | null = null;
  private actionKey: string | null = null;

  constructor() {
    effect(() => {
      const caseId = this.caseId();
      untracked(() => {
        this.store.loadCase(caseId);
      });
    });
  }

  protected reload(): void {
    this.store.loadCase(this.caseId());
  }

  protected actionOptions(
    moderationCase: ModerationCaseResponse,
  ): readonly ModerationActionType[] {
    if (moderationCase.case.status === 'Open') return ['StartReview'];
    if (moderationCase.case.status === 'InReview') {
      if (moderationCase.case.report.report.targetKind === 'Course' ||
        moderationCase.case.report.report.targetKind === 'ReportedUser') {
        return ['Resolve', 'Dismiss'];
      }
      if (moderationCase.targetPreview.status === 'Published') return ['HideContent', 'Resolve', 'Dismiss'];
      if (moderationCase.targetPreview.status === 'Hidden') return ['RestoreContent', 'Resolve', 'Dismiss'];
      return ['Resolve', 'Dismiss'];
    }
    return [];
  }

  protected openAction(action: ModerationActionType, dialog: HTMLDialogElement): void {
    if (this.store.action().status === 'saving') return;
    this.selectedAction.set(action);
    this.actionForm.reset({ reason: '', auditReason: '', confirmed: false });
    this.actionSignature = null;
    this.actionKey = null;
    this.store.resetAction();
    dialog.showModal();
  }

  protected submitAction(): void {
    const action = this.selectedAction();
    if (action === null) return;
    this.actionForm.markAllAsTouched();
    if (this.actionForm.invalid || this.store.action().status === 'success') return;

    const reason = this.actionForm.controls.reason.value.trim();
    const auditReason = this.actionForm.controls.auditReason.value.trim();
    const expectedVersion = this.store.detail().value?.case.version;
    if (expectedVersion === undefined) return;
    const actionState = this.store.action();
    if (actionState.status === 'error' && !isRetryableActionError(actionState.errorCode)) {
      this.actionSignature = null;
      this.actionKey = null;
    }
    const signature = JSON.stringify([this.caseId(), action, reason, auditReason, expectedVersion]);
    if (this.actionSignature !== signature || this.actionKey === null) {
      this.actionSignature = signature;
      this.actionKey = globalThis.crypto.randomUUID();
    }
    this.store.applyAction(
      this.caseId(),
      { action, reason, expectedVersion },
      this.actionKey,
      auditReason,
    );
  }

  protected closeAction(dialog: HTMLDialogElement): void {
    if (this.store.action().status === 'saving') return;
    dialog.close();
    this.selectedAction.set(null);
    this.actionSignature = null;
    this.actionKey = null;
    this.store.resetAction();
  }

  protected reloadFromConflict(dialog: HTMLDialogElement): void {
    this.closeAction(dialog);
    this.reload();
  }

  protected cancelAction(event: Event): void {
    if (this.store.action().status === 'saving') {
      event.preventDefault();
      return;
    }
    this.selectedAction.set(null);
    this.actionSignature = null;
    this.actionKey = null;
    this.store.resetAction();
  }

  protected isDestructive(action: ModerationActionType): boolean {
    return action === 'HideContent' || action === 'Resolve' || action === 'Dismiss';
  }

  protected actionLabel(action: ModerationActionType): string {
    return moderationActionLabel(action, this.locale.locale());
  }

  protected statusLabel(status: ModerationWorkflowStatus): string {
    return moderationStatusLabel(status, this.locale.locale());
  }

  protected targetLabel(target: ContentReportTargetKind): string {
    return moderationTargetLabel(target, this.locale.locale());
  }

  protected reasonLabel(reason: ContentReportReason): string {
    return reportReasonLabel(reason, this.locale.locale());
  }

  protected actionErrorLabel(code: string | null): string {
    if (this.locale.locale() === 'ar') {
      if (code === 'MODERATION.TARGET_NOT_FOUND') return 'لم يعد الهدف موجوداً.';
      if (code === 'AUTH.FORBIDDEN') return 'يلزم حساب مدير وجلسة مصادقة ثنائية حديثة.';
      return 'تعذر تسجيل الإجراء. تحقق من البيانات وحاول مرة أخرى.';
    }
    if (code === 'MODERATION.TARGET_NOT_FOUND') return 'The reported target no longer exists.';
    if (code === 'AUTH.FORBIDDEN') return 'An admin account with a recent MFA session is required.';
    return 'The action could not be recorded. Check the details and try again.';
  }

  protected formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.valueOf())) return '';
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(date);
  }
}

const requiredTrueValidator: ValidatorFn = (control) => Validators.requiredTrue(control);

const trimmedLengthValidator: ValidatorFn = (control) =>
  typeof control.value === 'string' && control.value.trim().length >= 8
    ? null
    : { trimmedMinLength: true };

const isRetryableActionError = (code: string | null): boolean =>
  code === null || code === 'HTTP.408' || code === 'HTTP.429' || code === 'NETWORK.OFFLINE';
