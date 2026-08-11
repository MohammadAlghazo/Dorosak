import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import type { Conversation } from '../../core/api/communications-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { ChatStore } from './chat.store';

@Component({
  selector: 'drs-chat-thread-page',
  imports: [FormsModule, RouterLink],
  providers: [ChatStore],
  template: `
    <section class="thread-page" aria-labelledby="thread-title">
      <a class="back-link" [routerLink]="['/', locale.locale(), 'chat']">{{ copy().back }}</a>
      <header class="thread-heading">
        <div>
          <p class="identity-kicker">{{ copy().kicker }}</p>
          <h1 id="thread-title" dir="auto">{{ conversationName() }}</h1>
          @if (activeConversation(); as conversation) {
            <p>{{ participantSummary(conversation) }}</p>
          }
        </div>
        <button class="danger-button leave-button" type="button" (click)="confirmLeave.set(true)">
          {{ copy().leave }}
        </button>
      </header>

      @if (confirmLeave()) {
        <section class="leave-confirmation" role="alertdialog" aria-labelledby="leave-title">
          <h2 id="leave-title">{{ copy().leaveTitle }}</h2>
          <p>{{ copy().leaveBody }}</p>
          <div class="action-row">
            <button class="secondary-button" type="button" (click)="confirmLeave.set(false)">
              {{ copy().cancel }}
            </button>
            <button
              class="danger-button"
              type="button"
              [disabled]="store.leave().status === 'leaving'"
              (click)="leave()"
            >
              {{ store.leave().status === 'leaving' ? copy().leaving : copy().confirmLeave }}
            </button>
          </div>
        </section>
      }

      @if (store.leave().status === 'error' || store.leave().status === 'offline') {
        <div class="form-alert" role="alert">
          {{ store.leave().status === 'offline' ? copy().offline : copy().leaveFailed }}
          @if (store.leave().errorCode) {
            <code>{{ store.leave().errorCode }}</code>
          }
        </div>
      }

      @switch (store.thread().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">{{ copy().loading }}</div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ copy().offline }}
            <button class="text-button" type="button" (click)="reload()">{{ copy().retry }}</button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ copy().failed }}
            @if (store.thread().errorCode) {
              <code>{{ store.thread().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="reload()">{{ copy().retry }}</button>
          </div>
        }
      }

      <div class="thread-surface">
        @if (store.thread().hasMore) {
          <button
            class="text-button older-button"
            type="button"
            [disabled]="store.thread().status === 'loadingOlder'"
            (click)="store.loadOlderMessages()"
          >
            {{ store.thread().status === 'loadingOlder' ? copy().loadingOlder : copy().older }}
          </button>
        }

        @if (store.thread().messages.length === 0 && store.thread().status === 'empty') {
          <div class="empty-thread">
            <h2>{{ copy().emptyTitle }}</h2>
            <p>{{ copy().emptyBody }}</p>
          </div>
        }

        <ol
          class="message-log"
          role="log"
          aria-live="polite"
          aria-relevant="additions"
          [attr.aria-label]="copy().logLabel"
        >
          @for (message of store.thread().messages; track message.clientMessageId) {
            <li [class.own-message]="message.senderUserId === session.identity()?.userId">
              <article [class.failed-message]="message.delivery === 'failed'">
                <header>
                  <strong dir="auto">{{ message.senderName }}</strong>
                  <time [attr.datetime]="message.createdAt">{{
                    formatTime(message.createdAt)
                  }}</time>
                </header>
                <p dir="auto">{{ message.body }}</p>
                @if (message.delivery === 'pending') {
                  <span class="delivery" role="status">{{ copy().sending }}</span>
                }
                @if (message.delivery === 'failed') {
                  <div class="failed-actions" role="alert">
                    <span>{{ copy().sendFailed }}</span>
                    <button
                      class="text-button"
                      type="button"
                      (click)="store.retryMessage(message.clientMessageId)"
                    >
                      {{ copy().retrySend }}
                    </button>
                  </div>
                }
              </article>
            </li>
          }
        </ol>
      </div>

      <form class="composer" (ngSubmit)="send()">
        <label for="chat-message">{{ copy().messageLabel }}</label>
        <div>
          <textarea
            id="chat-message"
            name="message"
            rows="2"
            maxlength="5000"
            required
            dir="auto"
            [(ngModel)]="draft"
            [placeholder]="copy().placeholder"
          ></textarea>
          <button class="primary-button" type="submit" [disabled]="!draft.trim()">
            {{ copy().send }}
          </button>
        </div>
        <small>{{ formatNumber(draft.length) }} / 5,000</small>
      </form>
    </section>
  `,
  styles: `
    .thread-page {
      inline-size: min(100%, 72rem);
      margin-inline: auto;
    }
    .thread-heading {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-5);
      margin-block-end: var(--space-4);
    }
    h1 {
      margin-block: var(--space-2);
      font-size: clamp(1.9rem, 5vw, 3.4rem);
    }
    .thread-heading p:last-child {
      color: var(--color-muted);
    }
    .leave-button {
      flex: none;
    }
    .leave-confirmation {
      margin-block: var(--space-4);
      padding: var(--space-5);
      background: var(--color-surface);
      border: 1px solid var(--color-danger);
      border-radius: var(--radius-2);
    }
    .leave-confirmation h2 {
      margin-block-start: 0;
    }
    .thread-surface {
      min-block-size: 24rem;
      padding: clamp(var(--space-3), 3vw, var(--space-5));
      background:
        linear-gradient(
          135deg,
          color-mix(in srgb, var(--color-brand) 7%, transparent),
          transparent 48%
        ),
        var(--color-subtle);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2) var(--radius-2) 0 0;
    }
    .older-button {
      display: block;
      margin-inline: auto;
    }
    .message-log {
      display: grid;
      gap: var(--space-3);
      margin: var(--space-4) 0 0;
      padding: 0;
      list-style: none;
    }
    .message-log li {
      display: flex;
      min-inline-size: 0;
    }
    .message-log article {
      inline-size: min(78%, 42rem);
      min-inline-size: 0;
      padding: var(--space-3) var(--space-4);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 1rem 1rem 1rem 0.25rem;
      box-shadow: var(--shadow-1);
    }
    :host-context([dir='rtl']) .message-log article {
      border-radius: 1rem 1rem 0.25rem 1rem;
    }
    .message-log .own-message {
      justify-content: end;
    }
    .message-log .own-message article {
      color: var(--color-text);
      background: color-mix(in srgb, var(--color-brand) 13%, var(--color-surface));
      border-color: color-mix(in srgb, var(--color-brand) 38%, var(--color-border));
    }
    .message-log article.failed-message {
      border-color: var(--color-danger);
    }
    .message-log header {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
      color: var(--color-muted);
      font-size: 0.78rem;
    }
    .message-log p {
      margin-block: var(--space-2) 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    .delivery,
    .failed-actions {
      display: block;
      margin-block-start: var(--space-2);
      color: var(--color-muted);
      font-size: 0.78rem;
    }
    .failed-actions {
      color: var(--color-danger);
    }
    .composer {
      position: sticky;
      inset-block-end: 0;
      z-index: 1;
      display: grid;
      gap: var(--space-2);
      padding: var(--space-4);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-block-start: 3px solid var(--color-brand);
      border-radius: 0 0 var(--radius-2) var(--radius-2);
    }
    .composer > div {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: var(--space-3);
      align-items: end;
    }
    .composer textarea {
      inline-size: 100%;
      min-block-size: 54px;
      padding: var(--space-3);
      color: var(--color-text);
      background: var(--color-canvas);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
      resize: vertical;
    }
    .composer small {
      color: var(--color-muted);
      text-align: end;
    }
    .empty-thread {
      padding-block: var(--space-8);
      color: var(--color-muted);
      text-align: center;
    }
    @media (max-width: 560px) {
      .thread-heading {
        align-items: stretch;
        flex-direction: column;
      }
      .leave-button {
        inline-size: 100%;
      }
      .thread-surface {
        margin-inline: calc(-1 * var(--page-gutter));
        border-inline: 0;
        border-radius: 0;
      }
      .message-log article {
        inline-size: 90%;
      }
      .composer {
        margin-inline: calc(-1 * var(--page-gutter));
        border-inline: 0;
        border-radius: 0;
      }
      .composer > div {
        grid-template-columns: minmax(0, 1fr);
      }
      .composer button {
        inline-size: 100%;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatThreadPageComponent {
  readonly conversationId = input.required<string>();
  protected readonly locale = inject(LocaleService);
  protected readonly session = inject(SessionStore);
  protected readonly store = inject(ChatStore);
  protected readonly confirmLeave = signal(false);
  private readonly router = inject(Router);
  protected draft = '';
  private loadedConversationId: string | null = null;

  constructor() {
    effect(() => {
      const conversationId = this.conversationId();
      if (conversationId === this.loadedConversationId) return;
      this.loadedConversationId = conversationId;
      this.confirmLeave.set(false);
      this.store.openThread(conversationId);
    });
    effect(() => {
      if (this.store.leftConversationId() !== this.conversationId()) return;
      void this.router.navigate(['/', this.locale.locale(), 'chat']);
    });
  }

  protected copy(): (typeof chatThreadCopy)[keyof typeof chatThreadCopy] {
    return chatThreadCopy[this.locale.locale()];
  }

  protected activeConversation(): Conversation | undefined {
    return this.store
      .conversations()
      .items.find((conversation) => conversation.id === this.conversationId());
  }

  protected conversationName(): string {
    const conversation = this.activeConversation();
    if (!conversation) return this.copy().conversation;
    const userId = this.session.identity()?.userId;
    return (
      conversation.participants
        .filter((participant) => participant.userId !== userId)
        .map((participant) => participant.displayName)
        .join(this.locale.locale() === 'ar' ? '، ' : ', ') || this.copy().conversation
    );
  }

  protected participantSummary(conversation: Conversation): string {
    return `${this.formatNumber(conversation.participants.length)} ${this.copy().participants}`;
  }

  protected send(): void {
    const body = this.draft.trim();
    if (!body) return;
    this.store.sendMessage(body);
    this.draft = '';
  }

  protected reload(): void {
    this.store.openThread(this.conversationId());
  }

  protected leave(): void {
    this.store.leaveConversation(this.conversationId());
  }

  protected formatTime(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  protected formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale.locale()).format(value);
  }
}

const chatThreadCopy = {
  ar: {
    back: 'العودة إلى المحادثات',
    kicker: 'محادثة دورة',
    conversation: 'المحادثة',
    participants: 'مشاركين',
    leave: 'مغادرة المحادثة',
    leaveTitle: 'هل تريد مغادرة المحادثة؟',
    leaveBody: 'لن تتمكن من قراءة الرسائل أو إرسال رسائل جديدة بعد المغادرة.',
    cancel: 'إلغاء',
    confirmLeave: 'نعم، غادر',
    leaving: 'جارٍ المغادرة…',
    leaveFailed: 'تعذر مغادرة المحادثة.',
    loading: 'جارٍ تحميل الرسائل…',
    loadingOlder: 'جارٍ تحميل الرسائل الأقدم…',
    older: 'تحميل رسائل أقدم',
    retry: 'إعادة المحاولة',
    offline: 'أنت غير متصل. لن تُرسل الرسائل تلقائيًا.',
    failed: 'تعذر تحميل رسائل المحادثة.',
    emptyTitle: 'ابدأ المحادثة',
    emptyBody: 'لا توجد رسائل في هذه المحادثة بعد.',
    logLabel: 'سجل رسائل المحادثة',
    sending: 'جارٍ الإرسال…',
    sendFailed: 'لم تُرسل الرسالة.',
    retrySend: 'إعادة الإرسال يدويًا',
    messageLabel: 'رسالة جديدة',
    placeholder: 'اكتب رسالتك…',
    send: 'إرسال',
  },
  en: {
    back: 'Back to conversations',
    kicker: 'Course conversation',
    conversation: 'Conversation',
    participants: 'participants',
    leave: 'Leave conversation',
    leaveTitle: 'Leave this conversation?',
    leaveBody: 'You will no longer be able to read or send messages after leaving.',
    cancel: 'Cancel',
    confirmLeave: 'Yes, leave',
    leaving: 'Leaving…',
    leaveFailed: 'The conversation could not be left.',
    loading: 'Loading messages…',
    loadingOlder: 'Loading older messages…',
    older: 'Load older messages',
    retry: 'Retry',
    offline: 'You are offline. Messages will not be sent automatically.',
    failed: 'Conversation messages could not be loaded.',
    emptyTitle: 'Start the conversation',
    emptyBody: 'There are no messages in this conversation yet.',
    logLabel: 'Conversation message log',
    sending: 'Sending…',
    sendFailed: 'Message not sent.',
    retrySend: 'Retry manually',
    messageLabel: 'New message',
    placeholder: 'Write your message…',
    send: 'Send',
  },
} as const;
