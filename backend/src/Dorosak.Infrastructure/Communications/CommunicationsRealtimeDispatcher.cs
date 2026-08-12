using System.Text.Json;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dorosak.Infrastructure.Communications;

internal sealed class CommunicationsRealtimeDispatcher(
    DorosakDbContext dbContext,
    ICommunicationsRealtimePublisher publisher,
    TimeProvider timeProvider,
    ILogger<CommunicationsRealtimeDispatcher> logger) : ICommunicationsRealtimeDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
    };

    private static readonly Action<ILogger, Guid, string, Exception?> PublishFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5900, nameof(PublishFailed)),
            "Communications realtime outbox message {MessageId} failed with {ErrorCode}");

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        int processed = 0;
        for (int index = 0; index < 20; index++)
        {
            ClaimedMessage? claimed = await ClaimAsync(cancellationToken);
            if (claimed is null)
            {
                break;
            }

            try
            {
                if (claimed.Message.SchemaVersion != CommunicationsRealtimeEvents.SchemaVersion)
                {
                    if (await OutboxLease.TerminateAsync(
                            dbContext,
                            claimed.Message.Id,
                            claimed.LockToken,
                            timeProvider.GetUtcNow(),
                            "REALTIME.DEAD_LETTER.SCHEMA_INVALID",
                            logger,
                            cancellationToken))
                    {
                        processed++;
                    }

                    continue;
                }

                await PublishAsync(claimed.Message, cancellationToken);
                if (await CompleteAsync(claimed, cancellationToken))
                {
                    processed++;
                }
            }
            catch (JsonException)
            {
                if (await OutboxLease.TerminateAsync(
                        dbContext,
                        claimed.Message.Id,
                        claimed.LockToken,
                        timeProvider.GetUtcNow(),
                        "REALTIME.DEAD_LETTER.PAYLOAD_INVALID",
                        logger,
                        cancellationToken))
                {
                    processed++;
                }
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                string errorCode = exception.GetType().Name;
                PublishFailed(logger, claimed.Message.Id, errorCode, null);
                if (claimed.AttemptCount >= OutboxLease.MaximumAttempts)
                {
                    if (await OutboxLease.TerminateAsync(
                            dbContext,
                            claimed.Message.Id,
                            claimed.LockToken,
                            timeProvider.GetUtcNow(),
                            "REALTIME.DEAD_LETTER.MAX_RETRIES",
                            logger,
                            cancellationToken))
                    {
                        processed++;
                    }
                }
                else
                {
                    await ReleaseAsync(claimed, errorCode, cancellationToken);
                }
            }
        }

        return processed;
    }

    private async Task<ClaimedMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ClaimCoreAsync(cancellationToken));
    }

    private async Task<ClaimedMessage?> ClaimCoreAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        OutboxMessage? message = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT *
                FROM operations.outbox_messages
                WHERE processed_at IS NULL
                  AND available_at <= now()
                  AND (locked_until IS NULL OR locked_until <= now())
                  AND event_type IN (
                      'communication.conversation-created',
                      'communication.message-created',
                      'communication.conversation-left',
                      'communication.announcement-created',
                      'communication.announcement-updated',
                      'communication.announcement-deleted')
                ORDER BY available_at, occurred_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        Guid lockToken = Guid.CreateVersion7();
        if (!message.TryAcquire(timeProvider.GetUtcNow(), TimeSpan.FromMinutes(2), lockToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        int attemptCount = message.AttemptCount;
        dbContext.Entry(message).State = EntityState.Detached;
        return new ClaimedMessage(message, lockToken, attemptCount);
    }

    private Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        return message.EventType switch
        {
            CommunicationsRealtimeEvents.ConversationCreated =>
                PublishConversationCreatedAsync(message, cancellationToken),
            CommunicationsRealtimeEvents.MessageCreated =>
                PublishMessageCreatedAsync(message, cancellationToken),
            CommunicationsRealtimeEvents.ConversationLeft =>
                PublishConversationLeftAsync(message, cancellationToken),
            CommunicationsRealtimeEvents.AnnouncementCreated =>
                PublishAnnouncementCreatedAsync(message, cancellationToken),
            CommunicationsRealtimeEvents.AnnouncementUpdated =>
                PublishAnnouncementUpdatedAsync(message, cancellationToken),
            CommunicationsRealtimeEvents.AnnouncementDeleted =>
                PublishAnnouncementDeletedAsync(message, cancellationToken),
            _ => throw new InvalidOperationException("The communications realtime event type is unsupported."),
        };
    }

    private async Task PublishConversationCreatedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        ConversationCreatedRealtimePayload requested = ReadPayload<ConversationCreatedRealtimePayload>(message);
        ConversationSource? source = await dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.Id == requested.ConversationId)
            .Select(conversation => new ConversationSource(
                conversation.Id,
                conversation.CourseId,
                conversation.CreatedByUserId))
            .SingleOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            return;
        }

        Guid[] recipients = await GetCurrentConversationRecipientsAsync(
            source.Id,
            source.CourseId,
            cancellationToken);
        var payload = new ConversationCreatedRealtimePayload(
            source.Id,
            source.CreatedByUserId,
            source.CourseId);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private async Task PublishMessageCreatedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        MessageCreatedRealtimePayload requested = ReadPayload<MessageCreatedRealtimePayload>(message);
        MessageSource? source = await (
            from candidate in dbContext.Messages.AsNoTracking()
            join conversation in dbContext.Conversations.AsNoTracking()
                on candidate.ConversationId equals conversation.Id
            where candidate.Id == requested.MessageId &&
                candidate.ConversationId == requested.ConversationId
            select new MessageSource(
                candidate.Id,
                candidate.ConversationId,
                conversation.CourseId,
                candidate.SenderUserId,
                candidate.Sequence)).SingleOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            return;
        }

        Guid[] recipients = await GetCurrentConversationRecipientsAsync(
            source.ConversationId,
            source.CourseId,
            cancellationToken);
        var payload = new MessageCreatedRealtimePayload(
            source.Id,
            source.ConversationId,
            source.SenderUserId,
            source.Sequence);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private async Task PublishConversationLeftAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        ConversationLeftRealtimePayload requested = ReadPayload<ConversationLeftRealtimePayload>(message);
        ConversationSource? source = await dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.Id == requested.ConversationId)
            .Select(conversation => new ConversationSource(
                conversation.Id,
                conversation.CourseId,
                conversation.CreatedByUserId))
            .SingleOrDefaultAsync(cancellationToken);
        bool participantLeft = source is not null && await dbContext.ConversationParticipants.AsNoTracking()
            .AnyAsync(participant => participant.ConversationId == requested.ConversationId &&
                participant.UserId == requested.UserId &&
                participant.LeftAt != null,
                cancellationToken);
        if (source is null || !participantLeft)
        {
            return;
        }

        Guid[] recipients = await GetCurrentConversationRecipientsAsync(
            source.Id,
            source.CourseId,
            cancellationToken);
        if (await CanReceiveConversationEventAsync(requested.UserId, cancellationToken))
        {
            recipients = recipients
                .Append(requested.UserId)
                .Distinct()
                .Order()
                .ToArray();
        }
        var payload = new ConversationLeftRealtimePayload(source.Id, requested.UserId);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private async Task PublishAnnouncementCreatedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        AnnouncementCreatedRealtimePayload requested = ReadPayload<AnnouncementCreatedRealtimePayload>(message);
        AnnouncementSource? source = await FindAnnouncementAsync(
            requested.AnnouncementId,
            requested.CourseId,
            cancellationToken);
        if (source is null || requested.Version != 1 || requested.Version > source.Version)
        {
            return;
        }

        Guid[] recipients = await GetAnnouncementRecipientsAsync(
            source.CourseId,
            source.Id,
            requested.Version,
            includeTargets: true,
            cancellationToken);
        long targetCount = await GetAnnouncementTargetCountAsync(source.Id, requested.Version, cancellationToken);
        var payload = new AnnouncementCreatedRealtimePayload(
            source.Id,
            source.CourseId,
            source.CreatedByUserId,
            requested.Version,
            targetCount);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private async Task PublishAnnouncementUpdatedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        AnnouncementUpdatedRealtimePayload requested = ReadPayload<AnnouncementUpdatedRealtimePayload>(message);
        AnnouncementSource? source = await FindAnnouncementAsync(
            requested.AnnouncementId,
            requested.CourseId,
            cancellationToken);
        if (source is null || requested.UpdatedByUserId == Guid.Empty ||
            requested.Version <= 1 || requested.Version > source.Version)
        {
            return;
        }

        Guid[] recipients = await GetAnnouncementRecipientsAsync(
            source.CourseId,
            source.Id,
            requested.Version,
            includeTargets: true,
            cancellationToken);
        long targetCount = await GetAnnouncementTargetCountAsync(source.Id, requested.Version, cancellationToken);
        var payload = new AnnouncementUpdatedRealtimePayload(
            source.Id,
            source.CourseId,
            requested.UpdatedByUserId,
            requested.Version,
            targetCount);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private async Task PublishAnnouncementDeletedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        AnnouncementDeletedRealtimePayload requested = ReadPayload<AnnouncementDeletedRealtimePayload>(message);
        AnnouncementSource? source = await FindAnnouncementAsync(
            requested.AnnouncementId,
            requested.CourseId,
            cancellationToken);
        if (source?.DeletedByUserId is not { } deletedByUserId ||
            requested.Version != source.Version ||
            requested.DeletedByUserId != deletedByUserId)
        {
            return;
        }

        Guid[] recipients = await GetAnnouncementRecipientsAsync(
            source.CourseId,
            source.Id,
            requested.Version,
            includeTargets: false,
            cancellationToken);
        var payload = new AnnouncementDeletedRealtimePayload(
            source.Id,
            source.CourseId,
            deletedByUserId,
            source.Version);
        await PublishToUsersAsync(message, recipients, payload, cancellationToken);
    }

    private Task<AnnouncementSource?> FindAnnouncementAsync(
        Guid announcementId,
        Guid courseId,
        CancellationToken cancellationToken) =>
        dbContext.Announcements.AsNoTracking()
            .Where(announcement => announcement.Id == announcementId && announcement.CourseId == courseId)
            .Select(announcement => new AnnouncementSource(
                announcement.Id,
                announcement.CourseId,
                announcement.CreatedByUserId,
                announcement.Version,
                announcement.DeletedByUserId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Guid[]> GetCurrentConversationRecipientsAsync(
        Guid conversationId,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IQueryable<Guid> conversationReaders = GetUserIdsWithPermission(Permissions.ConversationReadOwn);
        IQueryable<Guid> courseManagers = GetUserIdsWithPermission(Permissions.CourseManageAny);
        return await dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                conversationReaders.Contains(user.Id) &&
                dbContext.ConversationParticipants.Any(participant =>
                    participant.ConversationId == conversationId &&
                    participant.UserId == user.Id &&
                    participant.LeftAt == null) &&
                dbContext.Courses.Any(course => course.Id == courseId && course.DeletedAt == null) &&
                (dbContext.Courses.Any(course => course.Id == courseId && course.OwnerUserId == user.Id) ||
                    dbContext.CourseInstructors.Any(instructor =>
                        instructor.CourseId == courseId &&
                        instructor.UserId == user.Id &&
                        (instructor.Role == CourseCollaboratorRole.Editor ||
                            instructor.Role == CourseCollaboratorRole.CoInstructor)) ||
                    dbContext.Enrollments.Any(enrollment =>
                        enrollment.UserId == user.Id &&
                        enrollment.CourseId == courseId &&
                        (enrollment.Status == EnrollmentStatus.Active ||
                            enrollment.Status == EnrollmentStatus.Completed) &&
                        dbContext.Entitlements.Any(entitlement =>
                            entitlement.Id == enrollment.EntitlementId &&
                            entitlement.UserId == enrollment.UserId &&
                            entitlement.CourseId == enrollment.CourseId &&
                            entitlement.Status == EntitlementStatus.Active &&
                            (entitlement.ExpiresAt == null || entitlement.ExpiresAt > now))) ||
                    courseManagers.Contains(user.Id)))
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<Guid[]> GetAnnouncementRecipientsAsync(
        Guid courseId,
        Guid announcementId,
        long version,
        bool includeTargets,
        CancellationToken cancellationToken)
    {
        Guid[] targetUserIds = includeTargets
            ? await GetAnnouncementTargetRecipientsAsync(announcementId, version, cancellationToken)
            : [];
        Guid[] managerUserIds = await GetAnnouncementManagerRecipientsAsync(courseId, cancellationToken);
        return targetUserIds
            .Concat(managerUserIds)
            .Distinct()
            .Order()
            .ToArray();
    }

    private Task<Guid[]> GetAnnouncementTargetRecipientsAsync(
        Guid announcementId,
        long version,
        CancellationToken cancellationToken)
    {
        IQueryable<Guid> notificationReaders = GetUserIdsWithPermission(Permissions.NotificationReadOwn);
        return dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                notificationReaders.Contains(user.Id) &&
                dbContext.AnnouncementTargets.Any(target =>
                    target.AnnouncementId == announcementId &&
                    target.AnnouncementVersion == version &&
                    target.UserId == user.Id))
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
    }

    private Task<Guid[]> GetAnnouncementManagerRecipientsAsync(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        IQueryable<Guid> announcementManagers = GetUserIdsWithPermission(Permissions.AnnouncementManageCourse);
        IQueryable<Guid> courseManagers = GetUserIdsWithPermission(Permissions.CourseManageAny);
        return dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                announcementManagers.Contains(user.Id) &&
                dbContext.Courses.Any(course => course.Id == courseId && course.DeletedAt == null) &&
                (dbContext.Courses.Any(course => course.Id == courseId && course.OwnerUserId == user.Id) ||
                    dbContext.CourseInstructors.Any(instructor =>
                        instructor.CourseId == courseId &&
                        instructor.UserId == user.Id &&
                        (instructor.Role == CourseCollaboratorRole.Editor ||
                            instructor.Role == CourseCollaboratorRole.CoInstructor)) ||
                    courseManagers.Contains(user.Id)))
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<Guid> GetUserIdsWithPermission(string permission) =>
        from role in dbContext.UserRoles.AsNoTracking()
        join claim in dbContext.RoleClaims.AsNoTracking() on role.RoleId equals claim.RoleId
        where claim.ClaimType == IdentityConstants.PermissionClaimType && claim.ClaimValue == permission
        select role.UserId;

    private Task<bool> CanReceiveConversationEventAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            GetUserIdsWithPermission(Permissions.ConversationReadOwn).Contains(user.Id),
            cancellationToken);

    private Task<long> GetAnnouncementTargetCountAsync(
        Guid announcementId,
        long version,
        CancellationToken cancellationToken) =>
        dbContext.AnnouncementTargets.AsNoTracking().LongCountAsync(
            target => target.AnnouncementId == announcementId && target.AnnouncementVersion == version,
            cancellationToken);

    private Task PublishToUsersAsync<TPayload>(
        OutboxMessage message,
        Guid[] recipients,
        TPayload payload,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        if (recipients.Length == 0)
        {
            return Task.CompletedTask;
        }

        var envelope = new CommunicationsRealtimeEnvelope<TPayload>(
            message.Id,
            message.EventType,
            message.SchemaVersion,
            message.OccurredAt,
            payload);
        return publisher.PublishAsync(recipients, envelope, cancellationToken);
    }

    private static TPayload ReadPayload<TPayload>(OutboxMessage message)
        where TPayload : class
    {
        TPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TPayload>(message.Payload, JsonOptions);
        }
        catch (JsonException)
        {
            throw new JsonException("The communications realtime payload is invalid.");
        }

        if (payload is null || !IsValidPayload(payload))
        {
            throw new JsonException("The communications realtime payload is invalid.");
        }

        return payload;
    }

    private static bool IsValidPayload<TPayload>(TPayload payload)
        where TPayload : class => payload switch
        {
            ConversationCreatedRealtimePayload value =>
                value.ConversationId != Guid.Empty && value.CreatedByUserId != Guid.Empty && value.CourseId != Guid.Empty,
            MessageCreatedRealtimePayload value =>
                value.MessageId != Guid.Empty && value.ConversationId != Guid.Empty &&
                value.SenderUserId != Guid.Empty && value.Sequence > 0,
            ConversationLeftRealtimePayload value =>
                value.ConversationId != Guid.Empty && value.UserId != Guid.Empty,
            AnnouncementCreatedRealtimePayload value =>
                value.AnnouncementId != Guid.Empty && value.CourseId != Guid.Empty &&
                value.CreatedByUserId != Guid.Empty && value.Version > 0 && value.TargetCount >= 0,
            AnnouncementUpdatedRealtimePayload value =>
                value.AnnouncementId != Guid.Empty && value.CourseId != Guid.Empty &&
                value.UpdatedByUserId != Guid.Empty && value.Version > 1 && value.TargetCount >= 0,
            AnnouncementDeletedRealtimePayload value =>
                value.AnnouncementId != Guid.Empty && value.CourseId != Guid.Empty &&
                value.DeletedByUserId != Guid.Empty && value.Version > 0,
            _ => false,
        };

    private Task<bool> CompleteAsync(ClaimedMessage claimed, CancellationToken cancellationToken) =>
        OutboxLease.CompleteAsync(
            dbContext,
            claimed.Message.Id,
            claimed.LockToken,
            timeProvider.GetUtcNow(),
            logger,
            cancellationToken);

    private Task<bool> ReleaseAsync(
        ClaimedMessage claimed,
        string errorCode,
        CancellationToken cancellationToken)
    {
        TimeSpan retryDelay = OutboxLease.GetRetryDelay(claimed.AttemptCount);
        return OutboxLease.ReleaseAsync(
            dbContext,
            claimed.Message.Id,
            claimed.LockToken,
            timeProvider.GetUtcNow().Add(retryDelay),
            errorCode,
            logger,
            cancellationToken);
    }

    private sealed record ClaimedMessage(OutboxMessage Message, Guid LockToken, int AttemptCount);

    private sealed record ConversationSource(Guid Id, Guid CourseId, Guid CreatedByUserId);

    private sealed record MessageSource(
        Guid Id,
        Guid ConversationId,
        Guid CourseId,
        Guid SenderUserId,
        long Sequence);

    private sealed record AnnouncementSource(
        Guid Id,
        Guid CourseId,
        Guid CreatedByUserId,
        long Version,
        Guid? DeletedByUserId);
}

public static class CommunicationsRealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddCommunicationsRealtimeDispatching(this IServiceCollection services)
    {
        services.AddScoped<ICommunicationsRealtimeDispatcher, CommunicationsRealtimeDispatcher>();
        return services;
    }
}
