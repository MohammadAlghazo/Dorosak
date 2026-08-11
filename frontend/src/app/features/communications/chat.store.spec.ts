import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type {
  CommunicationRealtimeEvent,
  Conversation,
  Message,
  MessagePage,
} from '../../core/api/communications-api.types';
import { SessionStore } from '../../core/auth/session.store';
import type { IdentitySnapshot } from '../../core/api/identity-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';
import { ChatStore } from './chat.store';

describe('ChatStore', () => {
  let store: ChatStore;
  let api: ReturnType<typeof createApi>;
  let online: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    api = createApi();
    online = signal(true);
    TestBed.configureTestingModule({
      providers: [
        ChatStore,
        { provide: CommunicationsApiClient, useValue: api },
        { provide: ConnectivityStore, useValue: { isOnline: online.asReadonly() } },
        {
          provide: CommunicationsRealtimeService,
          useValue: {
            events$: new Subject<CommunicationRealtimeEvent>(),
            resync$: new Subject<void>(),
          },
        },
        {
          provide: SessionStore,
          useValue: {
            identity: signal<IdentitySnapshot | null>(identity).asReadonly(),
            isAuthenticated: signal(true).asReadonly(),
          },
        },
      ],
    });
    store = TestBed.inject(ChatStore);
  });

  it('turns the newest initial page into chronological order and prepends older messages', () => {
    api.getMessages
      .mockReturnValueOnce(
        of({
          items: [message(2), message(1)],
          nextCursor: 'older-cursor',
          hasMore: true,
          latestSequence: 2,
        }),
      )
      .mockReturnValueOnce(
        of({
          items: [message(1), message(0)],
          nextCursor: null,
          hasMore: false,
          latestSequence: 2,
        }),
      );

    store.openThread('conversation-1');
    expect(store.thread().messages.map((item) => item.sequence)).toEqual([1, 2]);
    store.loadOlderMessages();

    expect(api.getMessages).toHaveBeenLastCalledWith('conversation-1', 50, 'older-cursor');
    expect(store.thread().messages.map((item) => item.sequence)).toEqual([0, 1, 2]);
  });

  it('retries a failed send with the same clientMessageId and idempotency key', () => {
    api.getMessages.mockReturnValue(of(emptyMessagePage));
    api.createMessage
      .mockReturnValueOnce(throwError(() => problem(503, 'DEPENDENCY.UNAVAILABLE')))
      .mockImplementationOnce((_conversationId, request) =>
        of({
          ...message(1),
          clientMessageId: request.clientMessageId,
          body: request.body,
        }),
      );
    store.openThread('conversation-1');

    store.sendMessage('Stable retry');
    const failed = store.thread().messages[0];
    expect(failed?.delivery).toBe('failed');
    if (!failed) throw new Error('Expected a failed pending message.');
    store.retryMessage(failed.clientMessageId);

    expect(api.createMessage).toHaveBeenCalledTimes(2);
    expect(api.createMessage.mock.calls[1]?.[1].clientMessageId).toBe(
      api.createMessage.mock.calls[0]?.[1].clientMessageId,
    );
    expect(api.createMessage.mock.calls[1]?.[2]).toBe(api.createMessage.mock.calls[0]?.[2]);
    expect(store.thread().messages[0]?.delivery).toBe('sent');
  });

  it('does not call the send API while offline', () => {
    api.getMessages.mockReturnValue(of(emptyMessagePage));
    store.openThread('conversation-1');
    online.set(false);

    store.sendMessage('Wait for a manual retry');

    expect(api.createMessage).not.toHaveBeenCalled();
    expect(store.thread().messages[0]?.delivery).toBe('failed');
  });
});

const createApi = () => ({
  getConversations: vi.fn<CommunicationsApiClient['getConversations']>(() =>
    of({ items: [conversation], nextCursor: null, hasMore: false }),
  ),
  getMessages: vi.fn<CommunicationsApiClient['getMessages']>(() => of(emptyMessagePage)),
  createMessage: vi.fn<CommunicationsApiClient['createMessage']>(() => of(message(1))),
  leaveConversation: vi.fn<CommunicationsApiClient['leaveConversation']>(() => of(true)),
});

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
  permissions: ['Conversation.ReadOwn', 'Message.SendAsSelf'],
  authenticationMethods: ['pwd'],
};

const conversation: Conversation = {
  id: 'conversation-1',
  courseId: 'course-1',
  createdByUserId: 'user-1',
  participants: [
    { userId: 'user-1', displayName: 'Current Learner', joinedAt: '2030-01-01T00:00:00Z' },
    { userId: 'user-2', displayName: 'Instructor', joinedAt: '2030-01-01T00:00:00Z' },
  ],
  lastSequence: 2,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:02:00Z',
};

const message = (sequence: number): Message => ({
  id: `message-${String(sequence)}`,
  conversationId: 'conversation-1',
  senderUserId: sequence % 2 === 0 ? 'user-2' : 'user-1',
  senderName: sequence % 2 === 0 ? 'Instructor' : 'Current Learner',
  clientMessageId: `client-message-${String(sequence)}`,
  sequence,
  body: `Message ${String(sequence)}`,
  createdAt: `2030-01-01T00:00:0${String(sequence)}Z`,
});

const emptyMessagePage: MessagePage = {
  items: [],
  nextCursor: null,
  hasMore: false,
  latestSequence: 0,
};

const problem = (status: number, code: string): ApiProblem =>
  new ApiProblem(status, code, null, null, null, {}, code);
