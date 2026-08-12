import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { SessionCoordinator } from '../../core/auth/session-coordinator.service';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { NotificationBadgeStore } from '../../features/communications/notification-badge.store';

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
        <span class="identity-name">{{
          session.identity()?.displayName ?? locale.copy().dashboard
        }}</span>
        @if (session.hasPermission('Conversation.ReadOwn')) {
          <a
            class="header-communication-link"
            [routerLink]="['/', locale.locale(), 'chat']"
            [attr.aria-label]="locale.copy().chat"
          >
            {{ locale.copy().chat }}
          </a>
        }
        @if (session.hasPermission('Notification.ReadOwn')) {
          <a
            class="header-communication-link notification-header-link"
            [routerLink]="['/', locale.locale(), 'notifications']"
            [attr.aria-label]="locale.copy().notifications"
          >
            {{ locale.copy().notifications }}
            @if (badge.state().count > 0) {
              <span class="notification-badge" [attr.aria-label]="badgeLabel()">{{
                badgeCount()
              }}</span>
            }
          </a>
        }
        <a
          class="header-security-link"
          [routerLink]="['/', locale.locale(), 'settings', 'security']"
        >
          {{ locale.locale() === 'ar' ? 'الأمان' : 'Security' }}
        </a>
        <button type="button" class="header-logout" (click)="logout()">
          {{ locale.locale() === 'ar' ? 'تسجيل الخروج' : 'Sign out' }}
        </button>
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
          @if (session.hasPermission('Conversation.ReadOwn')) {
            <a [routerLink]="['/', locale.locale(), 'chat']" (click)="closeNavigation()">{{
              locale.copy().chat
            }}</a>
          }
          @if (session.hasPermission('Notification.ReadOwn')) {
            <a
              class="notification-nav-link"
              [routerLink]="['/', locale.locale(), 'notifications']"
              (click)="closeNavigation()"
            >
              {{ locale.copy().notifications }}
              @if (badge.state().count > 0) {
                <span class="notification-badge">{{ badgeCount() }}</span>
              }
            </a>
          }
          @if (session.hasPermission('Certificate.ReadOwn')) {
            <a [routerLink]="['/', locale.locale(), 'certificates']" (click)="closeNavigation()">{{
              locale.locale() === 'ar' ? 'الشهادات' : 'Certificates'
            }}</a>
          }
          <a
            [routerLink]="['/', locale.locale(), 'settings', 'security']"
            (click)="closeNavigation()"
            >{{ locale.locale() === 'ar' ? 'الأمان' : 'Security' }}</a
          >
          <a
            [routerLink]="['/', locale.locale(), 'settings', 'sessions']"
            (click)="closeNavigation()"
            >{{ locale.locale() === 'ar' ? 'الجلسات' : 'Sessions' }}</a
          >
          <a
            [routerLink]="['/', locale.locale(), 'settings', 'teacher-application']"
            (click)="closeNavigation()"
            >{{ locale.locale() === 'ar' ? 'طلب التدريس' : 'Teacher application' }}</a
          >
          @if (session.hasPermission('Subscription.ManageOwn')) {
            <a
              [routerLink]="['/', locale.locale(), 'settings', 'subscription']"
              (click)="closeNavigation()"
              >{{ locale.locale() === 'ar' ? 'الاشتراك التجريبي' : 'Demo subscription' }}</a
            >
          }
          @if (
            session.hasAnyPermission([
              'TeacherApplication.ReviewAny',
              'Course.ReviewAny',
              'Catalog.ManageTaxonomy',
            ])
          ) {
            <a [routerLink]="['/', locale.locale(), 'admin']" (click)="closeNavigation()">{{
              locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
            }}</a>
          }
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
    .identity-name {
      min-inline-size: 0;
      max-inline-size: 14rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .header-communication-link,
    .header-security-link {
      color: var(--color-text);
      text-decoration: none;
      white-space: nowrap;
    }
    .header-communication-link,
    .notification-nav-link {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
    }
    .header-communication-link {
      min-block-size: 44px;
    }
    .notification-badge {
      display: inline-grid;
      place-items: center;
      min-inline-size: 1.35rem;
      block-size: 1.35rem;
      padding-inline: 0.2rem;
      color: var(--color-on-brand);
      background: var(--color-danger);
      border-radius: 999px;
      font-size: 0.72rem;
      font-weight: 700;
      line-height: 1;
    }
    .header-security-link,
    .header-logout {
      margin-inline-start: auto;
    }
    .header-logout {
      min-block-size: 44px;
      padding-inline: var(--space-3);
      color: var(--color-text);
      background: transparent;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
    }
    .header-security-link + .header-logout {
      margin-inline-start: 0;
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
      header {
        gap: var(--space-2);
        padding-inline: var(--space-3);
      }
      .identity-name,
      .header-security-link {
        display: none;
      }
      .header-communication-link {
        position: relative;
        max-inline-size: 2rem;
        overflow: hidden;
        font-size: 0;
      }
      .header-communication-link::before {
        content: '•';
        display: grid;
        place-items: center;
        inline-size: 2rem;
        block-size: 2rem;
        color: var(--color-brand);
        font-size: 1.4rem;
      }
      .notification-header-link .notification-badge {
        position: absolute;
        inset-block-start: 0;
        inset-inline-end: 0;
      }
      #workspace-navigation {
        position: fixed;
        inset-block: 68px 0;
        inset-inline-start: 0;
        z-index: var(--z-overlay);
        display: none;
        inline-size: calc(100% - 2rem);
        max-inline-size: 20rem;
        overflow-x: hidden;
        overflow-y: auto;
      }
      #workspace-navigation.open {
        display: block;
      }
      nav,
      nav a {
        min-inline-size: 0;
        max-inline-size: 100%;
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
  protected readonly session = inject(SessionStore);
  protected readonly badge = inject(NotificationBadgeStore);
  private readonly coordinator = inject(SessionCoordinator);
  private readonly router = inject(Router);
  protected readonly navigationOpen = signal(false);

  protected toggleNavigation(): void {
    this.navigationOpen.update((open) => !open);
  }
  protected closeNavigation(): void {
    this.navigationOpen.set(false);
  }

  protected badgeCount(): string {
    const count = this.badge.state().count;
    return count > 99 ? '99+' : String(count);
  }

  protected badgeLabel(): string {
    return `${this.locale.copy().unreadNotifications}: ${this.badgeCount()}`;
  }

  protected logout(): void {
    this.coordinator.logout().subscribe({
      next: () => {
        void this.router.navigate(['/', this.locale.locale(), 'auth', 'sign-in']);
      },
    });
  }
}
