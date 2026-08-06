import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-auth-shell',
  imports: [RouterLink, RouterOutlet],
  template: `
    <div class="auth-layout">
      <header>
        <a [routerLink]="['/', locale.locale()]">{{ locale.copy().brand }}</a>
        <button type="button" (click)="locale.switchLocale()">
          {{ locale.copy().switchLocale }}
        </button>
      </header>
      <main id="main-content" tabindex="-1"><router-outlet /></main>
      <aside aria-label="Platform principles">
        <span>{{ locale.locale() === 'ar' ? 'مسار واضح' : 'A clear path' }}</span>
        <strong>{{
          locale.locale() === 'ar'
            ? 'وقت أقل في البحث. وقت أكثر في التعلم.'
            : 'Less searching. More learning.'
        }}</strong>
      </aside>
    </div>
  `,
  styles: `
    .auth-layout {
      display: grid;
      grid-template-columns: minmax(20rem, 1fr) minmax(18rem, 36rem);
      min-block-size: 100dvh;
      background: var(--color-canvas);
    }
    header {
      position: fixed;
      inset-block-start: 0;
      inset-inline: 0;
      z-index: 2;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--space-5) var(--page-gutter);
    }
    header a {
      color: var(--color-text);
      font-size: 1.25rem;
      font-weight: 700;
      text-decoration: none;
    }
    header button {
      color: var(--color-text);
      background: transparent;
      border: 0;
    }
    main {
      display: grid;
      place-items: center;
      padding: calc(var(--space-12) + 3rem) var(--page-gutter) var(--space-8);
    }
    aside {
      display: grid;
      align-content: end;
      gap: var(--space-3);
      padding: var(--space-12);
      color: #ecfeff;
      background: #0b3c39;
    }
    aside span {
      color: #99f6e4;
    }
    aside strong {
      max-inline-size: 14ch;
      font-size: clamp(2rem, 4vw, 4.5rem);
      line-height: 1.05;
    }
    @media (max-width: 800px) {
      .auth-layout {
        grid-template-columns: 1fr;
      }
      aside {
        display: none;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthShellComponent {
  protected readonly locale = inject(LocaleService);
}
