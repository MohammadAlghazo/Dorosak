import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { Conversation } from '../../core/api/communications-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { ChatStore } from './chat.store';

@Component({
  selector: 'drs-chat-list-page',
  imports: [RouterLink],
  providers: [ChatStore],
  template: `
    <section class="chat-list-page" aria-labelledby="chat-title">
      <header>
        <p class="identity-kicker">{{ copy().kicker }}</p>
        <h1 id="chat-title">{{ locale.copy().chat }}</h1>
        <p>{{ copy().intro }}</p>
      </header>

      @switch (store.conversations().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">{{ copy().loading }}</div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ copy().offline }}
            <button class="text-button" type="button" (click)="store.loadConversations()">
              {{ copy().retry }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ copy().failed }}
            @if (store.conversations().errorCode) {
              <code>{{ store.conversations().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.loadConversations()">
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

      @if (store.conversations().items.length > 0) {
        <ol class="conversation-list" [attr.aria-label]="locale.copy().chat">
          @for (conversation of store.conversations().items; track conversation.id) {
            <li>
              <a [routerLink]="['./', conversation.id]">
                <span class="avatar" aria-hidden="true">{{ initials(conversation) }}</span>
                <span class="conversation-copy">
                  <strong dir="auto">{{ participantNames(conversation) }}</strong>
                  <span>{{ participantCount(conversation) }}</span>
                </span>
                <span class="conversation-meta">
                  <time [attr.datetime]="conversation.updatedAt">{{
                    formatDate(conversation.updatedAt)
                  }}</time>
                  @if (conversation.lastSequence > 0) {
                    <span>{{ copy().messages }} {{ formatNumber(conversation.lastSequence) }}</span>
                  }
                </span>
              </a>
            </li>
          }
        </ol>
      }

      @if (store.conversations().hasMore) {
        <button
          class="secondary-button load-more"
          type="button"
          [disabled]="store.conversations().status === 'loadingMore'"
          (click)="store.loadMoreConversations()"
        >
          {{ store.conversations().status === 'loadingMore' ? copy().loadingMore : copy().more }}
        </button>
      }
    </section>
  `,
  styles: `
    .chat-list-page {
      inline-size: min(100%, 68rem);
      margin-inline: auto;
    }
    header {
      margin-block-end: var(--space-6);
    }
    h1 {
      margin-block: var(--space-2);
      font-size: clamp(2.4rem, 7vw, 4.8rem);
      line-height: 0.98;
    }
    header p:last-child {
      max-inline-size: 60ch;
      color: var(--color-muted);
    }
    .conversation-list {
      display: grid;
      gap: var(--space-3);
      margin: 0 0 var(--space-5);
      padding: 0;
      list-style: none;
    }
    li {
      min-inline-size: 0;
    }
    li a {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--space-4);
      min-block-size: 84px;
      padding: var(--space-4);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
      text-decoration: none;
    }
    li a:hover {
      border-color: var(--color-brand);
      transform: translateY(-1px);
    }
    .avatar {
      display: grid;
      place-items: center;
      inline-size: 3rem;
      block-size: 3rem;
      color: var(--color-on-brand);
      background: var(--color-brand);
      border-radius: 50%;
      font-weight: 750;
    }
    .conversation-copy,
    .conversation-meta {
      display: grid;
      min-inline-size: 0;
      gap: var(--space-1);
    }
    .conversation-copy strong {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .conversation-copy span,
    .conversation-meta {
      color: var(--color-muted);
      font-size: 0.84rem;
    }
    .conversation-meta {
      justify-items: end;
    }
    @media (max-width: 520px) {
      li a {
        grid-template-columns: auto minmax(0, 1fr);
        gap: var(--space-3);
        padding: var(--space-3);
      }
      .conversation-meta {
        grid-column: 2;
        justify-items: start;
      }
      .avatar {
        inline-size: 2.6rem;
        block-size: 2.6rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatListPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(ChatStore);
  private readonly session = inject(SessionStore);

  constructor() {
    this.store.loadConversations();
  }

  protected copy(): (typeof chatListCopy)[keyof typeof chatListCopy] {
    return chatListCopy[this.locale.locale()];
  }

  protected participantNames(conversation: Conversation): string {
    const currentUserId = this.session.identity()?.userId;
    const names = conversation.participants
      .filter((participant) => participant.userId !== currentUserId)
      .map((participant) => participant.displayName);
    return names.join(this.locale.locale() === 'ar' ? '، ' : ', ') || this.copy().conversation;
  }

  protected participantCount(conversation: Conversation): string {
    const count = this.formatNumber(conversation.participants.length);
    return `${count} ${this.copy().participants}`;
  }

  protected initials(conversation: Conversation): string {
    return this.participantNames(conversation).trim().slice(0, 2).toLocaleUpperCase();
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

const chatListCopy = {
  ar: {
    kicker: 'مساحة تواصل خاصة',
    intro: 'محادثات الدورات التي تشارك فيها الآن. يبدأ إنشاء محادثة من سياق الدورة فقط.',
    loading: 'جارٍ تحميل المحادثات…',
    loadingMore: 'جارٍ تحميل المزيد…',
    offline: 'لا يمكن جلب المحادثات أثناء عدم الاتصال.',
    failed: 'تعذر تحميل المحادثات.',
    retry: 'إعادة المحاولة',
    emptyTitle: 'لا توجد محادثات حالية',
    emptyBody: 'ستظهر هنا المحادثات التي أُنشئت لك من داخل دورة مشتركة.',
    more: 'محادثات أقدم',
    participants: 'مشاركين',
    messages: 'آخر تسلسل',
    conversation: 'محادثة دورة',
  },
  en: {
    kicker: 'Private course channel',
    intro:
      'Conversations for courses you currently share. New threads begin from course context only.',
    loading: 'Loading conversations…',
    loadingMore: 'Loading more…',
    offline: 'Conversations cannot be fetched while offline.',
    failed: 'Conversations could not be loaded.',
    retry: 'Retry',
    emptyTitle: 'No current conversations',
    emptyBody: 'Conversations created for you within a shared course will appear here.',
    more: 'Older conversations',
    participants: 'participants',
    messages: 'Latest sequence',
    conversation: 'Course conversation',
  },
} as const;
