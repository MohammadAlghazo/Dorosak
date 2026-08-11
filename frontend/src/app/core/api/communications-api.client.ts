import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import type {
  Announcement,
  AnnouncementContentRequest,
  AnnouncementPage,
  CommunicationNotification,
  Conversation,
  ConversationPage,
  CreateConversationRequest,
  CreateMessageRequest,
  Message,
  MessagePage,
  NotificationPage,
  NotificationsReadResult,
  NotificationUnreadCount,
  UpdateAnnouncementRequest,
} from './communications-api.types';
import { IdentityApiClient } from './identity-api.client';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class CommunicationsApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getConversations(limit = 20, cursor: string | null = null): Observable<ConversationPage> {
    return this.read<ConversationPage>('conversations', pageParams(limit, cursor));
  }

  createConversation(
    request: CreateConversationRequest,
    idempotencyKey: string,
  ): Observable<Conversation> {
    return this.mutation<Conversation>('post', 'conversations', request, idempotencyKey);
  }

  getMessages(
    conversationId: string,
    limit = 50,
    cursor: string | null = null,
    afterSequence: number | null = null,
  ): Observable<MessagePage> {
    let params = pageParams(limit, cursor);
    if (afterSequence !== null) params = params.set('afterSequence', afterSequence);
    return this.read<MessagePage>(
      `conversations/${encodeURIComponent(conversationId)}/messages`,
      params,
    );
  }

  createMessage(
    conversationId: string,
    request: CreateMessageRequest,
    idempotencyKey: string,
  ): Observable<Message> {
    return this.mutation<Message>(
      'post',
      `conversations/${encodeURIComponent(conversationId)}/messages`,
      request,
      idempotencyKey,
    );
  }

  leaveConversation(conversationId: string): Observable<boolean> {
    return this.mutation<{ readonly completed: boolean }>(
      'delete',
      `conversations/${encodeURIComponent(conversationId)}/participants/me`,
      null,
    ).pipe(map((result) => result.completed));
  }

  getNotifications(
    limit = 20,
    cursor: string | null = null,
    afterSequence: number | null = null,
  ): Observable<NotificationPage> {
    let params = pageParams(limit, cursor);
    if (afterSequence !== null) params = params.set('afterSequence', afterSequence);
    return this.read<NotificationPage>('me/notifications', params);
  }

  getNotificationUnreadCount(): Observable<NotificationUnreadCount> {
    return this.read<NotificationUnreadCount>('me/notifications/unread-count');
  }

  markNotificationRead(notificationId: string): Observable<CommunicationNotification> {
    return this.mutation<CommunicationNotification>(
      'put',
      `me/notifications/${encodeURIComponent(notificationId)}/read`,
      null,
    );
  }

  markAllNotificationsRead(): Observable<NotificationsReadResult> {
    return this.mutation<NotificationsReadResult>('post', 'me/notifications/read-all', null);
  }

  getAnnouncements(
    courseId: string,
    limit = 20,
    cursor: string | null = null,
  ): Observable<AnnouncementPage> {
    return this.read<AnnouncementPage>(announcementPath(courseId), pageParams(limit, cursor));
  }

  getAnnouncement(courseId: string, announcementId: string): Observable<Announcement> {
    return this.read<Announcement>(
      `${announcementPath(courseId)}/${encodeURIComponent(announcementId)}`,
    );
  }

  createAnnouncement(
    courseId: string,
    request: AnnouncementContentRequest,
    idempotencyKey: string,
  ): Observable<Announcement> {
    return this.mutation<Announcement>('post', announcementPath(courseId), request, idempotencyKey);
  }

  updateAnnouncement(
    courseId: string,
    announcementId: string,
    request: UpdateAnnouncementRequest,
    idempotencyKey: string,
  ): Observable<Announcement> {
    return this.mutation<Announcement>(
      'put',
      `${announcementPath(courseId)}/${encodeURIComponent(announcementId)}`,
      request,
      idempotencyKey,
    );
  }

  deleteAnnouncement(
    courseId: string,
    announcementId: string,
    expectedVersion: number,
  ): Observable<boolean> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.delete<ApiEnvelope<{ readonly completed: boolean }>>(
          `${announcementPath(courseId)}/${encodeURIComponent(announcementId)}`,
          {
            context: authenticatedMutationContext(),
            params: new HttpParams().set('expectedVersion', expectedVersion),
          },
        ),
      ),
      map((response) => response.data.completed),
    );
  }

  private read<T>(path: string, params = new HttpParams()): Observable<T> {
    return this.http
      .get<ApiEnvelope<T>>(path, { context: authenticatedReadContext(), params })
      .pipe(map((response) => response.data));
  }

  private mutation<T>(
    method: 'post' | 'put' | 'delete',
    path: string,
    body: unknown,
    idempotencyKey?: string,
  ): Observable<T> {
    const headers = idempotencyKey
      ? new HttpHeaders({ 'Idempotency-Key': idempotencyKey })
      : new HttpHeaders();
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.request<ApiEnvelope<T>>(method, path, {
          body,
          context: authenticatedMutationContext(),
          headers,
        }),
      ),
      map((response) => response.data),
    );
  }
}

const pageParams = (limit: number, cursor: string | null): HttpParams => {
  let params = new HttpParams().set('limit', limit);
  if (cursor !== null) params = params.set('cursor', cursor);
  return params;
};

const announcementPath = (courseId: string): string =>
  `instructor/courses/${encodeURIComponent(courseId)}/announcements`;
