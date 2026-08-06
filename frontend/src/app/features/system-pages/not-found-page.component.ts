import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
@Component({
  selector: 'drs-not-found-page',
  imports: [RouterLink],
  template: `<main id="main-content" class="system-page">
    <span>404</span>
    <h1>
      {{
        locale.locale() === 'ar' ? 'هذه الصفحة ليست في المسار.' : 'This page is not on the pathway.'
      }}
    </h1>
    <a [routerLink]="['/', locale.locale()]">{{
      locale.locale() === 'ar' ? 'العودة للبداية' : 'Return home'
    }}</a>
  </main>`,
  styles: `
    .system-page {
      display: grid;
      align-content: center;
      max-inline-size: var(--content);
      min-block-size: 75dvh;
      margin-inline: auto;
      padding: var(--page-gutter);
    }
    span {
      color: var(--color-brand);
      font-size: 5rem;
      font-weight: 700;
    }
    h1 {
      max-inline-size: 14ch;
      font-size: clamp(2.4rem, 6vw, 5rem);
      line-height: 1;
    }
    a {
      margin-block-start: var(--space-5);
      color: var(--color-link);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPageComponent {
  protected readonly locale = inject(LocaleService);
}
