import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { AdminPhase6Store } from './admin-phase6.store';

type PublicationDecision = 'changesRequested' | 'approve';

@Component({
  selector: 'drs-publication-reviews-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdminPhase6Store],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="publication-review-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'جودة المحتوى' : 'Content quality' }}
        </p>
        <h1 id="publication-review-title">
          {{ locale.locale() === 'ar' ? 'مراجعات النشر' : 'Publication reviews' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'راجع نسخة المسودة المطلوبة ثم اعتمدها أو اطلب تغييرات محددة.'
              : 'Review the requested draft version, then approve it or request specific changes.'
          }}
        </p>
      </header>

      @switch (store.publicationReviews().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل المراجعات…' : 'Loading reviews…' }}
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            {{
              locale.locale() === 'ar'
                ? 'لا توجد مراجعات نشر.'
                : 'There are no publication reviews.'
            }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
            <button class="text-button" type="button" (click)="store.loadPublicationReviews()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar' ? 'تعذر تحميل المراجعات.' : 'Reviews could not be loaded.'
            }}
            @if (store.publicationReviews().errorCode) {
              <code>{{ store.publicationReviews().errorCode }}</code>
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
        @for (review of store.publicationReviews().items; track review.id) {
          <article class="review-card">
            <div class="workflow-card-heading">
              <div>
                <p class="eyebrow">
                  {{ locale.locale() === 'ar' ? 'الدورة' : 'Course' }}
                  <code>{{ review.courseId }}</code>
                </p>
                <h2>
                  {{ locale.locale() === 'ar' ? 'نسخة المسودة' : 'Draft version' }} v{{
                    review.draftVersion
                  }}
                </h2>
              </div>
              <span class="status-chip" [class.status-chip-active]="review.status === 'Approved'">{{
                review.status
              }}</span>
            </div>
            <dl class="detail-grid">
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'مقدم الطلب' : 'Requested by' }}</dt>
                <dd>
                  <code>{{ review.requestedByUserId }}</code>
                </dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'تاريخ الطلب' : 'Requested' }}</dt>
                <dd>{{ formatDate(review.requestedAt) }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'المسودة' : 'Draft' }}</dt>
                <dd>
                  <code>{{ review.draftId }}</code>
                </dd>
              </div>
            </dl>
            @if (review.reviewerReason) {
              <div class="review-note">
                <strong>{{
                  locale.locale() === 'ar' ? 'ملاحظات المراجع' : 'Reviewer feedback'
                }}</strong>
                <p>{{ review.reviewerReason }}</p>
              </div>
            }
            @if (review.status === 'Pending') {
              <div class="action-row">
                <button
                  class="primary-button"
                  type="button"
                  (click)="openReview(review.id, 'approve', decisionDialog)"
                >
                  {{ locale.locale() === 'ar' ? 'اعتماد' : 'Approve' }}
                </button>
                <button
                  class="secondary-button"
                  type="button"
                  (click)="openReview(review.id, 'changesRequested', decisionDialog)"
                >
                  {{ locale.locale() === 'ar' ? 'طلب تغييرات' : 'Request changes' }}
                </button>
              </div>
            }
          </article>
        }
      </div>

      @if (store.publicationReviews().hasMore) {
        <button class="secondary-button load-more" type="button" (click)="loadMore()">
          {{ locale.locale() === 'ar' ? 'عرض المزيد' : 'Load more' }}
        </button>
      }
    </section>

    <dialog #decisionDialog aria-labelledby="publication-decision-title">
      <form
        class="dialog-content workflow-form"
        [formGroup]="form"
        (ngSubmit)="review(decisionDialog)"
        novalidate
      >
        <h2 id="publication-decision-title">
          {{
            decision() === 'approve'
              ? locale.locale() === 'ar'
                ? 'اعتماد المسودة'
                : 'Approve draft'
              : locale.locale() === 'ar'
                ? 'طلب تغييرات'
                : 'Request changes'
          }}
        </h2>
        <label for="publication-reason">{{
          locale.locale() === 'ar' ? 'ملاحظات المراجع' : 'Reviewer feedback'
        }}</label>
        <textarea
          id="publication-reason"
          formControlName="reason"
          rows="6"
          maxlength="2000"
        ></textarea>
        @if (reviewError()) {
          <p class="field-error" role="alert">{{ reviewError() }}</p>
        }
        <div class="dialog-actions">
          <button class="secondary-button" type="button" (click)="decisionDialog.close()">
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
export class PublicationReviewsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdminPhase6Store);
  protected readonly selectedReview = signal<string | null>(null);
  protected readonly decision = signal<PublicationDecision>('approve');
  protected readonly reviewError = signal<string | null>(null);
  protected readonly form = new FormGroup({
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  constructor() {
    this.store.loadPublicationReviews();
  }

  protected openReview(
    reviewId: string,
    decision: PublicationDecision,
    dialog: HTMLDialogElement,
  ): void {
    this.selectedReview.set(reviewId);
    this.decision.set(decision);
    this.form.reset();
    this.reviewError.set(null);
    dialog.showModal();
  }

  protected review(dialog: HTMLDialogElement): void {
    const reviewId = this.selectedReview();
    const reason = this.form.controls.reason.value.trim();
    if (reviewId === null) return;
    if (this.decision() === 'changesRequested' && reason.length === 0) {
      this.reviewError.set(
        this.locale.locale() === 'ar'
          ? 'اكتب التغييرات المطلوبة.'
          : 'Describe the required changes.',
      );
      return;
    }
    this.store.reviewPublication(reviewId, this.decision(), reason.length === 0 ? null : reason);
    dialog.close();
  }

  protected loadMore(): void {
    const cursor = this.store.publicationReviews().nextCursor;
    if (cursor !== null) this.store.loadPublicationReviews(cursor);
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(
      new Date(value),
    );
  }
}
