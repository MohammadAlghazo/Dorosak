import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
@Component({
  selector: 'drs-offline-page',
  imports: [RouterLink],
  template: `<main id="main-content" class="system-page">
    <span aria-hidden="true">↯</span>
    <h1>
      {{ locale.locale() === 'ar' ? 'الاتصال غير متاح الآن.' : 'The network is unavailable.' }}
    </h1>
    <p>{{ locale.copy().offline }}</p>
    <a [routerLink]="['/', locale.locale()]">{{
      locale.locale() === 'ar' ? 'العودة للرئيسية' : 'Return home'
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
      color: var(--color-warning);
      font-size: 5rem;
    }
    h1 {
      font-size: clamp(2.4rem, 6vw, 5rem);
      line-height: 1;
    }
    p {
      max-inline-size: 38rem;
      color: var(--color-muted);
      line-height: 1.8;
    }
    a {
      color: var(--color-link);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OfflinePageComponent {
  protected readonly locale = inject(LocaleService);
}
