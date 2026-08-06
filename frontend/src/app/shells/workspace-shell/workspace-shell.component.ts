import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-workspace-shell',
  imports: [CdkTrapFocus, RouterLink, RouterOutlet],
  template: `
    <div class="workspace-layout">
      <header>
        <button
          type="button"
          class="menu-button"
          [attr.aria-expanded]="navigationOpen()"
          aria-controls="workspace-navigation"
          (click)="toggleNavigation()"
        >
          ☰
        </button>
        <a class="brand" [routerLink]="['/', locale.locale()]">{{ locale.copy().brand }}</a>
        <span>{{ locale.copy().dashboard }}</span>
      </header>
      @if (navigationOpen()) {
        <button
          class="backdrop"
          type="button"
          aria-label="Close navigation"
          (click)="closeNavigation()"
        ></button>
      }
      <aside
        id="workspace-navigation"
        [class.open]="navigationOpen()"
        cdkTrapFocus
        [cdkTrapFocusAutoCapture]="navigationOpen()"
      >
        <nav aria-label="Workspace navigation">
          <a [routerLink]="['/', locale.locale(), 'dashboard']" (click)="closeNavigation()">{{
            locale.copy().dashboard
          }}</a>
          <a [routerLink]="['/', locale.locale(), 'courses']" (click)="closeNavigation()">{{
            locale.copy().browseCourses
          }}</a>
          <a [routerLink]="['/', locale.locale(), 'instructor']" (click)="closeNavigation()">{{
            locale.locale() === 'ar' ? 'التدريس' : 'Teaching'
          }}</a>
        </nav>
      </aside>
      <main id="main-content" tabindex="-1"><router-outlet /></main>
    </div>
  `,
  styles: `
    .workspace-layout {
      display: grid;
      grid-template: 68px 1fr / 16rem 1fr;
      min-block-size: 100dvh;
      background: var(--color-canvas);
    }
    header {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      gap: var(--space-4);
      padding-inline: var(--page-gutter);
      background: var(--color-surface);
      border-block-end: 1px solid var(--color-border);
    }
    .brand {
      color: var(--color-text);
      font-weight: 700;
      text-decoration: none;
    }
    .menu-button {
      display: none;
      inline-size: 44px;
      block-size: 44px;
    }
    aside {
      padding: var(--space-5);
      background: var(--color-surface);
      border-inline-end: 1px solid var(--color-border);
    }
    nav {
      display: grid;
      gap: var(--space-2);
    }
    nav a {
      min-block-size: 44px;
      padding: var(--space-3);
      color: var(--color-muted);
      text-decoration: none;
      border-radius: var(--radius-2);
    }
    nav a:hover {
      color: var(--color-text);
      background: var(--color-subtle);
    }
    main {
      min-inline-size: 0;
      padding: var(--space-7) var(--page-gutter);
    }
    .backdrop {
      display: none;
    }
    @media (max-width: 800px) {
      .workspace-layout {
        grid-template-columns: 1fr;
      }
      .menu-button {
        display: inline-grid;
        place-items: center;
      }
      aside {
        position: fixed;
        inset-block: 68px 0;
        inset-inline-start: 0;
        z-index: var(--z-overlay);
        inline-size: min(20rem, 88vw);
        transform: translateX(calc((100% + 1rem) * var(--offcanvas-sign)));
        transition: transform var(--motion-normal);
      }
      aside.open {
        transform: translateX(0);
      }
      .backdrop {
        position: fixed;
        inset: 68px 0 0;
        z-index: calc(var(--z-overlay) - 1);
        display: block;
        inline-size: 100%;
        block-size: auto;
        background: rgb(0 0 0 / 45%);
        border: 0;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceShellComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly navigationOpen = signal(false);

  protected toggleNavigation(): void {
    this.navigationOpen.update((open) => !open);
  }
  protected closeNavigation(): void {
    this.navigationOpen.set(false);
  }
}
