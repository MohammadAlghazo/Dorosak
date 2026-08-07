import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { CourseEditorStore } from './course-editor.store';

@Component({
  selector: 'drs-publication-page',
  imports: [RouterLink],
  template: `
    <section class="workflow-page" aria-labelledby="publication-title">
      <a class="back-link" [routerLink]="['../']">
        {{ locale.locale() === 'ar' ? 'العودة إلى بيانات الدورة' : 'Back to course metadata' }}
      </a>
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">
            {{ locale.locale() === 'ar' ? 'بوابة المراجعة' : 'Review gate' }}
          </p>
          <h1 id="publication-title">
            {{ locale.locale() === 'ar' ? 'حالة النشر' : 'Publication status' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'الموافقة تجعل الدورة جاهزة للنشر، ولا تنشر إصدارًا عامًا في هذه المرحلة.'
                : 'Approval marks the course ready to publish; it does not create a public release in this phase.'
            }}
          </p>
        </div>
        <nav
          class="section-tabs"
          [attr.aria-label]="locale.locale() === 'ar' ? 'أقسام المسودة' : 'Draft sections'"
        >
          <a [routerLink]="['../']">{{ locale.locale() === 'ar' ? 'البيانات' : 'Metadata' }}</a>
          <a [routerLink]="['../curriculum']">{{
            locale.locale() === 'ar' ? 'المنهج' : 'Curriculum'
          }}</a>
          <a [routerLink]="['../publication']" aria-current="page">{{
            locale.locale() === 'ar' ? 'النشر' : 'Publication'
          }}</a>
        </nav>
      </header>

      @switch (store.publication().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل الحالة…' : 'Loading status…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar'
                ? 'تعذر تحميل حالة النشر.'
                : 'Publication status could not be loaded.'
            }}
            @if (store.publication().errorCode) {
              <code>{{ store.publication().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
      }

      @if (store.publication().value; as publication) {
        <article class="workflow-card publication-card">
          <div class="workflow-card-heading">
            <div>
              <p class="eyebrow">{{ locale.locale() === 'ar' ? 'الدورة' : 'Course' }}</p>
              <h2>{{ publication.courseStatus }}</h2>
            </div>
            <span
              class="status-chip"
              [class.status-chip-active]="publication.courseStatus === 'ReadyToPublish'"
            >
              {{
                publication.reviewStatus ??
                  (locale.locale() === 'ar' ? 'لم تُرسل' : 'Not submitted')
              }}
            </span>
          </div>
          <dl class="detail-grid">
            <div>
              <dt>{{ locale.locale() === 'ar' ? 'نسخة المسودة' : 'Draft version' }}</dt>
              <dd>v{{ publication.draftVersion }}</dd>
            </div>
            @if (publication.reviewId) {
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'معرّف المراجعة' : 'Review ID' }}</dt>
                <dd>
                  <code>{{ publication.reviewId }}</code>
                </dd>
              </div>
            }
          </dl>
          @if (publication.reviewerReason) {
            <div class="review-note" role="note">
              <strong>{{
                locale.locale() === 'ar' ? 'ملاحظات المراجع' : 'Reviewer feedback'
              }}</strong>
              <p>{{ publication.reviewerReason }}</p>
            </div>
          }
          @if (session.hasPermission('Course.SubmitOwn')) {
            <div class="action-row">
              @if (
                publication.courseStatus === 'Draft' ||
                publication.courseStatus === 'ChangesRequested'
              ) {
                <button
                  class="primary-button"
                  type="button"
                  [disabled]="store.publication().status === 'saving'"
                  (click)="store.requestPublication(courseId)"
                >
                  {{ locale.locale() === 'ar' ? 'إرسال للمراجعة' : 'Submit for review' }}
                </button>
              }
              @if (publication.courseStatus === 'InReview') {
                <button
                  class="danger-button"
                  type="button"
                  [disabled]="store.publication().status === 'saving'"
                  (click)="store.withdrawPublication(courseId)"
                >
                  {{ locale.locale() === 'ar' ? 'سحب طلب النشر' : 'Withdraw publication request' }}
                </button>
              }
            </div>
          }
          @if (store.publication().status === 'saving') {
            <p role="status">
              {{ locale.locale() === 'ar' ? 'جارٍ تحديث الحالة…' : 'Updating status…' }}
            </p>
          }
        </article>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicationPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(CourseEditorStore);
  protected readonly session = inject(SessionStore);
  private readonly route = inject(ActivatedRoute);
  protected readonly courseId = routeCourseId(this.route);

  constructor() {
    this.store.loadPublication(this.courseId);
  }

  protected reload(): void {
    this.store.loadPublication(this.courseId);
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (value === null || value === undefined) {
    throw new Error('The course route requires a courseId parameter.');
  }
  return value;
};
