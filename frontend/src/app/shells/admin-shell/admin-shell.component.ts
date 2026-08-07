import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-admin-shell',
  imports: [RouterLink, RouterOutlet],
  template: `
    <div class="admin-layout">
      <header>
        <a [routerLink]="['/', locale.locale()]">{{ locale.copy().brand }}</a
        ><strong>Operations</strong>
      </header>
      <aside>
        <nav aria-label="Administration">
          <a [routerLink]="['/', locale.locale(), 'admin']">Overview</a>
          @if (session.hasPermission('TeacherApplication.ReviewAny')) {
            <a [routerLink]="['/', locale.locale(), 'admin', 'teacher-applications']">
              {{ locale.locale() === 'ar' ? 'طلبات المدرسين' : 'Teacher applications' }}
            </a>
          }
          @if (session.hasPermission('Course.ReviewAny')) {
            <a [routerLink]="['/', locale.locale(), 'admin', 'publication-reviews']">
              {{ locale.locale() === 'ar' ? 'مراجعات النشر' : 'Publication reviews' }}
            </a>
          }
          @if (session.hasPermission('Catalog.ManageTaxonomy')) {
            <a [routerLink]="['/', locale.locale(), 'admin', 'taxonomy']">
              {{ locale.locale() === 'ar' ? 'التصنيف' : 'Taxonomy' }}
            </a>
          }
          <span>Audit-ready workspace</span>
        </nav>
      </aside>
      <main id="main-content" tabindex="-1"><router-outlet /></main>
    </div>
  `,
  styles: `
    .admin-layout {
      display: grid;
      grid-template: 56px 1fr / 14rem 1fr;
      min-block-size: 100dvh;
      background: var(--color-canvas);
    }
    header {
      grid-column: 1 / -1;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-inline: var(--space-5);
      color: #fff;
      background: #111827;
    }
    header a {
      color: #99f6e4;
      text-decoration: none;
    }
    aside {
      padding: var(--space-5);
      color: var(--color-muted);
      background: var(--color-surface);
      border-inline-end: 1px solid var(--color-border);
    }
    nav {
      display: grid;
      gap: var(--space-4);
    }
    nav a {
      color: var(--color-text);
    }
    main {
      min-inline-size: 0;
      padding: var(--space-6);
    }
    @media (max-width: 700px) {
      .admin-layout {
        grid-template: auto auto 1fr / 1fr;
      }
      aside {
        border-inline-end: 0;
        border-block-end: 1px solid var(--color-border);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminShellComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly session = inject(SessionStore);
}
