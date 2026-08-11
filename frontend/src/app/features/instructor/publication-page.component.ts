import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { AdminPhase6ApiClient } from '../../core/api/admin-phase6-api.client';
import type { CourseRelease } from '../../core/api/learning-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { CourseEditorStore } from './course-editor.store';

@Component({
  selector: 'drs-publication-page',
  imports: [FormsModule, RouterLink],
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
                ? 'بعد الموافقة يستطيع المسؤول إنشاء إصدار عام ثابت أو إلغاء نشره.'
                : 'After approval, an administrator can create or unpublish an immutable public release.'
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
          <a [routerLink]="['../media']">{{ locale.locale() === 'ar' ? 'الوسائط' : 'Media' }}</a>
          @if (session.hasPermission('Announcement.ManageCourse')) {
            <a [routerLink]="['../announcements']">{{
              locale.locale() === 'ar' ? 'الإعلانات' : 'Announcements'
            }}</a>
          }
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
          @if (session.hasPermission('Course.PublishAny')) {
            <section class="release-gate" aria-labelledby="release-gate-title">
              <p class="eyebrow">ADMIN RELEASE GATE</p>
              <h3 id="release-gate-title">
                {{ locale.locale() === 'ar' ? 'تفعيل الإصدار العام' : 'Activate public release' }}
              </h3>
              <p>
                {{
                  locale.locale() === 'ar'
                    ? 'تتطلب العملية MFA ودخولاً حديثاً وسبب تدقيق واضحاً.'
                    : 'This operation requires MFA, recent authentication, and a clear audit reason.'
                }}
              </p>
              <label>
                {{ locale.locale() === 'ar' ? 'سبب التدقيق' : 'Audit reason' }}
                <textarea
                  [(ngModel)]="auditReason"
                  rows="3"
                  minlength="8"
                  maxlength="1000"
                  [placeholder]="
                    locale.locale() === 'ar'
                      ? 'مثال: اكتملت مراجعة المحتوى والوسائط'
                      : 'Example: content and media review completed'
                  "
                ></textarea>
              </label>
              <div class="action-row">
                @if (publication.courseStatus !== 'Published') {
                  <button
                    class="primary-button"
                    type="button"
                    [disabled]="releaseBusy() || auditReason.trim().length < 8"
                    (click)="activateRelease()"
                  >
                    {{ locale.locale() === 'ar' ? 'انشر الإصدار' : 'Publish release' }}
                  </button>
                } @else {
                  <button
                    class="danger-button"
                    type="button"
                    [disabled]="releaseBusy() || auditReason.trim().length < 8"
                    (click)="unpublishRelease()"
                  >
                    {{ locale.locale() === 'ar' ? 'ألغ النشر' : 'Unpublish' }}
                  </button>
                }
              </div>
              @if (releaseResult(); as release) {
                <dl class="detail-grid release-result">
                  <div>
                    <dt>{{ locale.locale() === 'ar' ? 'الحالة' : 'State' }}</dt>
                    <dd>{{ release.state }}</dd>
                  </div>
                  <div>
                    <dt>{{ locale.locale() === 'ar' ? 'رقم الإصدار' : 'Release number' }}</dt>
                    <dd>#{{ release.releaseNumber }}</dd>
                  </div>
                  <div>
                    <dt>SHA-256</dt>
                    <dd>
                      <code>{{ release.manifestHash.slice(0, 16) }}…</code>
                    </dd>
                  </div>
                </dl>
              }
              @if (releaseError()) {
                <p class="form-alert" role="alert">
                  <code>{{ releaseError() }}</code>
                </p>
              }
            </section>
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
  private readonly adminApi = inject(AdminPhase6ApiClient);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly courseId = routeCourseId(this.route);
  protected readonly releaseBusy = signal(false);
  protected readonly releaseResult = signal<CourseRelease | null>(null);
  protected readonly releaseError = signal<string | null>(null);
  protected auditReason = '';

  constructor() {
    this.store.loadPublication(this.courseId);
  }

  protected reload(): void {
    this.store.loadPublication(this.courseId);
  }

  protected activateRelease(): void {
    this.mutateRelease('publish');
  }

  protected unpublishRelease(): void {
    this.mutateRelease('unpublish');
  }

  private mutateRelease(operation: 'publish' | 'unpublish'): void {
    const reason = this.auditReason.trim();
    if (reason.length < 8 || this.releaseBusy()) return;
    this.releaseBusy.set(true);
    this.releaseError.set(null);
    const request =
      operation === 'publish'
        ? this.adminApi.publishCourse(this.courseId, reason)
        : this.adminApi.unpublishCourse(this.courseId, reason);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (release) => {
        this.releaseBusy.set(false);
        this.releaseResult.set(release);
        this.store.loadPublication(this.courseId);
      },
      error: (error: unknown) => {
        this.releaseBusy.set(false);
        this.releaseError.set(error instanceof ApiProblem ? error.code : 'PUBLICATION.FAILED');
      },
    });
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
