import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-course-details-page',
  imports: [RouterLink],
  template: `
    <article class="course-detail">
      <a [routerLink]="['/', locale.locale(), 'courses']">← {{ locale.copy().browseCourses }}</a>
      <p>FOUNDATION PATH / {{ slug() }}</p>
      <h1>
        {{
          locale.locale() === 'ar'
            ? 'مسار يُبنى حول النتيجة'
            : 'A pathway organized around an outcome'
        }}
      </h1>
      <div class="outline">
        <strong>01</strong><span>{{ locale.locale() === 'ar' ? 'الأساس' : 'Foundation' }}</span
        ><strong>02</strong><span>{{ locale.locale() === 'ar' ? 'التطبيق' : 'Practice' }}</span
        ><strong>03</strong><span>{{ locale.locale() === 'ar' ? 'المشروع' : 'Project' }}</span>
      </div>
    </article>
  `,
  styles: `
    .course-detail {
      max-inline-size: var(--content);
      min-block-size: 75dvh;
      margin-inline: auto;
      padding: var(--space-12) var(--page-gutter);
    }
    .course-detail > p {
      margin-block-start: var(--space-10);
      color: var(--color-brand);
      font-size: 0.8rem;
    }
    .course-detail h1 {
      max-inline-size: 15ch;
      font-size: clamp(2.5rem, 6vw, 5.5rem);
      line-height: 1;
    }
    .outline {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: var(--space-5);
      margin-block-start: var(--space-10);
      padding: var(--space-6);
      border: 1px solid var(--color-border);
    }
    .outline strong {
      color: var(--color-brand);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseDetailsPageComponent {
  readonly slug = input.required<string>();
  protected readonly locale = inject(LocaleService);
}
