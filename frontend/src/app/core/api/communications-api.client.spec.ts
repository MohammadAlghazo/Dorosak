import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { API_REQUEST } from './api-context';
import { CommunicationsApiClient } from './communications-api.client';
import { IdentityApiClient } from './identity-api.client';

describe('CommunicationsApiClient', () => {
  let client: CommunicationsApiClient;
  let http: HttpTestingController;
  const bootstrapCsrf = vi.fn(() => of(undefined));

  beforeEach(() => {
    bootstrapCsrf.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf } },
      ],
    });
    client = TestBed.inject(CommunicationsApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('loads an encoded conversation resync page in an authenticated context', async () => {
    const promise = firstValueFrom(client.getMessages('conversation/1', 100, 'next token', 27));
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'conversations/conversation%2F1/messages' &&
        candidate.params.get('limit') === '100' &&
        candidate.params.get('cursor') === 'next token' &&
        candidate.params.get('afterSequence') === '27',
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    request.flush({ data: messagePage });
    await expect(promise).resolves.toEqual(messagePage);
  });

  it('sends a message with caller-owned stable identifiers after CSRF bootstrap', async () => {
    const promise = firstValueFrom(
      client.createMessage(
        'conversation-1',
        { clientMessageId: 'client-message-1', body: 'Hello' },
        'stable-message-key',
      ),
    );
    const request = http.expectOne('conversations/conversation-1/messages');
    expect(bootstrapCsrf).toHaveBeenCalledOnce();
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('stable-message-key');
    expect(request.request.body).toEqual({ clientMessageId: 'client-message-1', body: 'Hello' });
    request.flush({ data: message });
    await expect(promise).resolves.toEqual(message);
  });

  it('marks one encoded notification as read with CSRF protection', async () => {
    const promise = firstValueFrom(client.markNotificationRead('notification/1'));
    const request = http.expectOne('me/notifications/notification%2F1/read');
    expect(request.request.method).toBe('PUT');
    expect(bootstrapCsrf).toHaveBeenCalledOnce();
    request.flush({ data: notification });
    await expect(promise).resolves.toEqual(notification);
  });

  it('updates an announcement with expectedVersion and the stable key', async () => {
    const promise = firstValueFrom(
      client.updateAnnouncement(
        'course/1',
        'announcement/1',
        { title: 'Revised', body: 'Revised body', expectedVersion: 4 },
        'stable-announcement-key',
      ),
    );
    const request = http.expectOne('instructor/courses/course%2F1/announcements/announcement%2F1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('Idempotency-Key')).toBe('stable-announcement-key');
    expect(request.request.body).toEqual({
      title: 'Revised',
      body: 'Revised body',
      expectedVersion: 4,
    });
    request.flush({ data: announcement });
    await expect(promise).resolves.toEqual(announcement);
  });

  it('deletes an encoded announcement with expectedVersion in the query', async () => {
    const promise = firstValueFrom(client.deleteAnnouncement('course/1', 'announcement/1', 5));
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'instructor/courses/course%2F1/announcements/announcement%2F1' &&
        candidate.params.get('expectedVersion') === '5',
    );
    expect(request.request.method).toBe('DELETE');
    expect(bootstrapCsrf).toHaveBeenCalledOnce();
    request.flush({ data: { completed: true } });
    await expect(promise).resolves.toBe(true);
  });
});

const message = {
  id: 'message-1',
  conversationId: 'conversation-1',
  senderUserId: 'user-1',
  senderName: 'Learner',
  clientMessageId: 'client-message-1',
  sequence: 28,
  body: 'Hello',
  createdAt: '2030-01-01T00:00:00Z',
};

const messagePage = {
  items: [message],
  nextCursor: null,
  hasMore: false,
  latestSequence: 28,
};

const notification = {
  id: 'notification-1',
  sequence: 9,
  type: 'Message' as const,
  resourceId: 'message-1',
  courseId: 'course-1',
  conversationId: 'conversation-1',
  actorUserId: 'user-2',
  announcementVersion: null,
  title: null,
  body: null,
  isRead: true,
  readAt: '2030-01-01T00:01:00Z',
  createdAt: '2030-01-01T00:00:00Z',
};

const announcement = {
  id: 'announcement-1',
  courseId: 'course-1',
  createdByUserId: 'user-1',
  title: 'Revised',
  body: 'Revised body',
  version: 5,
  targetCount: 12,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-02T00:00:00Z',
};
