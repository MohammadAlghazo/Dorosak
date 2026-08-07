import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import type { TeacherApplicationStatus } from '../../core/api/phase6-api.types';
import { requiredValidator } from '../auth/auth-form.helpers';
import { TeacherApplicationStore } from './teacher-application.store';

@Component({
  selector: 'drs-teacher-application-page',
  imports: [ReactiveFormsModule],
  providers: [TeacherApplicationStore],
  template: `
    <section class="workflow-page" aria-labelledby="teacher-application-title">
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'ملفك المهني' : 'Your professional profile' }}
        </p>
        <h1 id="teacher-application-title">
          {{ locale.locale() === 'ar' ? 'طلب الانضمام إلى المدرسين' : 'Teacher application' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'قدّم خبرتك ودافعك للتدريس، وتابع حالة المراجعة هنا.'
              : 'Share your expertise and motivation, then follow the review status here.'
          }}
        </p>
      </header>

      @switch (store.state().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل الطلب…' : 'Loading application…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar'
                ? 'تعذر تحميل الطلب دون اتصال.'
                : 'The application could not be loaded while offline.'
            }}
            <button class="text-button" type="button" (click)="store.load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar' ? 'تعذر إكمال الطلب.' : 'The request could not be completed.'
            }}
            @if (store.state().errorCode) {
              <code>{{ store.state().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
      }

      @if (store.state().application; as application) {
        <article class="workflow-card">
          <div class="workflow-card-heading">
            <div>
              <p class="eyebrow">
                {{ locale.locale() === 'ar' ? 'الحالة الحالية' : 'Current status' }}
              </p>
              <h2>{{ statusLabel(application.status) }}</h2>
            </div>
            <span
              class="status-chip"
              [class.status-chip-active]="application.status === 'Approved'"
            >
              {{ statusLabel(application.status) }}
            </span>
          </div>
          <dl class="detail-grid">
            <div>
              <dt>{{ locale.locale() === 'ar' ? 'العنوان المهني' : 'Professional headline' }}</dt>
              <dd>{{ application.headline }}</dd>
            </div>
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
          </dl>
          @if (application.reviewerReason) {
            <div class="review-note" role="note">
              <strong>{{ locale.locale() === 'ar' ? 'ملاحظة المراجع' : 'Reviewer note' }}</strong>
              <p>{{ application.reviewerReason }}</p>
            </div>
          }
          @if (application.status === 'Pending' || application.status === 'InReview') {
            <button class="danger-button" type="button" (click)="openDialog(withdrawDialog)">
              {{ locale.locale() === 'ar' ? 'سحب الطلب' : 'Withdraw application' }}
            </button>
          }
        </article>
      }

      @if (canSubmit()) {
        <article class="workflow-card">
          <h2>{{ locale.locale() === 'ar' ? 'طلب جديد' : 'New application' }}</h2>
          @if (!session.identity()?.emailVerified) {
            <div class="form-alert" role="alert">
              {{
                locale.locale() === 'ar'
                  ? 'يجب تأكيد البريد الإلكتروني قبل الإرسال.'
                  : 'Verify your email address before submitting.'
              }}
            </div>
          }
          <form class="workflow-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <label for="teacher-headline">{{
              locale.locale() === 'ar' ? 'العنوان المهني' : 'Professional headline'
            }}</label>
            <input
              id="teacher-headline"
              formControlName="headline"
              maxlength="160"
              [attr.aria-invalid]="form.controls.headline.touched && form.controls.headline.invalid"
            />

            <label for="teacher-biography">{{
              locale.locale() === 'ar' ? 'السيرة المهنية' : 'Professional biography'
            }}</label>
            <textarea
              id="teacher-biography"
              formControlName="biography"
              rows="6"
              maxlength="4000"
              [attr.aria-invalid]="
                form.controls.biography.touched && form.controls.biography.invalid
              "
            ></textarea>

            <label for="teacher-expertise">{{
              locale.locale() === 'ar' ? 'مجالات الخبرة' : 'Areas of expertise'
            }}</label>
            <textarea
              id="teacher-expertise"
              formControlName="expertise"
              rows="3"
              maxlength="1000"
              [attr.aria-invalid]="
                form.controls.expertise.touched && form.controls.expertise.invalid
              "
            ></textarea>

            <label for="teacher-motivation">{{
              locale.locale() === 'ar' ? 'لماذا تريد التدريس؟' : 'Why do you want to teach?'
            }}</label>
            <textarea
              id="teacher-motivation"
              formControlName="motivation"
              rows="6"
              maxlength="4000"
              [attr.aria-invalid]="
                form.controls.motivation.touched && form.controls.motivation.invalid
              "
            ></textarea>
            @if (form.touched && form.invalid) {
              <p class="field-error" role="alert">
                {{
                  locale.locale() === 'ar'
                    ? 'أكمل جميع الحقول ضمن الحدود الموضحة.'
                    : 'Complete every field within the stated limits.'
                }}
              </p>
            }
            <button
              class="primary-button"
              type="submit"
              [disabled]="
                store.state().status === 'submitting' || !session.identity()?.emailVerified
              "
            >
              {{
                store.state().status === 'submitting'
                  ? locale.locale() === 'ar'
                    ? 'جارٍ الإرسال…'
                    : 'Submitting…'
                  : locale.locale() === 'ar'
                    ? 'إرسال الطلب'
                    : 'Submit application'
              }}
            </button>
          </form>
        </article>
      }
    </section>

    <dialog #withdrawDialog aria-labelledby="withdraw-title">
      <form method="dialog" class="dialog-content">
        <h2 id="withdraw-title">
          {{ locale.locale() === 'ar' ? 'سحب طلب المدرس؟' : 'Withdraw teacher application?' }}
        </h2>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'يمكنك تقديم طلب جديد بعد السحب.'
              : 'You can submit a new application after withdrawal.'
          }}
        </p>
        <div class="dialog-actions">
          <button class="secondary-button" value="cancel">
            {{ locale.locale() === 'ar' ? 'إلغاء' : 'Cancel' }}
          </button>
          <button class="danger-button" value="confirm" (click)="store.withdraw()">
            {{ locale.locale() === 'ar' ? 'تأكيد السحب' : 'Confirm withdrawal' }}
          </button>
        </div>
      </form>
    </dialog>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeacherApplicationPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly session = inject(SessionStore);
  protected readonly store = inject(TeacherApplicationStore);
  protected readonly form = new FormGroup({
    headline: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.minLength(2), Validators.maxLength(160)],
    }),
    biography: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(4000)],
    }),
    expertise: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(1000)],
    }),
    motivation: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(4000)],
    }),
  });

  constructor() {
    this.store.load();
  }

  protected canSubmit(): boolean {
    const application = this.store.state().application;
    return application === null || ['Rejected', 'Withdrawn'].includes(application.status);
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.store.state().status === 'submitting') return;
    const value = this.form.getRawValue();
    this.store.submit({
      headline: value.headline.trim(),
      biography: value.biography.trim(),
      expertise: value.expertise.trim(),
      motivation: value.motivation.trim(),
    });
  }

  protected openDialog(dialog: HTMLDialogElement): void {
    dialog.showModal();
  }

  protected statusLabel(status: TeacherApplicationStatus): string {
    const labels: Record<TeacherApplicationStatus, readonly [string, string]> = {
      Pending: ['قيد الانتظار', 'Pending'],
      InReview: ['قيد المراجعة', 'In review'],
      Approved: ['مقبول', 'Approved'],
      Rejected: ['مرفوض', 'Rejected'],
      Withdrawn: ['مسحوب', 'Withdrawn'],
    };
    return labels[status][this.locale.locale() === 'ar' ? 0 : 1];
  }
}
