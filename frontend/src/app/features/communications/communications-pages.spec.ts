import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { IdentitySnapshot } from '../../core/api/identity-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';
import { ChatStore, type ChatListState, type ChatThreadState } from './chat.store';
import { ChatThreadPageComponent } from './chat-thread-page.component';
import type { NotificationMutationState, NotificationsState } from './notifications.store';
import { NotificationsStore } from './notifications.store';
import { NotificationsPageComponent } from './notifications-page.component';

describe('communications pages', () => {
  it('renders an accessible directional chat log and exposes manual retry', async () => {
    const chat = createChatStore();
    await TestBed.configureTestingModule({
      imports: [ChatThreadPageComponent],
      providers: [
        provideRouter([]),
        { provide: LocaleService, useValue: localeService },
        { provide: SessionStore, useValue: sessionStore },
      ],
    })
      .overrideComponent(ChatThreadPageComponent, {
        set: { providers: [{ provide: ChatStore, useValue: chat }] },
      })
      .compileComponents();
    const fixture = TestBed.createComponent(ChatThreadPageComponent);
    fixture.componentRef.setInput('conversationId', 'conversation-1');
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[role="log"]')).toBeTruthy();
    expect(root.querySelector('.message-log p')?.getAttribute('dir')).toBe('auto');
    expect(root.querySelector('input')).toBeNull();
    root.querySelector<HTMLButtonElement>('.failed-actions button')?.click();
    expect(chat.retryMessage).toHaveBeenCalledWith('client-message-1');
    expect(chat.openThread).toHaveBeenCalledWith('conversation-1');
  });

  it('links a message notification to its conversation and marks it read', async () => {
    const notifications = createNotificationsStore();
    await TestBed.configureTestingModule({
      imports: [NotificationsPageComponent],
      providers: [
        provideRouter([]),
        { provide: LocaleService, useValue: localeService },
        { provide: SessionStore, useValue: sessionStore },
      ],
    })
      .overrideComponent(NotificationsPageComponent, {
        set: { providers: [{ provide: NotificationsStore, useValue: notifications }] },
      })
      .compileComponents();
    const fixture = TestBed.createComponent(NotificationsPageComponent);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const link = root.querySelector<HTMLAnchorElement>('.notification-link');
    expect(link?.getAttribute('href')).toBe('/en/chat/conversation-1');
    root.querySelector<HTMLButtonElement>('.read-button')?.click();
    expect(notifications.markRead).toHaveBeenCalledWith('notification-1');
    expect(notifications.load).toHaveBeenCalledOnce();
  });
});

const locale = signal<'ar' | 'en'>('en');
const localeService = {
  locale: locale.asReadonly(),
  copy: () => ({ notifications: 'Notifications' }),
};

const identity: IdentitySnapshot = {
  userId: 'user-1',
  sessionId: 'session-1',
  displayName: 'Current Learner',
  email: 'learner@example.test',
  emailVerified: true,
  mfaEnabled: false,
  authenticatedAt: '2030-01-01T00:00:00Z',
  recentAuthenticationExpiresAt: '2030-01-01T01:00:00Z',
  authorizationVersion: 1,
  roles: ['Student'],
  permissions: ['Conversation.ReadOwn', 'Notification.ReadOwn'],
  authenticationMethods: ['pwd'],
};

const sessionStore = {
  identity: signal<IdentitySnapshot | null>(identity).asReadonly(),
  hasPermission: (permission: string) => identity.permissions.includes(permission),
};

const createChatStore = () => {
  const conversations: ChatListState = {
    status: 'success',
    items: [
      {
        id: 'conversation-1',
        courseId: 'course-1',
        createdByUserId: 'user-1',
        participants: [
          {
            userId: 'user-1',
            displayName: 'Current Learner',
            joinedAt: '2030-01-01T00:00:00Z',
          },
          {
            userId: 'user-2',
            displayName: 'Instructor',
            joinedAt: '2030-01-01T00:00:00Z',
          },
        ],
        lastSequence: 1,
        createdAt: '2030-01-01T00:00:00Z',
        updatedAt: '2030-01-01T00:01:00Z',
      },
    ],
    nextCursor: null,
    hasMore: false,
    errorCode: null,
  };
  const thread: ChatThreadState = {
    status: 'success',
    conversationId: 'conversation-1',
    messages: [
      {
        id: null,
        conversationId: 'conversation-1',
        senderUserId: 'user-1',
        senderName: 'Current Learner',
        clientMessageId: 'client-message-1',
        sequence: null,
        body: 'رسالة ثنائية الاتجاه',
        createdAt: '2030-01-01T00:01:00Z',
        delivery: 'failed',
        idempotencyKey: 'stable-key',
        errorCode: 'HTTP.0',
      },
    ],
    nextCursor: null,
    hasMore: false,
    latestSequence: 0,
    errorCode: null,
  };
  return {
    conversations: signal(conversations).asReadonly(),
    thread: signal(thread).asReadonly(),
    leave: signal({ status: 'idle' as const, errorCode: null }).asReadonly(),
    leftConversationId: signal<string | null>(null).asReadonly(),
    openThread: vi.fn(),
    loadOlderMessages: vi.fn(),
    sendMessage: vi.fn(),
    retryMessage: vi.fn(),
    leaveConversation: vi.fn(),
  };
};

const createNotificationsStore = () => {
  const state: NotificationsState = {
    status: 'success',
    items: [
      {
        id: 'notification-1',
        sequence: 1,
        type: 'Message',
        resourceId: 'message-1',
        courseId: 'course-1',
        conversationId: 'conversation-1',
        actorUserId: 'user-2',
        announcementVersion: null,
        title: null,
        body: null,
        isRead: false,
        readAt: null,
        createdAt: '2030-01-01T00:00:00Z',
      },
    ],
    nextCursor: null,
    hasMore: false,
    latestSequence: 1,
    unreadCount: 1,
    errorCode: null,
  };
  const mutation: NotificationMutationState = {
    pendingReadIds: new Set(),
    markingAll: false,
    errorCode: null,
  };
  return {
    state: signal(state).asReadonly(),
    mutation: signal(mutation).asReadonly(),
    load: vi.fn(),
    loadMore: vi.fn(),
    retry: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
  };
};
