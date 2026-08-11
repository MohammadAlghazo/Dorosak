import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type {
  CommunicationNotification,
  CommunicationRealtimeEvent,
  NotificationPage,
} from '../../core/api/communications-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';
import { NotificationBadgeStore } from './notification-badge.store';
import { NotificationsStore } from './notifications.store';

describe('NotificationsStore', () => {
  let store: NotificationsStore;
  let api: ReturnType<typeof createApi>;
  let realtimeEvents: Subject<CommunicationRealtimeEvent>;
  let realtimeResync: Subject<void>;
  let badge: ReturnType<typeof createBadge>;

  beforeEach(() => {
    api = createApi();
    badge = createBadge();
    realtimeEvents = new Subject<CommunicationRealtimeEvent>();
    realtimeResync = new Subject<void>();
    TestBed.configureTestingModule({
      providers: [
        NotificationsStore,
        { provide: CommunicationsApiClient, useValue: api },
        { provide: NotificationBadgeStore, useValue: badge },
        { provide: ConnectivityStore, useValue: { isOnline: signal(true).asReadonly() } },
        {
          provide: CommunicationsRealtimeService,
          useValue: { events$: realtimeEvents, resync$: realtimeResync },
        },
      ],
    });
    store = TestBed.inject(NotificationsStore);
  });

  it('resyncs every cursor page and deduplicates notifications by id', () => {
    api.getNotifications
      .mockReturnValueOnce(of(page([notification(2)], 2, 1)))
      .mockReturnValueOnce(
        of({
          ...page([notification(2, true), notification(3)], 4, 2),
          nextCursor: 'resync-cursor',
          hasMore: true,
        }),
      )
      .mockReturnValueOnce(of(page([notification(4)], 4, 2)));
    store.load();

    realtimeEvents.next(messageEvent);

    expect(api.getNotifications).toHaveBeenNthCalledWith(2, 100, null, 2);
    expect(api.getNotifications).toHaveBeenNthCalledWith(3, 100, 'resync-cursor', 2);
    expect(store.state().items.map((item) => item.id)).toEqual([
      'notification-4',
      'notification-3',
      'notification-2',
    ]);
    expect(store.state().latestSequence).toBe(4);
    expect(store.state().items.find((item) => item.id === 'notification-2')?.isRead).toBe(true);
  });

  it('optimistically marks one notification and rolls it back on failure', () => {
    const mutation = new Subject<CommunicationNotification>();
    api.getNotifications.mockReturnValue(of(page([notification(1)], 1, 1)));
    api.markNotificationRead.mockReturnValue(mutation);
    store.load();

    store.markRead('notification-1');
    expect(store.state().items[0]?.isRead).toBe(true);
    expect(store.state().unreadCount).toBe(0);
    expect(badge.markOneRead).toHaveBeenCalledOnce();

    mutation.error(problem(503, 'DEPENDENCY.UNAVAILABLE'));
    expect(store.state().items[0]?.isRead).toBe(false);
    expect(store.state().unreadCount).toBe(1);
    expect(badge.rollbackOneRead).toHaveBeenCalledOnce();
  });

  it('applies mark-all only through the server sequence boundary', () => {
    api.getNotifications.mockReturnValue(of(page([notification(6), notification(4)], 6, 2)));
    api.markAllNotificationsRead.mockReturnValue(of({ updatedCount: 1, throughSequence: 4 }));
    store.load();

    store.markAllRead();

    expect(store.state().items.find((item) => item.sequence === 4)?.isRead).toBe(true);
    expect(store.state().items.find((item) => item.sequence === 6)?.isRead).toBe(false);
    expect(store.state().unreadCount).toBe(1);
    expect(badge.markAllRead).toHaveBeenCalledWith(1, 4);
  });
});

const createApi = () => ({
  getNotifications: vi.fn<CommunicationsApiClient['getNotifications']>(() => of(page([], 0, 0))),
  markNotificationRead: vi.fn<CommunicationsApiClient['markNotificationRead']>(() =>
    of(notification(1, true)),
  ),
  markAllNotificationsRead: vi.fn<CommunicationsApiClient['markAllNotificationsRead']>(() =>
    of({ updatedCount: 0, throughSequence: 0 }),
  ),
});

const createBadge = () => ({
  synchronize: vi.fn(),
  markOneRead: vi.fn(),
  rollbackOneRead: vi.fn(),
  markAllRead: vi.fn(),
});

const notification = (sequence: number, isRead = false): CommunicationNotification => ({
  id: `notification-${String(sequence)}`,
  sequence,
  type: 'Message',
  resourceId: `message-${String(sequence)}`,
  courseId: 'course-1',
  conversationId: 'conversation-1',
  actorUserId: 'user-2',
  announcementVersion: null,
  title: null,
  body: null,
  isRead,
  readAt: isRead ? '2030-01-01T00:01:00Z' : null,
  createdAt: `2030-01-01T00:00:0${String(sequence)}Z`,
});

const page = (
  items: readonly CommunicationNotification[],
  latestSequence: number,
  unreadCount: number,
): NotificationPage => ({ items, nextCursor: null, hasMore: false, latestSequence, unreadCount });

const messageEvent: CommunicationRealtimeEvent = {
  eventId: 'event-1',
  eventType: 'communication.message-created',
  schemaVersion: 1,
  occurredAt: '2030-01-01T00:00:03Z',
  payload: {
    messageId: 'message-3',
    conversationId: 'conversation-1',
    senderUserId: 'user-2',
    sequence: 3,
  },
};

const problem = (status: number, code: string): ApiProblem =>
  new ApiProblem(status, code, null, null, null, {}, code);
