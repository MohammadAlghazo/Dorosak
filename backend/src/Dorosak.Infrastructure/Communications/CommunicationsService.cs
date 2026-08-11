using System.Globalization;
using System.Text.Json;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Domain.Communications;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;
using CommunicationMessage = Dorosak.Domain.Communications.Message;

namespace Dorosak.Infrastructure.Communications;

internal sealed class CommunicationsService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider,
    CatalogCursorCodec cursorCodec) : ICommunicationsService, IConversationAccessReader, IAnnouncementAccessReader
{
    public async Task<Result<ConversationPageResponse>> GetConversationsAsync(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        string canonicalQuery = $"conversations|{request.UserId:D}|updated-desc|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "conversations",
                canonicalQuery,
                out DateTimeOffset? afterUpdatedAt,
                out Guid? afterId))
        {
            return CursorInvalid<ConversationPageResponse>();
        }

        if (!await dbContext.Users.AsNoTracking().AnyAsync(
                user => user.Id == request.UserId && user.IsActive,
                cancellationToken))
        {
            return Result.Success(new ConversationPageResponse([], null, false));
        }

        IQueryable<Conversation> query =
            from conversation in dbContext.Conversations.AsNoTracking()
            join participant in dbContext.ConversationParticipants.AsNoTracking()
                on conversation.Id equals participant.ConversationId
            where participant.UserId == request.UserId && participant.LeftAt == null
            select conversation;
        if (afterUpdatedAt is { } timestamp && afterId is { } id)
        {
            query = query.Where(conversation =>
                conversation.UpdatedAt < timestamp ||
                conversation.UpdatedAt == timestamp && conversation.Id.CompareTo(id) < 0);
        }

        List<Conversation> conversations = await query
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = conversations.Count > request.Limit;
        Conversation[] page = conversations.Take(request.Limit).ToArray();
        IReadOnlyList<ConversationResponse> responses = await MapConversationsAsync(page, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create("conversations", canonicalQuery, page[^1].UpdatedAt, page[^1].Id)
            : null;
        return Result.Success(new ConversationPageResponse(responses, nextCursor, hasMore));
    }

    public async Task<Result<ConversationResponse>> CreateConversationAsync(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        Guid[] participantIds = request.ParticipantUserIds
            .Append(request.UserId)
            .Distinct()
            .Order()
            .ToArray();
        if (participantIds.Length is < 2 or > 50)
        {
            return Result.Failure<ConversationResponse>(ResultError.BusinessRule(
                "CONVERSATION.PARTICIPANTS_INVALID",
                "A conversation requires between two and 50 distinct participants."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!await dbContext.Courses.AsNoTracking().AnyAsync(
                course => course.Id == request.CourseId && course.DeletedAt == null,
                cancellationToken))
        {
            return NotFound<ConversationResponse>();
        }

        Dictionary<Guid, string> names = await dbContext.Users.AsNoTracking()
            .Where(user => participantIds.Contains(user.Id) && user.IsActive)
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        if (names.Count != participantIds.Length)
        {
            return NotFound<ConversationResponse>();
        }

        Guid[] courseAccessUserIds = await dbContext.Users.AsNoTracking()
            .Where(user => participantIds.Contains(user.Id) && user.IsActive &&
                (dbContext.Courses.Any(course =>
                        course.Id == request.CourseId &&
                        course.DeletedAt == null &&
                        course.OwnerUserId == user.Id) ||
                    dbContext.CourseInstructors.Any(instructor =>
                        instructor.CourseId == request.CourseId && instructor.UserId == user.Id) ||
                    dbContext.Enrollments.Any(enrollment =>
                        enrollment.UserId == user.Id &&
                        enrollment.CourseId == request.CourseId &&
                        (enrollment.Status == EnrollmentStatus.Active ||
                            enrollment.Status == EnrollmentStatus.Completed) &&
                        dbContext.Entitlements.Any(entitlement =>
                            entitlement.Id == enrollment.EntitlementId &&
                            entitlement.UserId == enrollment.UserId &&
                            entitlement.CourseId == enrollment.CourseId &&
                            entitlement.Status == EntitlementStatus.Active &&
                            (entitlement.ExpiresAt == null || entitlement.ExpiresAt > now)))))
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
        HashSet<Guid> authorizedUserIds = courseAccessUserIds.ToHashSet();
        if (!authorizedUserIds.Contains(request.UserId) &&
            await HasPermissionAsync(request.UserId, Permissions.CourseManageAny, cancellationToken))
        {
            authorizedUserIds.Add(request.UserId);
        }
        if (participantIds.Any(userId => !authorizedUserIds.Contains(userId)))
        {
            return NotFound<ConversationResponse>();
        }

        Conversation conversation;
        try
        {
            conversation = Conversation.Create(request.UserId, request.CourseId, now);
        }
        catch (DomainRuleException exception)
        {
            return RuleFailure<ConversationResponse>(exception);
        }

        ConversationParticipant[] participants = participantIds
            .Select(userId => ConversationParticipant.Join(conversation.Id, userId, now))
            .ToArray();
        dbContext.Conversations.Add(conversation);
        dbContext.ConversationParticipants.AddRange(participants);
        AddAudit(request.UserId, "communication.conversation-created", "Conversation", conversation.Id, now);
        AddOutbox(
            "communication.conversation-created",
            new { conversationId = conversation.Id, createdByUserId = request.UserId, courseId = request.CourseId, occurredAt = now },
            now);
        return Result.Success(MapConversation(conversation, participants, names));
    }

    public async Task<Result<MessagePageResponse>> GetConversationMessagesAsync(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(request.UserId, request.ConversationId, cancellationToken))
        {
            return NotFound<MessagePageResponse>();
        }

        long latestSequence = await dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.Id == request.ConversationId)
            .Select(conversation => conversation.LastSequence)
            .SingleAsync(cancellationToken);
        bool resync = request.AfterSequence.HasValue;
        string canonicalQuery = resync
            ? $"conversation-messages|{request.UserId:D}|{request.ConversationId:D}|sequence-asc|after:{request.AfterSequence}|{request.Limit}"
            : $"conversation-messages|{request.UserId:D}|{request.ConversationId:D}|created-desc|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "conversation-messages",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId,
                out string? afterKey))
        {
            return CursorInvalid<MessagePageResponse>();
        }

        IQueryable<CommunicationMessage> query = dbContext.Messages.AsNoTracking()
            .Where(message => message.ConversationId == request.ConversationId);
        List<CommunicationMessage> messages;
        if (resync)
        {
            if (!TryReadSequenceCursor(afterKey, request.AfterSequence!.Value, out long sequence))
            {
                return CursorInvalid<MessagePageResponse>();
            }

            messages = await query
                .Where(message => message.Sequence > sequence)
                .OrderBy(message => message.Sequence)
                .Take(request.Limit + 1)
                .ToListAsync(cancellationToken);
        }
        else
        {
            if (afterCreatedAt is { } timestamp && afterId is { } id)
            {
                query = query.Where(message =>
                    message.CreatedAt < timestamp ||
                    message.CreatedAt == timestamp && message.Id.CompareTo(id) < 0);
            }

            messages = await query
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Take(request.Limit + 1)
                .ToListAsync(cancellationToken);
        }

        bool hasMore = messages.Count > request.Limit;
        CommunicationMessage[] page = messages.Take(request.Limit).ToArray();
        IReadOnlyList<MessageResponse> responses = await MapMessagesAsync(page, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? resync
                ? cursorCodec.Create(
                    "conversation-messages",
                    canonicalQuery,
                    null,
                    null,
                    page[^1].Sequence.ToString(CultureInfo.InvariantCulture))
                : cursorCodec.Create("conversation-messages", canonicalQuery, page[^1].CreatedAt, page[^1].Id)
            : null;
        return Result.Success(new MessagePageResponse(responses, nextCursor, hasMore, latestSequence));
    }

    public async Task<Result<MessageResponse>> CreateMessageAsync(
        CreateMessageCommand request,
        CancellationToken cancellationToken)
    {
        await LockConversationAsync(request.ConversationId, cancellationToken);
        Conversation? conversation = await dbContext.Conversations.SingleOrDefaultAsync(candidate =>
            candidate.Id == request.ConversationId &&
            dbContext.ConversationParticipants.Any(participant =>
                participant.ConversationId == candidate.Id &&
                participant.UserId == request.UserId &&
                participant.LeftAt == null) &&
            dbContext.Users.Any(user => user.Id == request.UserId && user.IsActive),
            cancellationToken);
        if (conversation is null)
        {
            return NotFound<MessageResponse>();
        }

        string normalizedBody = request.Body.Trim();
        CommunicationMessage? existing = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(message =>
            message.ConversationId == request.ConversationId &&
            message.SenderUserId == request.UserId &&
            message.ClientMessageId == request.ClientMessageId,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Body, normalizedBody, StringComparison.Ordinal))
            {
                return Result.Failure<MessageResponse>(ResultError.Conflict(
                    "MESSAGE.CLIENT_MESSAGE_ID_REUSED",
                    "The client message identifier was already used with different content."));
            }

            return Result.Success(await MapMessageAsync(existing, cancellationToken));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        CommunicationMessage message;
        try
        {
            message = CommunicationMessage.Create(
                request.ConversationId,
                request.UserId,
                request.ClientMessageId,
                request.Body,
                conversation.NextMessageSequence(),
                now);
            conversation.RecordMessage(now);
        }
        catch (DomainRuleException exception)
        {
            return RuleFailure<MessageResponse>(exception);
        }

        dbContext.Messages.Add(message);
        Guid[] recipientIds = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.ConversationId == message.ConversationId &&
                participant.UserId != message.SenderUserId &&
                participant.LeftAt == null &&
                dbContext.Users.Any(user => user.Id == participant.UserId && user.IsActive))
            .OrderBy(participant => participant.UserId)
            .Select(participant => participant.UserId)
            .ToArrayAsync(cancellationToken);
        await AddMessageNotificationsAsync(
            message,
            recipientIds,
            now,
            cancellationToken);
        AddAudit(request.UserId, "communication.message-created", "Message", message.Id, now);
        AddOutbox(
            "communication.message-created",
            new
            {
                messageId = message.Id,
                conversationId = message.ConversationId,
                senderUserId = message.SenderUserId,
                sequence = message.Sequence,
                occurredAt = now,
            },
            now);
        return Result.Success(await MapMessageAsync(message, cancellationToken));
    }

    public async Task<Result<ConversationOperationResponse>> LeaveConversationAsync(
        LeaveConversationCommand request,
        CancellationToken cancellationToken)
    {
        await LockConversationAsync(request.ConversationId, cancellationToken);
        ConversationParticipant? participant = await dbContext.ConversationParticipants.SingleOrDefaultAsync(candidate =>
            candidate.ConversationId == request.ConversationId &&
            candidate.UserId == request.UserId &&
            candidate.LeftAt == null &&
            dbContext.Users.Any(user => user.Id == request.UserId && user.IsActive),
            cancellationToken);
        if (participant is null)
        {
            return NotFound<ConversationOperationResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            participant.Leave(now);
        }
        catch (DomainRuleException exception)
        {
            return RuleFailure<ConversationOperationResponse>(exception);
        }

        AddAudit(request.UserId, "communication.conversation-left", "Conversation", request.ConversationId, now);
        AddOutbox(
            "communication.conversation-left",
            new { conversationId = request.ConversationId, userId = request.UserId, occurredAt = now },
            now);
        return Result.Success(new ConversationOperationResponse(true));
    }

    public async Task<Result<NotificationPageResponse>> GetNotificationsAsync(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        bool resync = request.AfterSequence.HasValue;
        string canonicalQuery = resync
            ? $"notifications|{request.UserId:D}|sequence-asc|after:{request.AfterSequence}|{request.Limit}"
            : $"notifications|{request.UserId:D}|sequence-desc|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "notifications",
                canonicalQuery,
                out _,
                out _,
                out string? afterKey))
        {
            return CursorInvalid<NotificationPageResponse>();
        }

        IQueryable<Notification> query = dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == request.UserId);
        List<Notification> notifications;
        if (resync)
        {
            if (!TryReadSequenceCursor(afterKey, request.AfterSequence!.Value, out long sequence))
            {
                return CursorInvalid<NotificationPageResponse>();
            }

            notifications = await query
                .Where(notification => notification.Sequence > sequence)
                .OrderBy(notification => notification.Sequence)
                .Take(request.Limit + 1)
                .ToListAsync(cancellationToken);
        }
        else
        {
            if (!TryReadDescendingSequenceCursor(afterKey, out long? beforeSequence))
            {
                return CursorInvalid<NotificationPageResponse>();
            }
            if (beforeSequence is { } sequence)
            {
                query = query.Where(notification => notification.Sequence < sequence);
            }

            notifications = await query
                .OrderByDescending(notification => notification.Sequence)
                .Take(request.Limit + 1)
                .ToListAsync(cancellationToken);
        }

        bool hasMore = notifications.Count > request.Limit;
        Notification[] page = notifications.Take(request.Limit).ToArray();
        IReadOnlyList<NotificationResponse> responses = await MapNotificationsAsync(page, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create(
                "notifications",
                canonicalQuery,
                null,
                null,
                page[^1].Sequence.ToString(CultureInfo.InvariantCulture))
            : null;
        (long latestSequence, long unreadCount) = await GetNotificationSummaryAsync(
            request.UserId,
            cancellationToken);
        return Result.Success(new NotificationPageResponse(
            responses,
            nextCursor,
            hasMore,
            latestSequence,
            unreadCount));
    }

    public async Task<Result<NotificationUnreadCountResponse>> GetNotificationUnreadCountAsync(
        GetNotificationUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        (long latestSequence, long unreadCount) = await GetNotificationSummaryAsync(
            request.UserId,
            cancellationToken);
        return Result.Success(new NotificationUnreadCountResponse(unreadCount, latestSequence));
    }

    public async Task<Result<NotificationResponse>> MarkNotificationReadAsync(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        Notification? notification = await dbContext.Notifications.SingleOrDefaultAsync(
            candidate => candidate.Id == request.NotificationId && candidate.UserId == request.UserId,
            cancellationToken);
        if (notification is null)
        {
            return NotificationNotFound<NotificationResponse>();
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        return Result.Success(await MapNotificationAsync(notification, cancellationToken));
    }

    public async Task<Result<NotificationsReadResponse>> MarkAllNotificationsReadAsync(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        await LockNotificationSequenceAsync(request.UserId, cancellationToken);
        long throughSequence = await dbContext.NotificationSequences.AsNoTracking()
            .Where(sequence => sequence.UserId == request.UserId)
            .Select(sequence => sequence.LastSequence)
            .SingleOrDefaultAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        int updatedCount = await dbContext.Notifications
            .Where(notification => notification.UserId == request.UserId &&
                !notification.IsRead &&
                notification.Sequence <= throughSequence)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.IsRead, true)
                    .SetProperty(notification => notification.ReadAt, now),
                cancellationToken);
        return Result.Success(new NotificationsReadResponse(updatedCount, throughSequence));
    }

    public async Task<Result<AnnouncementPageResponse>> GetAnnouncementsAsync(
        GetAnnouncementsQuery request,
        CancellationToken cancellationToken)
    {
        string canonicalQuery = $"announcements|{request.UserId:D}|{request.CourseId:D}|created-desc|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "announcements",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId))
        {
            return CursorInvalid<AnnouncementPageResponse>();
        }

        IQueryable<Announcement> query = dbContext.Announcements.AsNoTracking()
            .Where(announcement => announcement.CourseId == request.CourseId && announcement.DeletedAt == null);
        if (afterCreatedAt is { } timestamp && afterId is { } id)
        {
            query = query.Where(announcement =>
                announcement.CreatedAt < timestamp ||
                announcement.CreatedAt == timestamp && announcement.Id.CompareTo(id) < 0);
        }

        List<Announcement> announcements = await query
            .OrderByDescending(announcement => announcement.CreatedAt)
            .ThenByDescending(announcement => announcement.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = announcements.Count > request.Limit;
        Announcement[] page = announcements.Take(request.Limit).ToArray();
        IReadOnlyList<AnnouncementResponse> responses = await MapAnnouncementsAsync(page, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create("announcements", canonicalQuery, page[^1].CreatedAt, page[^1].Id)
            : null;
        return Result.Success(new AnnouncementPageResponse(responses, nextCursor, hasMore));
    }

    public async Task<Result<AnnouncementResponse>> GetAnnouncementAsync(
        GetAnnouncementQuery request,
        CancellationToken cancellationToken) =>
        await FindAnnouncementAsync(request.CourseId, request.AnnouncementId, cancellationToken);

    public async Task<Result<AnnouncementResponse>> CreateAnnouncementAsync(
        CreateAnnouncementCommand request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Courses.AsNoTracking().AnyAsync(
                course => course.Id == request.CourseId && course.DeletedAt == null,
                cancellationToken))
        {
            return AnnouncementNotFound<AnnouncementResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Announcement announcement;
        try
        {
            announcement = Announcement.Create(request.CourseId, request.UserId, request.Title, request.Body, now);
        }
        catch (DomainRuleException exception)
        {
            return RuleFailure<AnnouncementResponse>(exception);
        }

        dbContext.Announcements.Add(announcement);
        Guid[] recipientIds = await GetEligibleAnnouncementRecipientIdsAsync(
            announcement.CourseId,
            now,
            cancellationToken);
        await AddAnnouncementNotificationsAsync(
            announcement,
            recipientIds,
            now,
            cancellationToken);
        AddAudit(request.UserId, "communication.announcement-created", "Announcement", announcement.Id, now);
        AddOutbox(
            "communication.announcement-created",
            new
            {
                announcementId = announcement.Id,
                courseId = announcement.CourseId,
                createdByUserId = announcement.CreatedByUserId,
                version = announcement.Version,
                targetCount = recipientIds.LongLength,
                occurredAt = now,
            },
            now);
        return Result.Success(MapAnnouncement(announcement, recipientIds.LongLength));
    }

    public async Task<Result<AnnouncementResponse>> UpdateAnnouncementAsync(
        UpdateAnnouncementCommand request,
        CancellationToken cancellationToken)
    {
        await LockAnnouncementAsync(request.AnnouncementId, cancellationToken);
        Announcement? announcement = await dbContext.Announcements.SingleOrDefaultAsync(
            candidate => candidate.Id == request.AnnouncementId &&
                candidate.CourseId == request.CourseId &&
                candidate.DeletedAt == null,
            cancellationToken);
        if (announcement is null)
        {
            return AnnouncementNotFound<AnnouncementResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        bool changed;
        try
        {
            changed = announcement.Update(request.Title, request.Body, now);
        }
        catch (DomainRuleException exception)
        {
            return RuleFailure<AnnouncementResponse>(exception);
        }

        long targetCount;
        if (changed)
        {
            Guid[] recipientIds = await GetEligibleAnnouncementRecipientIdsAsync(
                announcement.CourseId,
                now,
                cancellationToken);
            await AddAnnouncementNotificationsAsync(
                announcement,
                recipientIds,
                now,
                cancellationToken);
            targetCount = recipientIds.LongLength;
            AddAudit(request.UserId, "communication.announcement-updated", "Announcement", announcement.Id, now);
            AddOutbox(
                "communication.announcement-updated",
                new
                {
                    announcementId = announcement.Id,
                    courseId = announcement.CourseId,
                    updatedByUserId = request.UserId,
                    version = announcement.Version,
                    targetCount,
                    occurredAt = now,
                },
                now);
        }
        else
        {
            targetCount = await GetAnnouncementTargetCountAsync(
                announcement.Id,
                announcement.Version,
                cancellationToken);
        }

        return Result.Success(MapAnnouncement(announcement, targetCount));
    }

    public async Task<Result<AnnouncementOperationResponse>> DeleteAnnouncementAsync(
        DeleteAnnouncementCommand request,
        CancellationToken cancellationToken)
    {
        await LockAnnouncementAsync(request.AnnouncementId, cancellationToken);
        Announcement? announcement = await dbContext.Announcements.SingleOrDefaultAsync(
            candidate => candidate.Id == request.AnnouncementId && candidate.CourseId == request.CourseId,
            cancellationToken);
        if (announcement is null)
        {
            return AnnouncementNotFound<AnnouncementOperationResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (announcement.Delete(request.UserId, now))
        {
            AddAudit(request.UserId, "communication.announcement-deleted", "Announcement", announcement.Id, now);
            AddOutbox(
                "communication.announcement-deleted",
                new
                {
                    announcementId = announcement.Id,
                    courseId = announcement.CourseId,
                    deletedByUserId = request.UserId,
                    version = announcement.Version,
                    occurredAt = now,
                },
                now);
        }

        return Result.Success(new AnnouncementOperationResponse(true));
    }

    public async Task<Result<ConversationResponse>> GetConversationForReplayAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(userId, conversationId, cancellationToken))
        {
            return NotFound<ConversationResponse>();
        }

        Conversation? conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == conversationId,
            cancellationToken);
        if (conversation is null)
        {
            return NotFound<ConversationResponse>();
        }

        IReadOnlyList<ConversationResponse> response = await MapConversationsAsync([conversation], cancellationToken);
        return Result.Success(response[0]);
    }

    public async Task<Result<MessageResponse>> GetMessageForReplayAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(userId, conversationId, cancellationToken))
        {
            return NotFound<MessageResponse>();
        }

        CommunicationMessage? message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == messageId && candidate.ConversationId == conversationId,
            cancellationToken);
        return message is null
            ? NotFound<MessageResponse>()
            : Result.Success(await MapMessageAsync(message, cancellationToken));
    }

    public async Task<Result<AnnouncementResponse>> GetAnnouncementForReplayAsync(
        Guid userId,
        Guid courseId,
        Guid announcementId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageCourseAsync(userId, courseId, cancellationToken))
        {
            return AnnouncementNotFound<AnnouncementResponse>();
        }

        return await FindAnnouncementAsync(courseId, announcementId, cancellationToken);
    }

    public Task<bool> CanAccessAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken) =>
        dbContext.ConversationParticipants.AsNoTracking().AnyAsync(participant =>
            participant.ConversationId == conversationId &&
            participant.UserId == userId &&
            participant.LeftAt == null &&
            dbContext.Users.Any(user => user.Id == userId && user.IsActive),
            cancellationToken);

    public async Task<bool> CanManageCourseAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        bool courseExists = await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId && course.DeletedAt == null,
            cancellationToken);
        if (!courseExists)
        {
            return false;
        }

        bool assigned = await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId &&
                (course.OwnerUserId == userId || dbContext.CourseInstructors.Any(instructor =>
                    instructor.CourseId == courseId &&
                    instructor.UserId == userId &&
                    (instructor.Role == CourseCollaboratorRole.Editor ||
                        instructor.Role == CourseCollaboratorRole.CoInstructor))),
            cancellationToken);
        return assigned || await HasPermissionAsync(userId, Permissions.CourseManageAny, cancellationToken);
    }

    private async Task<IReadOnlyList<ConversationResponse>> MapConversationsAsync(
        Conversation[] conversations,
        CancellationToken cancellationToken)
    {
        Guid[] conversationIds = conversations.Select(conversation => conversation.Id).ToArray();
        ParticipantRow[] rows = await (
            from participant in dbContext.ConversationParticipants.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on participant.UserId equals user.Id
            where conversationIds.Contains(participant.ConversationId) && participant.LeftAt == null && user.IsActive
            orderby participant.JoinedAt, participant.UserId
            select new ParticipantRow(
                participant.ConversationId,
                participant.UserId,
                user.DisplayName,
                participant.JoinedAt)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, ParticipantRow[]> participants = rows
            .GroupBy(row => row.ConversationId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return conversations.Select(conversation => new ConversationResponse(
            conversation.Id,
            conversation.CourseId,
            conversation.CreatedByUserId,
            participants.GetValueOrDefault(conversation.Id, [])
                .Select(MapParticipant)
                .ToArray(),
            conversation.LastSequence,
            conversation.CreatedAt,
            conversation.UpdatedAt)).ToArray();
    }

    private static ConversationResponse MapConversation(
        Conversation conversation,
        ConversationParticipant[] participants,
        Dictionary<Guid, string> names) => new(
        conversation.Id,
        conversation.CourseId,
        conversation.CreatedByUserId,
        participants
            .OrderBy(participant => participant.JoinedAt)
            .ThenBy(participant => participant.UserId)
            .Select(participant => new ConversationParticipantResponse(
                participant.UserId,
                names[participant.UserId],
                participant.JoinedAt))
            .ToArray(),
        conversation.LastSequence,
        conversation.CreatedAt,
        conversation.UpdatedAt);

    private async Task<IReadOnlyList<MessageResponse>> MapMessagesAsync(
        CommunicationMessage[] messages,
        CancellationToken cancellationToken)
    {
        Guid[] senderIds = messages.Select(message => message.SenderUserId).Distinct().ToArray();
        Dictionary<Guid, string> names = await dbContext.Users.AsNoTracking()
            .Where(user => senderIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return messages.Select(message => MapMessage(
            message,
            names.GetValueOrDefault(message.SenderUserId, "User"))).ToArray();
    }

    private async Task<MessageResponse> MapMessageAsync(
        CommunicationMessage message,
        CancellationToken cancellationToken)
    {
        string senderName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == message.SenderUserId)
            .Select(user => user.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "User";
        return MapMessage(message, senderName);
    }

    private static MessageResponse MapMessage(CommunicationMessage message, string senderName) => new(
        message.Id,
        message.ConversationId,
        message.SenderUserId,
        senderName,
        message.ClientMessageId,
        message.Sequence,
        message.Body,
        message.CreatedAt);

    private async Task<IReadOnlyList<NotificationResponse>> MapNotificationsAsync(
        Notification[] notifications,
        CancellationToken cancellationToken)
    {
        Guid[] messageIds = notifications
            .Where(notification => notification.MessageId.HasValue)
            .Select(notification => notification.MessageId!.Value)
            .Distinct()
            .ToArray();
        Guid[] announcementIds = notifications
            .Where(notification => notification.AnnouncementId.HasValue)
            .Select(notification => notification.AnnouncementId!.Value)
            .Distinct()
            .ToArray();
        MessageSource[] messageSources = messageIds.Length == 0
            ? []
            : await (
                from message in dbContext.Messages.AsNoTracking()
                join conversation in dbContext.Conversations.AsNoTracking()
                    on message.ConversationId equals conversation.Id
                where messageIds.Contains(message.Id)
                select new MessageSource(
                    message.Id,
                    message.ConversationId,
                    conversation.CourseId,
                    message.SenderUserId)).ToArrayAsync(cancellationToken);
        AnnouncementSource[] announcementSources = announcementIds.Length == 0
            ? []
            : await dbContext.Announcements.AsNoTracking()
                .Where(announcement => announcementIds.Contains(announcement.Id))
                .Select(announcement => new AnnouncementSource(
                    announcement.Id,
                    announcement.CourseId,
                    announcement.CreatedByUserId))
                .ToArrayAsync(cancellationToken);
        Dictionary<Guid, MessageSource> messages = messageSources.ToDictionary(source => source.Id);
        Dictionary<Guid, AnnouncementSource> announcements = announcementSources.ToDictionary(source => source.Id);
        return notifications.Select(notification =>
        {
            if (notification.MessageId is { } messageId && messages.TryGetValue(messageId, out MessageSource? message))
            {
                return new NotificationResponse(
                    notification.Id,
                    notification.Sequence,
                    "Message",
                    message.Id,
                    message.CourseId,
                    message.ConversationId,
                    message.ActorUserId,
                    null,
                    null,
                    null,
                    notification.IsRead,
                    notification.ReadAt,
                    notification.CreatedAt);
            }

            if (notification.AnnouncementId is { } announcementId &&
                announcements.TryGetValue(announcementId, out AnnouncementSource? announcement))
            {
                return new NotificationResponse(
                    notification.Id,
                    notification.Sequence,
                    "Announcement",
                    announcement.Id,
                    announcement.CourseId,
                    null,
                    announcement.ActorUserId,
                    notification.AnnouncementVersion,
                    notification.Title,
                    notification.Body,
                    notification.IsRead,
                    notification.ReadAt,
                    notification.CreatedAt);
            }

            return new NotificationResponse(
                notification.Id,
                notification.Sequence,
                notification.MessageId.HasValue ? "Message" : "Announcement",
                notification.MessageId ?? notification.AnnouncementId ?? Guid.Empty,
                null,
                null,
                Guid.Empty,
                notification.AnnouncementVersion,
                notification.Title,
                notification.Body,
                notification.IsRead,
                notification.ReadAt,
                notification.CreatedAt);
        }).ToArray();
    }

    private async Task<NotificationResponse> MapNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationResponse> responses = await MapNotificationsAsync([notification], cancellationToken);
        return responses[0];
    }

    private async Task<IReadOnlyList<AnnouncementResponse>> MapAnnouncementsAsync(
        Announcement[] announcements,
        CancellationToken cancellationToken)
    {
        Guid[] announcementIds = announcements.Select(announcement => announcement.Id).ToArray();
        TargetCountRow[] counts = await dbContext.AnnouncementTargets.AsNoTracking()
            .Where(target => announcementIds.Contains(target.AnnouncementId))
            .GroupBy(target => new { target.AnnouncementId, target.AnnouncementVersion })
            .Select(group => new TargetCountRow(
                group.Key.AnnouncementId,
                group.Key.AnnouncementVersion,
                group.LongCount()))
            .ToArrayAsync(cancellationToken);
        Dictionary<(Guid AnnouncementId, long Version), long> countByVersion = counts.ToDictionary(
            row => (row.AnnouncementId, row.Version),
            row => row.Count);
        return announcements
            .Select(announcement => MapAnnouncement(
                announcement,
                countByVersion.GetValueOrDefault((announcement.Id, announcement.Version))))
            .ToArray();
    }

    private async Task<Result<AnnouncementResponse>> FindAnnouncementAsync(
        Guid courseId,
        Guid announcementId,
        CancellationToken cancellationToken)
    {
        Announcement? announcement = await dbContext.Announcements.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == announcementId &&
                candidate.CourseId == courseId &&
                candidate.DeletedAt == null,
            cancellationToken);
        if (announcement is null)
        {
            return AnnouncementNotFound<AnnouncementResponse>();
        }

        long targetCount = await GetAnnouncementTargetCountAsync(
            announcement.Id,
            announcement.Version,
            cancellationToken);
        return Result.Success(MapAnnouncement(announcement, targetCount));
    }

    private static AnnouncementResponse MapAnnouncement(Announcement announcement, long targetCount) => new(
        announcement.Id,
        announcement.CourseId,
        announcement.CreatedByUserId,
        announcement.Title,
        announcement.Body,
        announcement.Version,
        targetCount,
        announcement.CreatedAt,
        announcement.UpdatedAt);

    private static ConversationParticipantResponse MapParticipant(ParticipantRow row) => new(
        row.UserId,
        row.DisplayName,
        row.JoinedAt);

    private async Task AddMessageNotificationsAsync(
        CommunicationMessage message,
        Guid[] recipientIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (Guid recipientId in recipientIds.Order())
        {
            NotificationSequence sequence = await GetOrCreateNotificationSequenceAsync(
                recipientId,
                cancellationToken);
            long nextSequence = sequence.Advance();
            Notification notification = Notification.CreateForMessage(
                recipientId,
                message.Id,
                nextSequence,
                now);
            dbContext.Notifications.Add(notification);
        }
    }

    private async Task AddAnnouncementNotificationsAsync(
        Announcement announcement,
        Guid[] recipientIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (Guid recipientId in recipientIds.Order())
        {
            NotificationSequence sequence = await GetOrCreateNotificationSequenceAsync(
                recipientId,
                cancellationToken);
            long nextSequence = sequence.Advance();
            Notification notification = Notification.CreateForAnnouncement(
                recipientId,
                announcement.Id,
                announcement.Version,
                nextSequence,
                announcement.Title,
                announcement.Body,
                now);
            dbContext.Notifications.Add(notification);
            dbContext.AnnouncementTargets.Add(AnnouncementTarget.Create(
                announcement.Id,
                recipientId,
                announcement.Version,
                notification.Id,
                now));
        }
    }

    private async Task<NotificationSequence> GetOrCreateNotificationSequenceAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await LockNotificationSequenceAsync(userId, cancellationToken);
        NotificationSequence? sequence = await dbContext.NotificationSequences.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId,
            cancellationToken);
        if (sequence is not null)
        {
            return sequence;
        }

        sequence = NotificationSequence.Create(userId);
        dbContext.NotificationSequences.Add(sequence);
        return sequence;
    }

    private async Task<Guid[]> GetEligibleAnnouncementRecipientIdsAsync(
        Guid courseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join entitlement in dbContext.Entitlements.AsNoTracking()
                on enrollment.EntitlementId equals entitlement.Id
            join user in dbContext.Users.AsNoTracking()
                on enrollment.UserId equals user.Id
            where enrollment.CourseId == courseId &&
                user.IsActive &&
                (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Completed) &&
                entitlement.UserId == enrollment.UserId &&
                entitlement.CourseId == enrollment.CourseId &&
                entitlement.Status == EntitlementStatus.Active &&
                (entitlement.ExpiresAt == null || entitlement.ExpiresAt > now)
            select enrollment.UserId)
        .Distinct()
        .OrderBy(userId => userId)
        .ToArrayAsync(cancellationToken);

    private Task<long> GetAnnouncementTargetCountAsync(
        Guid announcementId,
        long version,
        CancellationToken cancellationToken) =>
        dbContext.AnnouncementTargets.AsNoTracking().LongCountAsync(
            target => target.AnnouncementId == announcementId && target.AnnouncementVersion == version,
            cancellationToken);

    private async Task<(long LatestSequence, long UnreadCount)> GetNotificationSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        long latestSequence = await dbContext.NotificationSequences.AsNoTracking()
            .Where(sequence => sequence.UserId == userId)
            .Select(sequence => sequence.LastSequence)
            .SingleOrDefaultAsync(cancellationToken);
        long unreadCount = await dbContext.Notifications.AsNoTracking()
            .LongCountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);
        return (latestSequence, unreadCount);
    }

    private Task<int> LockNotificationSequenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"notification-sequence:{userId:D}"}, 0))",
            cancellationToken);

    private Task<int> LockAnnouncementAsync(Guid announcementId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"announcement:{announcementId:D}"}, 0))",
            cancellationToken);

    private static bool TryReadSequenceCursor(string? value, long expected, out long sequence)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) || sequence < expected)
        {
            sequence = expected;
            return value is null;
        }

        return true;
    }

    private static bool TryReadDescendingSequenceCursor(string? value, out long? sequence)
    {
        if (value is null)
        {
            sequence = null;
            return true;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
        {
            sequence = parsed;
            return true;
        }

        sequence = null;
        return false;
    }

    private Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking()
            .Join(
                dbContext.RoleClaims.AsNoTracking(),
                role => role.RoleId,
                claim => claim.RoleId,
                (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId &&
                item.claim.ClaimType == IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == permission,
                cancellationToken);

    private Task<int> LockConversationAsync(Guid conversationId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"conversation:{conversationId:D}"}, 0))",
            cancellationToken);

    private void AddAudit(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", null, now));

    private void AddOutbox(string eventType, object value, DateTimeOffset now)
    {
        string payload = JsonSerializer.Serialize(value, value.GetType(), DorosakJsonSerializer.Options);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(eventType, 1, payload, "{}", now));
    }

    private static Result<T> NotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "CONVERSATION.NOT_FOUND",
        "The conversation was not found or is not available to this account."));

    private static Result<T> NotificationNotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "NOTIFICATION.NOT_FOUND",
        "The notification was not found or is not available to this account."));

    private static Result<T> AnnouncementNotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "ANNOUNCEMENT.NOT_FOUND",
        "The announcement was not found or is not available to this account."));

    private static Result<T> CursorInvalid<T>() => Result.Failure<T>(ResultError.BusinessRule(
        "CURSOR.INVALID",
        "The communication cursor is invalid or does not match this query."));

    private static Result<T> RuleFailure<T>(DomainRuleException exception) =>
        Result.Failure<T>(ResultError.BusinessRule(exception.Code, exception.Message));

    private sealed record ParticipantRow(
        Guid ConversationId,
        Guid UserId,
        string DisplayName,
        DateTimeOffset JoinedAt);

    private sealed record MessageSource(
        Guid Id,
        Guid ConversationId,
        Guid? CourseId,
        Guid ActorUserId);

    private sealed record AnnouncementSource(
        Guid Id,
        Guid CourseId,
        Guid ActorUserId);

    private sealed record TargetCountRow(Guid AnnouncementId, long Version, long Count);
}
