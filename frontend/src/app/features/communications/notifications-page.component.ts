import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { CommunicationNotification } from '../../core/api/communications-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { NotificationsStore } from './notifications.store';

@Component({
  selector: 'drs-notifications-page',
  imports: [RouterLink],
  providers: [NotificationsStore],
  template: `
    <section class="communications-page" aria-labelledby="notifications-title">
      <header class="communications-heading">
        <div>
          <p class="identity-kicker">{{ copy().kicker }}</p>
          <h1 id="notifications-title">{{ locale.copy().notifications }}</h1>
          <p>{{ copy().intro }}</p>
        </div>
        <button
          class="secondary-button mark-all"
          type="button"
          [disabled]="store.state().unreadCount === 0 || store.mutation().markingAll"
          (click)="store.markAllRead()"
        >
          {{ store.mutation().markingAll ? copy().markingAll : copy().markAll }}
        </button>
      </header>

      <p class="notification-summary" aria-live="polite">
        <strong>{{ formatNumber(store.state().unreadCount) }}</strong> {{ copy().unread }}
      </p>

      @if (store.mutation().errorCode) {
        <div class="form-alert" role="alert">
          {{ copy().mutationFailed }} <code>{{ store.mutation().errorCode }}</code>
        </div>
      }

      @switch (store.state().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">{{ copy().loading }}</div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ copy().offline }}
            <button class="text-button" type="button" (click)="store.retry()">
              {{ copy().retry }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ copy().loadFailed }}
            @if (store.state().errorCode) {
              <code>{{ store.state().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.retry()">
              {{ copy().retry }}
            </button>
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            <h2>{{ copy().emptyTitle }}</h2>
            <p>{{ copy().emptyBody }}</p>
          </div>
        }
      }

      @if (store.state().items.length > 0) {
        <ol class="notification-list" [attr.aria-label]="locale.copy().notifications">
          @for (notification of store.state().items; track notification.id) {
            <li [class.unread]="!notification.isRead">
              <a
                class="notification-link"
                [routerLink]="notificationLink(notification)"
                (click)="store.markRead(notification.id)"
              >
                <span class="notification-type">{{ typeLabel(notification) }}</span>
                <strong dir="auto">{{ notification.title || fallbackTitle(notification) }}</strong>
                @if (notification.body) {
                  <span class="notification-body" dir="auto">{{ notification.body }}</span>
                }
                <time [attr.datetime]="notification.createdAt">{{
                  formatDate(notification.createdAt)
                }}</time>
              </a>
              @if (!notification.isRead) {
                <button
                  class="read-button"
                  type="button"
                  [disabled]="store.mutation().pendingReadIds.has(notification.id)"
                  [attr.aria-label]="
                    copy().markOne + ': ' + (notification.title || fallbackTitle(notification))
                  "
                  (click)="store.markRead(notification.id)"
                >
                  {{ copy().markOne }}
                </button>
              }
            </li>
          }
        </ol>
      }

      @if (store.state().hasMore) {
        <button
          class="secondary-button load-more"
          type="button"
          [disabled]="store.state().status === 'loadingMore'"
          (click)="store.loadMore()"
        >
          {{ store.state().status === 'loadingMore' ? copy().loadingMore : copy().loadMore }}
        </button>
      }
    </section>
  `,
  styles: `
    .communications-page {
      inline-size: min(100%, 58rem);
      margin-inline: auto;
    }
    .communications-heading {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-5);
      margin-block-end: var(--space-4);
    }
    h1 {
      margin-block: var(--space-2);
      font-size: clamp(2.2rem, 6vw, 4rem);
      line-height: 1;
    }
    .communications-heading p:last-child,
    .notification-summary {
      color: var(--color-muted);
    }
    .mark-all {
      flex: none;
      margin: 0;
    }
    .notification-summary {
      padding-block: var(--space-3);
      border-block: 1px solid var(--color-border);
    }
    .notification-list {
      display: grid;
      gap: var(--space-3);
      margin: var(--space-5) 0;
      padding: 0;
      list-style: none;
    }
    li {
      position: relative;
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      min-inline-size: 0;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
      overflow: hidden;
    }
    li.unread {
      border-inline-start: 5px solid var(--color-brand);
    }
    .notification-link {
      display: grid;
      gap: var(--space-2);
      min-inline-size: 0;
      padding: var(--space-5);
      color: var(--color-text);
      text-decoration: none;
    }
    .notification-link:hover {
      background: var(--color-subtle);
    }
    .notification-type {
      color: var(--color-brand);
      font-size: 0.78rem;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
    }
    .notification-body,
    time {
      color: var(--color-muted);
    }
    .notification-body {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    time {
      font-size: 0.82rem;
    }
    .read-button {
      min-block-size: 44px;
      margin-inline-end: var(--space-4);
      padding-inline: var(--space-3);
      color: var(--color-link);
      background: transparent;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
    }
    @media (max-width: 560px) {
      .communications-heading {
        align-items: stretch;
        flex-direction: column;
      }
      .mark-all {
        inline-size: 100%;
      }
      li {
        grid-template-columns: minmax(0, 1fr);
      }
      .read-button {
        inline-size: calc(100% - 2 * var(--space-4));
        margin: 0 var(--space-4) var(--space-4);
      }
      .notification-link {
        padding: var(--space-4);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(NotificationsStore);
  private readonly session = inject(SessionStore);

  constructor() {
    this.store.load();
  }

  protected copy(): (typeof notificationCopy)[keyof typeof notificationCopy] {
    return notificationCopy[this.locale.locale()];
  }

  protected notificationLink(notification: CommunicationNotification): readonly string[] {
    if (notification.type === 'Message' && notification.conversationId !== null) {
      return ['/', this.locale.locale(), 'chat', notification.conversationId];
    }
    if (
      notification.type === 'Announcement' &&
      notification.courseId !== null &&
      this.session.hasPermission('Announcement.ManageCourse')
    ) {
      return ['/', this.locale.locale(), 'instructor', notification.courseId, 'announcements'];
    }
    return ['/', this.locale.locale(), 'my-learning'];
  }

  protected typeLabel(notification: CommunicationNotification): string {
    return notification.type === 'Message' ? this.copy().message : this.copy().announcement;
  }

  protected fallbackTitle(notification: CommunicationNotification): string {
    return notification.type === 'Message' ? this.copy().newMessage : this.copy().newAnnouncement;
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  protected formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale.locale()).format(value);
  }
}

const notificationCopy = {
  ar: {
    kicker: 'مركز المتابعة',
    intro: 'تصل التحديثات هنا بالترتيب الأحدث، وتُستعاد أي فجوة من الخادم.',
    unread: 'غير مقروء',
    markAll: 'تحديد الكل كمقروء',
    markingAll: 'جارٍ التحديد…',
    markOne: 'تحديد كمقروء',
    loading: 'جارٍ تحميل الإشعارات…',
    loadingMore: 'جارٍ تحميل المزيد…',
    loadMore: 'إشعارات أقدم',
    retry: 'إعادة المحاولة',
    offline: 'لا يمكن تحديث الإشعارات أثناء عدم الاتصال.',
    loadFailed: 'تعذر تحميل الإشعارات.',
    mutationFailed: 'لم يُحفظ تغيير حالة القراءة، وأُعيدت الحالة السابقة.',
    emptyTitle: 'لا إشعارات بعد',
    emptyBody: 'ستظهر الرسائل وإعلانات الدورات الجديدة هنا.',
    message: 'رسالة',
    announcement: 'إعلان',
    newMessage: 'رسالة جديدة',
    newAnnouncement: 'إعلان دورة جديد',
  },
  en: {
    kicker: 'Activity desk',
    intro: 'Updates arrive newest first, with any missed range restored from the server.',
    unread: 'unread',
    markAll: 'Mark all as read',
    markingAll: 'Marking all…',
    markOne: 'Mark as read',
    loading: 'Loading notifications…',
    loadingMore: 'Loading more…',
    loadMore: 'Older notifications',
    retry: 'Retry',
    offline: 'Notifications cannot be refreshed while offline.',
    loadFailed: 'Notifications could not be loaded.',
    mutationFailed: 'The read state was not saved, so the previous state was restored.',
    emptyTitle: 'No notifications yet',
    emptyBody: 'New messages and course announcements will appear here.',
    message: 'Message',
    announcement: 'Announcement',
    newMessage: 'New message',
    newAnnouncement: 'New course announcement',
  },
} as const;
