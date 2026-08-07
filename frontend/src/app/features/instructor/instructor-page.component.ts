import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { InstructorCoursesStore } from './instructor-courses.store';

@Component({
  selector: 'drs-instructor-page',
  imports: [RouterLink],
  providers: [InstructorCoursesStore],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="course-list-title">
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">
            {{ locale.locale() === 'ar' ? 'مساحة التدريس' : 'Teaching desk' }}
          </p>
          <h1 id="course-list-title">
            {{ locale.locale() === 'ar' ? 'مسودات الدورات' : 'Course drafts' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'نظّم البيانات والمنهج قبل إرسال الدورة للمراجعة.'
                : 'Shape metadata and curriculum before sending a course to review.'
            }}
          </p>
        </div>
        <a class="primary-link" [routerLink]="['/', locale.locale(), 'instructor', 'create']">
          {{ locale.locale() === 'ar' ? 'إنشاء دورة' : 'Create course' }}
        </a>
      </header>

      @switch (store.state().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل المسودات…' : 'Loading drafts…' }}
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            <h2>
              {{ locale.locale() === 'ar' ? 'ابدأ بمسودة واضحة' : 'Start with a clear draft' }}
            </h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'لا توجد دورات في مساحة التدريس بعد.'
                  : 'There are no courses in your teaching workspace yet.'
              }}
            </p>
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
            <button class="text-button" type="button" (click)="store.load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'تعذر تحميل الدورات.' : 'Courses could not be loaded.' }}
            @if (store.state().errorCode) {
              <code>{{ store.state().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
      }

      @if (store.state().items.length > 0) {
        <div class="course-grid" aria-live="polite">
          @for (course of store.state().items; track course.id) {
            <article class="course-draft-card">
              <div class="workflow-card-heading">
                <span class="status-chip">{{ course.status }}</span>
                <span class="version-label">v{{ course.draftVersion }}</span>
              </div>
              <p class="eyebrow">{{ course.defaultLocale.toUpperCase() }}</p>
              <h2>
                {{
                  course.title ?? (locale.locale() === 'ar' ? 'دورة بلا عنوان' : 'Untitled course')
                }}
              </h2>
              <p class="muted">
                {{ locale.locale() === 'ar' ? 'آخر تحديث' : 'Updated' }}
                {{ formatDate(course.updatedAt) }}
              </p>
              <a [routerLink]="['/', locale.locale(), 'instructor', course.id]">
                {{ locale.locale() === 'ar' ? 'فتح المسودة' : 'Open draft' }}
              </a>
            </article>
          }
        </div>
        @if (store.state().hasMore) {
          <button
            class="secondary-button load-more"
            type="button"
            [disabled]="store.state().status === 'loadingMore'"
            (click)="store.loadMore()"
          >
            {{
              store.state().status === 'loadingMore'
                ? locale.locale() === 'ar'
                  ? 'جارٍ التحميل…'
                  : 'Loading…'
                : locale.locale() === 'ar'
                  ? 'عرض المزيد'
                  : 'Load more'
            }}
          </button>
        }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstructorPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(InstructorCoursesStore);

  constructor() {
    this.store.load();
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(
      new Date(value),
    );
  }
}
