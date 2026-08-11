export interface ConversationParticipant {
  readonly userId: string;
  readonly displayName: string;
  readonly joinedAt: string;
}

export interface Conversation {
  readonly id: string;
  readonly courseId: string;
  readonly createdByUserId: string;
  readonly participants: readonly ConversationParticipant[];
  readonly lastSequence: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface ConversationPage {
  readonly items: readonly Conversation[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
}

export interface Message {
  readonly id: string;
  readonly conversationId: string;
  readonly senderUserId: string;
  readonly senderName: string;
  readonly clientMessageId: string;
  readonly sequence: number;
  readonly body: string;
  readonly createdAt: string;
}

export interface MessagePage {
  readonly items: readonly Message[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly latestSequence: number;
}

export type NotificationType = 'Message' | 'Announcement';

export interface CommunicationNotification {
  readonly id: string;
  readonly sequence: number;
  readonly type: NotificationType;
  readonly resourceId: string;
  readonly courseId: string | null;
  readonly conversationId: string | null;
  readonly actorUserId: string;
  readonly announcementVersion: number | null;
  readonly title: string | null;
  readonly body: string | null;
  readonly isRead: boolean;
  readonly readAt: string | null;
  readonly createdAt: string;
}

export interface NotificationPage {
  readonly items: readonly CommunicationNotification[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly latestSequence: number;
  readonly unreadCount: number;
}

export interface NotificationUnreadCount {
  readonly count: number;
  readonly latestSequence: number;
}

export interface NotificationsReadResult {
  readonly updatedCount: number;
  readonly throughSequence: number;
}

export interface Announcement {
  readonly id: string;
  readonly courseId: string;
  readonly createdByUserId: string;
  readonly title: string;
  readonly body: string;
  readonly version: number;
  readonly targetCount: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface AnnouncementPage {
  readonly items: readonly Announcement[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
}

export interface CreateConversationRequest {
  readonly participantUserIds: readonly string[];
  readonly courseId: string;
}

export interface CreateMessageRequest {
  readonly clientMessageId: string;
  readonly body: string;
}

export interface AnnouncementContentRequest {
  readonly title: string;
  readonly body: string;
}

export interface UpdateAnnouncementRequest extends AnnouncementContentRequest {
  readonly expectedVersion: number;
}

export type CommunicationRealtimeEvent =
  | CommunicationRealtimeEnvelope<
      'communication.conversation-created',
      {
        readonly conversationId: string;
        readonly createdByUserId: string;
        readonly courseId: string;
      }
    >
  | CommunicationRealtimeEnvelope<
      'communication.message-created',
      {
        readonly messageId: string;
        readonly conversationId: string;
        readonly senderUserId: string;
        readonly sequence: number;
      }
    >
  | CommunicationRealtimeEnvelope<
      'communication.conversation-left',
      { readonly conversationId: string; readonly userId: string }
    >
  | CommunicationRealtimeEnvelope<
      'communication.announcement-created',
      {
        readonly announcementId: string;
        readonly courseId: string;
        readonly createdByUserId: string;
        readonly version: number;
        readonly targetCount: number;
      }
    >
  | CommunicationRealtimeEnvelope<
      'communication.announcement-updated',
      {
        readonly announcementId: string;
        readonly courseId: string;
        readonly updatedByUserId: string;
        readonly version: number;
        readonly targetCount: number;
      }
    >
  | CommunicationRealtimeEnvelope<
      'communication.announcement-deleted',
      {
        readonly announcementId: string;
        readonly courseId: string;
        readonly deletedByUserId: string;
        readonly version: number;
      }
    >;

interface CommunicationRealtimeEnvelope<TEventType extends string, TPayload> {
  readonly eventId: string;
  readonly eventType: TEventType;
  readonly schemaVersion: 1;
  readonly occurredAt: string;
  readonly payload: TPayload;
}
