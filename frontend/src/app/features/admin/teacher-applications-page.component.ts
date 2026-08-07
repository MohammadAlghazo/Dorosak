import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { AdminPhase6Store } from './admin-phase6.store';

type TeacherDecision = 'start' | 'approve' | 'reject';

@Component({
  selector: 'drs-teacher-applications-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdminPhase6Store],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="teacher-review-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'مراجعة عالية الحساسية' : 'High-risk review' }}
        </p>
        <h1 id="teacher-review-title">
          {{ locale.locale() === 'ar' ? 'طلبات المدرسين' : 'Teacher applications' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'تتطلب هذه المساحة جلسة مدير حديثة بالمصادقة الثنائية وسبب تدقيق واضح.'
              : 'This workspace requires a recent admin MFA session and a clear audit reason.'
          }}
        </p>
      </header>

      <form
        class="audit-reason-form workflow-card"
        [formGroup]="auditForm"
        (ngSubmit)="load()"
        novalidate
      >
        <label for="audit-reason">{{
          locale.locale() === 'ar' ? 'سبب الوصول للتدقيق' : 'Audit access reason'
        }}</label>
        <div class="inline-control">
          <input
            id="audit-reason"
            formControlName="auditReason"
            minlength="8"
            maxlength="500"
            [attr.aria-invalid]="
              auditForm.controls.auditReason.touched && auditForm.controls.auditReason.invalid
            "
          />
          <button
            class="primary-button"
            type="submit"
            [disabled]="store.teacherApplications().status === 'loading'"
          >
            {{ locale.locale() === 'ar' ? 'تحميل الطلبات' : 'Load applications' }}
          </button>
        </div>
        @if (auditForm.controls.auditReason.touched && auditForm.controls.auditReason.invalid) {
          <p class="field-error">
            {{
              locale.locale() === 'ar'
                ? 'اكتب سببًا من 8 أحرف على الأقل.'
                : 'Enter at least 8 characters.'
            }}
          </p>
        }
      </form>

      @switch (store.teacherApplications().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ التحميل…' : 'Loading…' }}
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            {{ locale.locale() === 'ar' ? 'لا توجد طلبات.' : 'There are no applications.' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar'
                ? 'تعذر الوصول. تحقق من المصادقة الثنائية الحديثة وسبب التدقيق.'
                : 'Access failed. Check recent MFA and the audit reason.'
            }}
            @if (store.teacherApplications().errorCode) {
              <code>{{ store.teacherApplications().errorCode }}</code>
            }
          </div>
        }
        @case ('saving') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تسجيل القرار…' : 'Recording decision…' }}
          </div>
        }
      }

      <div class="review-list">
        @for (application of store.teacherApplications().items; track application.id) {
          <article class="review-card">
            <div class="workflow-card-heading">
              <div>
                <p class="eyebrow">
                  <code>{{ application.userId }}</code>
                </p>
                <h2>{{ application.headline }}</h2>
              </div>
              <span class="status-chip">{{ application.status }}</span>
            </div>
            <dl class="detail-grid">
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الخبرة' : 'Expertise' }}</dt>
                <dd>{{ application.expertise }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'السيرة' : 'Biography' }}</dt>
                <dd>{{ application.biography }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الدافع' : 'Motivation' }}</dt>
                <dd>{{ application.motivation }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'تاريخ التقديم' : 'Submitted' }}</dt>
                <dd>{{ formatDate(application.submittedAt) }}</dd>
              </div>
            </dl>
            @if (application.reviewerReason) {
              <div class="review-note">
                <strong>{{ locale.locale() === 'ar' ? 'سبب المراجع' : 'Reviewer reason' }}</strong>
                <p>{{ application.reviewerReason }}</p>
              </div>
            }
            @if (application.status === 'Pending') {
              <button
                class="primary-button"
                type="button"
                (click)="openReview(application.id, 'start', reviewDialog)"
              >
                {{ locale.locale() === 'ar' ? 'بدء المراجعة' : 'Start review' }}
              </button>
            }
            @if (application.status === 'InReview') {
              <div class="action-row">
                <button
                  class="primary-button"
                  type="button"
                  (click)="openReview(application.id, 'approve', reviewDialog)"
                >
                  {{ locale.locale() === 'ar' ? 'قبول' : 'Approve' }}
                </button>
                <button
                  class="danger-button"
                  type="button"
                  (click)="openReview(application.id, 'reject', reviewDialog)"
                >
                  {{ locale.locale() === 'ar' ? 'رفض' : 'Reject' }}
                </button>
              </div>
            }
          </article>
        }
      </div>

      @if (store.teacherApplications().hasMore) {
        <button class="secondary-button load-more" type="button" (click)="loadMore()">
          {{ locale.locale() === 'ar' ? 'عرض المزيد' : 'Load more' }}
        </button>
      }
    </section>

    <dialog #reviewDialog aria-labelledby="teacher-decision-title">
      <form
        class="dialog-content workflow-form"
        [formGroup]="reviewForm"
        (ngSubmit)="review(reviewDialog)"
        novalidate
      >
        <h2 id="teacher-decision-title">{{ decisionLabel() }}</h2>
        <label for="teacher-review-reason">{{
          locale.locale() === 'ar' ? 'سبب القرار' : 'Decision reason'
        }}</label>
        <textarea
          id="teacher-review-reason"
          formControlName="reason"
          rows="5"
          maxlength="2000"
        ></textarea>
        @if (reviewError()) {
          <p class="field-error" role="alert">{{ reviewError() }}</p>
        }
        <div class="dialog-actions">
          <button class="secondary-button" type="button" (click)="reviewDialog.close()">
            {{ locale.locale() === 'ar' ? 'إلغاء' : 'Cancel' }}
          </button>
          <button class="primary-button" type="submit">
            {{ locale.locale() === 'ar' ? 'تأكيد القرار' : 'Confirm decision' }}
          </button>
        </div>
      </form>
    </dialog>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeacherApplicationsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdminPhase6Store);
  protected readonly selectedApplication = signal<string | null>(null);
  protected readonly decision = signal<TeacherDecision>('start');
  protected readonly reviewError = signal<string | null>(null);
  protected readonly auditForm = new FormGroup({
    auditReason: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.minLength(8), Validators.maxLength(500)],
    }),
  });
  protected readonly reviewForm = new FormGroup({
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  protected load(): void {
    this.auditForm.markAllAsTouched();
    if (this.auditForm.invalid) return;
    this.store.loadTeacherApplications(this.auditForm.controls.auditReason.value);
  }

  protected loadMore(): void {
    const cursor = this.store.teacherApplications().nextCursor;
    if (cursor !== null)
      this.store.loadTeacherApplications(this.auditForm.controls.auditReason.value, cursor);
  }

  protected openReview(
    applicationId: string,
    decision: TeacherDecision,
    dialog: HTMLDialogElement,
  ): void {
    this.selectedApplication.set(applicationId);
    this.decision.set(decision);
    this.reviewForm.reset();
    this.reviewError.set(null);
    dialog.showModal();
  }

  protected review(dialog: HTMLDialogElement): void {
    const applicationId = this.selectedApplication();
    const reason = this.reviewForm.controls.reason.value.trim();
    if (applicationId === null) return;
    if (this.decision() === 'reject' && reason.length === 0) {
      this.reviewError.set(
        this.locale.locale() === 'ar' ? 'سبب الرفض مطلوب.' : 'A rejection reason is required.',
      );
      return;
    }
    this.store.reviewTeacherApplication(
      applicationId,
      this.decision(),
      reason.length === 0 ? null : reason,
    );
    dialog.close();
  }

  protected decisionLabel(): string {
    const labels: Record<TeacherDecision, readonly [string, string]> = {
      start: ['بدء مراجعة الطلب', 'Start application review'],
      approve: ['قبول طلب المدرس', 'Approve teacher application'],
      reject: ['رفض طلب المدرس', 'Reject teacher application'],
    };
    return labels[this.decision()][this.locale.locale() === 'ar' ? 0 : 1];
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(
      new Date(value),
    );
  }
}
