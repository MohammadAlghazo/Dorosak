import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-catalog-page',
  imports: [RouterLink],
  template: `
    <section class="content-page">
      <p class="eyebrow">{{ locale.locale() === 'ar' ? 'الكتالوج العام' : 'Public catalog' }}</p>
      <h1>
        {{
          locale.locale() === 'ar'
            ? 'اختر نتيجة، لا مجرد عنوان.'
            : 'Choose an outcome, not just a title.'
        }}
      </h1>
      <p>
        {{
          locale.locale() === 'ar'
            ? 'هذه بيانات تمهيدية إلى أن تصل عقود الكتالوج في Phase 6.'
            : 'Foundation content remains in place until catalog contracts arrive in Phase 6.'
        }}
      </p>
      <div class="catalog-lines">
        @for (course of courses; track course.slug) {
          <a [routerLink]="['/', locale.locale(), 'courses', course.slug]"
            ><strong>{{ course.title }}</strong
            ><span>{{ course.duration }}</span
            ><i aria-hidden="true">↗</i></a
          >
        }
      </div>
    </section>
  `,
  styles: `
    .content-page {
      max-inline-size: var(--content-wide);
      min-block-size: 70dvh;
      margin-inline: auto;
      padding: var(--space-12) var(--page-gutter);
    }
    .eyebrow {
      color: var(--color-brand);
      font-weight: 700;
    }
    .content-page > h1 {
      max-inline-size: 16ch;
      font-size: clamp(2.5rem, 6vw, 5rem);
      line-height: 1;
    }
    .content-page > p {
      max-inline-size: 45rem;
      color: var(--color-muted);
      line-height: 1.8;
    }
    .catalog-lines {
      margin-block-start: var(--space-10);
      border-block-start: 1px solid var(--color-border);
    }
    .catalog-lines a {
      display: grid;
      grid-template-columns: 1fr auto 48px;
      align-items: center;
      gap: var(--space-4);
      min-block-size: 82px;
      color: var(--color-text);
      border-block-end: 1px solid var(--color-border);
      text-decoration: none;
    }
    .catalog-lines span {
      color: var(--color-muted);
    }
    .catalog-lines i {
      display: grid;
      place-items: center;
      inline-size: 44px;
      block-size: 44px;
      border: 1px solid var(--color-border);
      border-radius: 50%;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly courses = [
    { slug: 'web', title: 'Web systems', duration: '12 weeks' },
    { slug: 'data', title: 'Data reasoning', duration: '8 weeks' },
    { slug: 'business', title: 'Practical communication', duration: '6 weeks' },
  ] as const;
}
