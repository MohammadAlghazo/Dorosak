using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Communications;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Application.IntegrationTests.Phase9;

[Collection(InfrastructureTestGroup.Name)]
public sealed class Phase9CommunicationsWorkflowTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task ConversationsEnforceCurrentParticipantsDedupeIdorAndSignedCursors()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid creatorId = await CreateUserAsync(
            "phase9-conversation-creator",
            DorosakIdentityConstants.TeacherRole);
        Guid participantId = await CreateUserAsync("phase9-conversation-participant");
        Guid outsiderId = await CreateUserAsync("phase9-conversation-outsider");
        Guid courseId = await SeedConversationCourseAsync(creatorId, participantId, cancellationToken);
        string conversationKey = Guid.CreateVersion7().ToString("N");
        var createConversation = new CreateConversationCommand(
            creatorId,
            [participantId],
            courseId,
            conversationKey);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<ConversationResponse> conversation = await sender.Send(createConversation, cancellationToken);
        Result<ConversationResponse> conversationReplay = await sender.Send(createConversation, cancellationToken);

        Assert.True(conversation.IsSuccess);
        Assert.Equal(conversation.Value.Id, conversationReplay.Value.Id);
        Assert.Equal(2, conversation.Value.Participants.Count);

        Guid firstClientMessageId = Guid.CreateVersion7();
        var firstMessage = new CreateMessageCommand(
            creatorId,
            conversation.Value.Id,
            firstClientMessageId,
            "First synthetic conversation message.",
            Guid.CreateVersion7().ToString("N"));
        Result<MessageResponse> created = await sender.Send(firstMessage, cancellationToken);
        Result<MessageResponse> replayed = await sender.Send(firstMessage, cancellationToken);
        Result<MessageResponse> clientDedupe = await sender.Send(
            firstMessage with { IdempotencyKey = Guid.CreateVersion7().ToString("N") },
            cancellationToken);
        Result<MessageResponse> conflictingClientId = await sender.Send(
            firstMessage with
            {
                Body = "Different content must not reuse the client identity.",
                IdempotencyKey = Guid.CreateVersion7().ToString("N"),
            },
            cancellationToken);

        Assert.Equal(created.Value.Id, replayed.Value.Id);
        Assert.Equal(created.Value.Id, clientDedupe.Value.Id);
        Assert.Equal(1, created.Value.Sequence);
        Assert.False(conflictingClientId.IsSuccess);
        Assert.Equal("MESSAGE.CLIENT_MESSAGE_ID_REUSED", conflictingClientId.Failure.Code);

        Guid concurrentClientId = Guid.CreateVersion7();
        var concurrentOne = new CreateMessageCommand(
            participantId,
            conversation.Value.Id,
            concurrentClientId,
            "A concurrent message is persisted once.",
            Guid.CreateVersion7().ToString("N"));
        CreateMessageCommand concurrentTwo = concurrentOne with
        {
            IdempotencyKey = Guid.CreateVersion7().ToString("N"),
        };
        Result<MessageResponse>[] concurrent = await Task.WhenAll(
            SendInNewScopeAsync(concurrentOne, cancellationToken),
            SendInNewScopeAsync(concurrentTwo, cancellationToken));
        Assert.All(concurrent, result => Assert.True(result.IsSuccess));
        Assert.Single(concurrent.Select(result => result.Value.Id).Distinct());
        Assert.All(concurrent, result => Assert.Equal(2, result.Value.Sequence));

        Result<MessageResponse>[] distinctConcurrent = await Task.WhenAll(
            SendInNewScopeAsync(
                new CreateMessageCommand(
                    creatorId,
                    conversation.Value.Id,
                    Guid.CreateVersion7(),
                    "First distinct concurrent message.",
                    Guid.CreateVersion7().ToString("N")),
                cancellationToken),
            SendInNewScopeAsync(
                new CreateMessageCommand(
                    creatorId,
                    conversation.Value.Id,
                    Guid.CreateVersion7(),
                    "Second distinct concurrent message.",
                    Guid.CreateVersion7().ToString("N")),
                cancellationToken));
        Assert.All(distinctConcurrent, result => Assert.True(result.IsSuccess));
        Assert.Equal(new long[] { 3, 4 }, distinctConcurrent.Select(result => result.Value.Sequence).Order().ToArray());

        Result<MessagePageResponse> firstPage = await sender.Send(
            new GetConversationMessagesQuery(creatorId, conversation.Value.Id, 1, null),
            cancellationToken);
        Assert.True(firstPage.Value.HasMore);
        Assert.NotNull(firstPage.Value.NextCursor);
        Result<MessagePageResponse> secondPage = await sender.Send(
            new GetConversationMessagesQuery(
                creatorId,
                conversation.Value.Id,
                1,
                firstPage.Value.NextCursor),
            cancellationToken);
        Assert.Single(secondPage.Value.Items);
        Assert.DoesNotContain(secondPage.Value.Items, item => item.Id == firstPage.Value.Items[0].Id);
        Assert.Equal(4, firstPage.Value.LatestSequence);

        Result<MessagePageResponse> resync = await sender.Send(
            new GetConversationMessagesQuery(creatorId, conversation.Value.Id, 20, null, 1),
            cancellationToken);
        Assert.Equal(new long[] { 2, 3, 4 }, resync.Value.Items.Select(message => message.Sequence).ToArray());
        Assert.Equal(4, resync.Value.LatestSequence);

        Result<NotificationPageResponse> creatorNotifications = await sender.Send(
            new GetNotificationsQuery(creatorId, 20, null, 0),
            cancellationToken);
        Result<NotificationPageResponse> participantNotifications = await sender.Send(
            new GetNotificationsQuery(participantId, 20, null, 0),
            cancellationToken);
        NotificationResponse creatorNotification = Assert.Single(creatorNotifications.Value.Items);
        Assert.Equal(new long[] { 1, 2, 3 }, participantNotifications.Value.Items
            .Select(notification => notification.Sequence)
            .ToArray());
        NotificationResponse participantNotification = participantNotifications.Value.Items[0];
        Assert.Equal("Message", creatorNotification.Type);
        Assert.Equal("Message", participantNotification.Type);
        Assert.Equal(1, creatorNotification.Sequence);
        Assert.Equal(1, participantNotification.Sequence);
        Assert.Null(creatorNotification.Body);
        Assert.Equal(1, creatorNotifications.Value.UnreadCount);

        Result<NotificationResponse> foreignNotificationRead = await sender.Send(
            new MarkNotificationReadCommand(outsiderId, participantNotification.Id),
            cancellationToken);
        Assert.False(foreignNotificationRead.IsSuccess);
        Assert.Equal("NOTIFICATION.NOT_FOUND", foreignNotificationRead.Failure.Code);
        Result<NotificationResponse> ownedNotificationRead = await sender.Send(
            new MarkNotificationReadCommand(participantId, participantNotification.Id),
            cancellationToken);
        Assert.True(ownedNotificationRead.Value.IsRead);
        Result<NotificationUnreadCountResponse> participantUnread = await sender.Send(
            new GetNotificationUnreadCountQuery(participantId),
            cancellationToken);
        Assert.Equal(2, participantUnread.Value.Count);
        Result<NotificationsReadResponse> readAll = await sender.Send(
            new MarkAllNotificationsReadCommand(creatorId),
            cancellationToken);
        Assert.Equal(1, readAll.Value.UpdatedCount);
        Assert.Equal(1, readAll.Value.ThroughSequence);

        string tamperedCursor = string.Concat(firstPage.Value.NextCursor, "x");
        Result<MessagePageResponse> invalidCursor = await sender.Send(
            new GetConversationMessagesQuery(creatorId, conversation.Value.Id, 1, tamperedCursor),
            cancellationToken);
        Assert.False(invalidCursor.IsSuccess);
        Assert.Equal("CURSOR.INVALID", invalidCursor.Failure.Code);

        ResourceNotFoundException foreignRead = await Assert.ThrowsAsync<ResourceNotFoundException>(() => sender.Send(
            new GetConversationMessagesQuery(outsiderId, conversation.Value.Id, 20, null),
            cancellationToken));
        Assert.Equal("CONVERSATION.NOT_FOUND", foreignRead.Code);

        Result<ConversationOperationResponse> left = await sender.Send(
            new LeaveConversationCommand(participantId, conversation.Value.Id),
            cancellationToken);
        Assert.True(left.IsSuccess);
        Assert.True(left.Value.Completed);
        ResourceNotFoundException formerParticipantRead = await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            SendInNewScopeAsync(
                new GetConversationMessagesQuery(participantId, conversation.Value.Id, 20, null),
                cancellationToken));
        Assert.Equal("CONVERSATION.NOT_FOUND", formerParticipantRead.Code);
        ResourceNotFoundException formerParticipantSend = await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            SendInNewScopeAsync(
                new CreateMessageCommand(
                    participantId,
                    conversation.Value.Id,
                    Guid.CreateVersion7(),
                    "A former participant cannot send.",
                    Guid.CreateVersion7().ToString("N")),
                cancellationToken));
        Assert.Equal("CONVERSATION.NOT_FOUND", formerParticipantSend.Code);
        ResourceNotFoundException repeatedLeave = await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            SendInNewScopeAsync(
                new LeaveConversationCommand(participantId, conversation.Value.Id),
                cancellationToken));
        Assert.Equal("CONVERSATION.NOT_FOUND", repeatedLeave.Code);

        Result<ConversationPageResponse> participantList = await SendInNewScopeAsync(
            new GetConversationsQuery(participantId, 20, null),
            cancellationToken);
        Assert.DoesNotContain(participantList.Value.Items, item => item.Id == conversation.Value.Id);
        Result<ConversationPageResponse> creatorList = await SendInNewScopeAsync(
            new GetConversationsQuery(creatorId, 20, null),
            cancellationToken);
        ConversationResponse currentConversation = Assert.Single(
            creatorList.Value.Items,
            item => item.Id == conversation.Value.Id);
        Assert.Single(currentConversation.Participants);
        Assert.Equal(creatorId, currentConversation.Participants[0].UserId);
        Assert.Equal(4, currentConversation.LastSequence);

        await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        Assert.Equal(4, await dbContext.Set<Message>().CountAsync(
            message => message.ConversationId == conversation.Value.Id,
            cancellationToken));
        Assert.Equal(4, await dbContext.Set<Notification>().CountAsync(
            notification => notification.MessageId != null && dbContext.Set<Message>()
                .Any(message => message.Id == notification.MessageId && message.ConversationId == conversation.Value.Id),
            cancellationToken));
        Assert.Equal(1, await dbContext.Set<NotificationSequence>().CountAsync(
            sequence => sequence.UserId == creatorId && sequence.LastSequence == 1,
            cancellationToken));
        Assert.Equal(1, await dbContext.Set<NotificationSequence>().CountAsync(
            sequence => sequence.UserId == participantId && sequence.LastSequence == 3,
            cancellationToken));
        Assert.Equal(6, await dbContext.Set<AuditLog>().CountAsync(
            audit => audit.TargetId == conversation.Value.Id ||
                audit.TargetType == "Message" && dbContext.Set<Message>()
                    .Any(message => message.Id == audit.TargetId && message.ConversationId == conversation.Value.Id),
            cancellationToken));
        OutboxMessage[] outbox = await dbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(message => message.EventType.StartsWith("communication."))
            .ToArrayAsync(cancellationToken);
        OutboxMessage[] conversationOutbox = outbox
            .Where(message => message.Payload.Contains(conversation.Value.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(6, conversationOutbox.Length);
        Assert.All(
            conversationOutbox.Where(message => message.EventType == "communication.message-created"),
            message => Assert.Contains("\"sequence\":", message.Payload, StringComparison.Ordinal));
        Assert.All(conversationOutbox, message => Assert.DoesNotContain(
            "synthetic conversation message",
            message.Payload,
            StringComparison.OrdinalIgnoreCase));
        Assert.All(conversationOutbox, message => Assert.DoesNotContain(
            "\"body\"",
            message.Payload,
            StringComparison.OrdinalIgnoreCase));
        IdempotencyRecord[] messageIdempotencyRecords = await dbContext.Set<IdempotencyRecord>().AsNoTracking()
            .Where(record => record.Operation == "communications.message-create.v1")
            .ToArrayAsync(cancellationToken);
        Assert.NotEmpty(messageIdempotencyRecords);
        Assert.All(
            messageIdempotencyRecords,
            record => Assert.Equal(TimeSpan.FromHours(24), record.ExpiresAt - record.CreatedAt));
    }

    [Fact]
    public async Task ConversationCreationRequiresActiveUsersWithCurrentCourseAccess()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid ownerId = await CreateUserAsync(
            "phase9-conversation-access-owner",
            DorosakIdentityConstants.TeacherRole);
        Guid editorId = await CreateUserAsync(
            "phase9-conversation-access-editor",
            DorosakIdentityConstants.TeacherRole);
        Guid coInstructorId = await CreateUserAsync(
            "phase9-conversation-access-co-instructor",
            DorosakIdentityConstants.TeacherRole);
        Guid reviewerId = await CreateUserAsync(
            "phase9-conversation-access-reviewer",
            DorosakIdentityConstants.TeacherRole);
        Guid activeLearnerId = await CreateUserAsync("phase9-conversation-access-active");
        Guid completedLearnerId = await CreateUserAsync("phase9-conversation-access-completed");
        Guid revokedEnrollmentLearnerId = await CreateUserAsync(
            "phase9-conversation-access-revoked-enrollment");
        Guid revokedEntitlementLearnerId = await CreateUserAsync(
            "phase9-conversation-access-revoked-entitlement");
        Guid expiredEntitlementLearnerId = await CreateUserAsync(
            "phase9-conversation-access-expired-entitlement");
        Guid inactiveReviewerId = await CreateUserAsync(
            "phase9-conversation-access-inactive-reviewer",
            DorosakIdentityConstants.TeacherRole);
        Guid outsiderId = await CreateUserAsync("phase9-conversation-access-outsider");
        Guid adminId = await CreateUserAsync(
            "phase9-conversation-access-admin",
            DorosakIdentityConstants.AdminRole);
        Guid courseId = await SeedConversationAccessCourseAsync(
            ownerId,
            editorId,
            coInstructorId,
            reviewerId,
            activeLearnerId,
            completedLearnerId,
            revokedEnrollmentLearnerId,
            revokedEntitlementLearnerId,
            expiredEntitlementLearnerId,
            inactiveReviewerId,
            cancellationToken);

        foreach (Guid participantId in new[]
                 {
                     editorId,
                     coInstructorId,
                     reviewerId,
                     activeLearnerId,
                     completedLearnerId,
                 })
        {
            Result<ConversationResponse> allowed = await SendInNewScopeAsync(
                CreateConversation(ownerId, participantId, courseId),
                cancellationToken);
            Assert.True(allowed.IsSuccess);
            Assert.Equal(courseId, allowed.Value.CourseId);
        }

        foreach (Guid participantId in new[]
                 {
                     revokedEnrollmentLearnerId,
                     revokedEntitlementLearnerId,
                     expiredEntitlementLearnerId,
                     inactiveReviewerId,
                     outsiderId,
                     adminId,
                     Guid.CreateVersion7(),
                 })
        {
            Result<ConversationResponse> denied = await SendInNewScopeAsync(
                CreateConversation(ownerId, participantId, courseId),
                cancellationToken);
            Assert.False(denied.IsSuccess);
            Assert.Equal("CONVERSATION.NOT_FOUND", denied.Failure.Code);
        }

        Result<ConversationResponse> missingCourse = await SendInNewScopeAsync(
            CreateConversation(ownerId, activeLearnerId, Guid.CreateVersion7()),
            cancellationToken);
        Assert.False(missingCourse.IsSuccess);
        Assert.Equal("CONVERSATION.NOT_FOUND", missingCourse.Failure.Code);

        Result<ConversationResponse> adminCreator = await SendInNewScopeAsync(
            CreateConversation(adminId, activeLearnerId, courseId),
            cancellationToken);
        Assert.True(adminCreator.IsSuccess);
        Result<ConversationResponse> adminCannotAddOutsider = await SendInNewScopeAsync(
            CreateConversation(adminId, outsiderId, courseId),
            cancellationToken);
        Assert.False(adminCannotAddOutsider.IsSuccess);
        Assert.Equal("CONVERSATION.NOT_FOUND", adminCannotAddOutsider.Failure.Code);

        await SetUserActiveAsync(ownerId, false, cancellationToken);
        Result<ConversationResponse> inactiveCreator = await SendInNewScopeAsync(
            CreateConversation(ownerId, activeLearnerId, courseId),
            cancellationToken);
        Assert.False(inactiveCreator.IsSuccess);
        Assert.Equal("CONVERSATION.NOT_FOUND", inactiveCreator.Failure.Code);
    }

    [Fact]
    public async Task LeaveAndSendUseOneConversationLockAndLeaveWinnerPreventsMessage()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid creatorId = await CreateUserAsync(
            "phase9-conversation-race-creator",
            DorosakIdentityConstants.TeacherRole);
        Guid participantId = await CreateUserAsync("phase9-conversation-race-participant");
        Guid courseId = await SeedConversationCourseAsync(creatorId, participantId, cancellationToken);
        Result<ConversationResponse> created = await SendInNewScopeAsync(
            CreateConversation(creatorId, participantId, courseId),
            cancellationToken);
        Assert.True(created.IsSuccess);

        await using var gateConnection = new NpgsqlConnection(fixture.DatabaseConnection);
        await gateConnection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction gateTransaction = await gateConnection.BeginTransactionAsync(cancellationToken);
        await using (var gateCommand = new NpgsqlCommand(
                         "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 0))",
                         gateConnection,
                         gateTransaction))
        {
            gateCommand.Parameters.AddWithValue("identity", $"conversation:{created.Value.Id:D}");
            await gateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var observerConnection = new NpgsqlConnection(fixture.DatabaseConnection);
        await observerConnection.OpenAsync(cancellationToken);
        long baselineWaiters = await CountAdvisoryWaitersAsync(observerConnection, cancellationToken);
        Task<Result<ConversationOperationResponse>> leaveTask = SendInNewScopeAsync(
            new LeaveConversationCommand(participantId, created.Value.Id),
            cancellationToken);
        bool gateReleased = false;
        try
        {
            await WaitForAdvisoryWaitersAsync(observerConnection, baselineWaiters + 1, cancellationToken);
            Guid clientMessageId = Guid.CreateVersion7();
            Task<Result<MessageResponse>> sendTask = SendInNewScopeAsync(
                new CreateMessageCommand(
                    participantId,
                    created.Value.Id,
                    clientMessageId,
                    "This message must not cross a completed leave.",
                    Guid.CreateVersion7().ToString("N")),
                cancellationToken);
            await WaitForAdvisoryWaitersAsync(observerConnection, baselineWaiters + 2, cancellationToken);
            await gateTransaction.CommitAsync(cancellationToken);
            gateReleased = true;

            Result<ConversationOperationResponse> leave = await leaveTask;
            Result<MessageResponse> send = await sendTask;
            Assert.True(leave.IsSuccess);
            Assert.False(send.IsSuccess);
            Assert.Equal("CONVERSATION.NOT_FOUND", send.Failure.Code);

            await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
            DorosakDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            ConversationParticipant participant = await dbContext.Set<ConversationParticipant>().AsNoTracking()
                .SingleAsync(
                    item => item.ConversationId == created.Value.Id && item.UserId == participantId,
                    cancellationToken);
            Assert.NotNull(participant.LeftAt);
            Assert.False(await dbContext.Set<Message>().AnyAsync(
                message => message.ConversationId == created.Value.Id &&
                    message.ClientMessageId == clientMessageId,
                cancellationToken));
            Assert.Equal(0, await dbContext.Set<Conversation>().AsNoTracking()
                .Where(conversation => conversation.Id == created.Value.Id)
                .Select(conversation => conversation.LastSequence)
                .SingleAsync(cancellationToken));
        }
        finally
        {
            if (!gateReleased)
            {
                await gateTransaction.RollbackAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task AnnouncementsAuthorizeCourseManagersAndTargetOnlyEntitledLearners()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid ownerId = await CreateUserAsync("phase9-announcement-owner", DorosakIdentityConstants.TeacherRole);
        Guid editorId = await CreateUserAsync("phase9-announcement-editor", DorosakIdentityConstants.TeacherRole);
        Guid coInstructorId = await CreateUserAsync("phase9-announcement-co-instructor", DorosakIdentityConstants.TeacherRole);
        Guid reviewerId = await CreateUserAsync("phase9-announcement-reviewer", DorosakIdentityConstants.TeacherRole);
        Guid adminId = await CreateUserAsync("phase9-announcement-admin", DorosakIdentityConstants.AdminRole);
        Guid activeLearnerId = await CreateUserAsync("phase9-announcement-active");
        Guid completedLearnerId = await CreateUserAsync("phase9-announcement-completed");
        Guid revokedEnrollmentLearnerId = await CreateUserAsync("phase9-announcement-revoked-enrollment");
        Guid revokedEntitlementLearnerId = await CreateUserAsync("phase9-announcement-revoked-entitlement");
        Guid unenrolledLearnerId = await CreateUserAsync("phase9-announcement-unenrolled");
        Guid courseId = await SeedAnnouncementCourseAsync(
            ownerId,
            editorId,
            coInstructorId,
            reviewerId,
            activeLearnerId,
            completedLearnerId,
            revokedEnrollmentLearnerId,
            revokedEntitlementLearnerId,
            cancellationToken);

        var create = new CreateAnnouncementCommand(
            ownerId,
            courseId,
            "First course announcement",
            "Only currently entitled learners receive this body.",
            Guid.CreateVersion7().ToString("N"));
        Result<AnnouncementResponse> created = await SendInNewScopeAsync(create, cancellationToken);
        Result<AnnouncementResponse> createReplay = await SendInNewScopeAsync(create, cancellationToken);

        Assert.True(created.IsSuccess);
        Assert.Equal(created.Value.Id, createReplay.Value.Id);
        Assert.Equal(1, created.Value.Version);
        Assert.Equal(2, created.Value.TargetCount);

        ResourceNotFoundException reviewerDenied = await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            SendInNewScopeAsync(
                new GetAnnouncementsQuery(reviewerId, courseId, 20, null),
                cancellationToken));
        Assert.Equal("ANNOUNCEMENT.NOT_FOUND", reviewerDenied.Code);

        var update = new UpdateAnnouncementCommand(
            editorId,
            courseId,
            created.Value.Id,
            "Revised course announcement",
            "The revised projection is delivered only to learners who remain entitled.",
            Guid.CreateVersion7().ToString("N"));
        Result<AnnouncementResponse> updated = await SendInNewScopeAsync(update, cancellationToken);
        Result<AnnouncementResponse> updateReplay = await SendInNewScopeAsync(update, cancellationToken);
        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value.Version);
        Assert.Equal(2, updated.Value.TargetCount);
        Assert.Equal(updated.Value.Version, updateReplay.Value.Version);

        Result<AnnouncementResponse> coInstructorRead = await SendInNewScopeAsync(
            new GetAnnouncementQuery(coInstructorId, courseId, created.Value.Id),
            cancellationToken);
        Assert.True(coInstructorRead.IsSuccess);
        Result<AnnouncementPageResponse> adminRead = await SendInNewScopeAsync(
            new GetAnnouncementsQuery(adminId, courseId, 20, null),
            cancellationToken);
        Assert.Contains(adminRead.Value.Items, announcement => announcement.Id == created.Value.Id);

        Result<NotificationPageResponse> firstActivePage = await SendInNewScopeAsync(
            new GetNotificationsQuery(activeLearnerId, 1, null, 0),
            cancellationToken);
        Assert.True(firstActivePage.Value.HasMore);
        Assert.NotNull(firstActivePage.Value.NextCursor);
        NotificationResponse firstProjection = Assert.Single(firstActivePage.Value.Items);
        Assert.Equal(1, firstProjection.Sequence);
        Assert.Equal("First course announcement", firstProjection.Title);
        Result<NotificationPageResponse> secondActivePage = await SendInNewScopeAsync(
            new GetNotificationsQuery(activeLearnerId, 1, firstActivePage.Value.NextCursor, 0),
            cancellationToken);
        NotificationResponse revisedProjection = Assert.Single(secondActivePage.Value.Items);
        Assert.Equal(2, revisedProjection.Sequence);
        Assert.Equal("Revised course announcement", revisedProjection.Title);
        Assert.Equal(2, secondActivePage.Value.LatestSequence);

        Result<NotificationPageResponse> completedNotifications = await SendInNewScopeAsync(
            new GetNotificationsQuery(completedLearnerId, 20, null, 0),
            cancellationToken);
        Assert.Equal(2, completedNotifications.Value.Items.Count);
        foreach (Guid excludedLearnerId in new[]
                 {
                     revokedEnrollmentLearnerId,
                     revokedEntitlementLearnerId,
                     unenrolledLearnerId,
                 })
        {
            Result<NotificationPageResponse> excluded = await SendInNewScopeAsync(
                new GetNotificationsQuery(excludedLearnerId, 20, null, 0),
                cancellationToken);
            Assert.Empty(excluded.Value.Items);
            Assert.Equal(0, excluded.Value.LatestSequence);
        }

        Result<NotificationResponse> foreignRead = await SendInNewScopeAsync(
            new MarkNotificationReadCommand(completedLearnerId, firstProjection.Id),
            cancellationToken);
        Assert.False(foreignRead.IsSuccess);
        Assert.Equal("NOTIFICATION.NOT_FOUND", foreignRead.Failure.Code);
        Assert.True((await SendInNewScopeAsync(
            new MarkNotificationReadCommand(activeLearnerId, firstProjection.Id),
            cancellationToken)).IsSuccess);
        Result<NotificationsReadResponse> readAll = await SendInNewScopeAsync(
            new MarkAllNotificationsReadCommand(activeLearnerId),
            cancellationToken);
        Assert.Equal(1, readAll.Value.UpdatedCount);
        Assert.Equal(2, readAll.Value.ThroughSequence);

        Result<AnnouncementOperationResponse> deleted = await SendInNewScopeAsync(
            new DeleteAnnouncementCommand(coInstructorId, courseId, created.Value.Id),
            cancellationToken);
        Assert.True(deleted.Value.Completed);
        Result<AnnouncementResponse> hiddenAfterDelete = await SendInNewScopeAsync(
            new GetAnnouncementQuery(ownerId, courseId, created.Value.Id),
            cancellationToken);
        Assert.False(hiddenAfterDelete.IsSuccess);
        Assert.Equal("ANNOUNCEMENT.NOT_FOUND", hiddenAfterDelete.Failure.Code);

        await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        Assert.Equal(4, await dbContext.Set<AnnouncementTarget>().CountAsync(
            target => target.AnnouncementId == created.Value.Id,
            cancellationToken));
        Assert.Equal(4, await dbContext.Set<Notification>().CountAsync(
            notification => notification.AnnouncementId == created.Value.Id,
            cancellationToken));
        Guid[] targetUsers = await dbContext.Set<AnnouncementTarget>().AsNoTracking()
            .Where(target => target.AnnouncementId == created.Value.Id)
            .Select(target => target.UserId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToArrayAsync(cancellationToken);
        Assert.Equal(new[] { activeLearnerId, completedLearnerId }.Order().ToArray(), targetUsers);
        Assert.Equal(4, await dbContext.Set<Entitlement>().CountAsync(
            entitlement => entitlement.CourseId == courseId,
            cancellationToken));
        Assert.Equal(4, await dbContext.Set<Enrollment>().CountAsync(
            enrollment => enrollment.CourseId == courseId,
            cancellationToken));

        OutboxMessage[] announcementOutbox = await dbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(message => message.EventType.StartsWith("communication.announcement-"))
            .ToArrayAsync(cancellationToken);
        OutboxMessage[] outbox = announcementOutbox
            .Where(message => message.Payload.Contains(created.Value.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(3, outbox.Length);
        Assert.All(outbox, message => Assert.DoesNotContain(
            "\"body\"",
            message.Payload,
            StringComparison.OrdinalIgnoreCase));
        Assert.All(outbox, message => Assert.DoesNotContain(
            "currently entitled learners",
            message.Payload,
            StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Guid> SeedConversationCourseAsync(
        Guid ownerId,
        Guid learnerId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(ownerId, "en", now);
        CourseDraft draft = CourseDraft.Create(course.Id, "Beginner", now);
        CourseRelease release = CourseRelease.Create(
            course.Id,
            draft.Id,
            draft.Version,
            1,
            "en",
            new string('a', 64),
            ownerId,
            now);
        Entitlement entitlement = Entitlement.GrantFree(learnerId, course.Id, now);
        Enrollment enrollment = Enrollment.Create(learnerId, course.Id, release.Id, entitlement.Id, now);
        dbContext.Set<Course>().Add(course);
        dbContext.Set<CourseDraft>().Add(draft);
        dbContext.Set<CourseRelease>().Add(release);
        dbContext.Set<Entitlement>().Add(entitlement);
        dbContext.Set<Enrollment>().Add(enrollment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return course.Id;
    }

    private async Task<Guid> SeedConversationAccessCourseAsync(
        Guid ownerId,
        Guid editorId,
        Guid coInstructorId,
        Guid reviewerId,
        Guid activeLearnerId,
        Guid completedLearnerId,
        Guid revokedEnrollmentLearnerId,
        Guid revokedEntitlementLearnerId,
        Guid expiredEntitlementLearnerId,
        Guid inactiveReviewerId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(ownerId, "en", now);
        CourseDraft draft = CourseDraft.Create(course.Id, "Beginner", now);
        CourseRelease release = CourseRelease.Create(
            course.Id,
            draft.Id,
            draft.Version,
            1,
            "en",
            new string('b', 64),
            ownerId,
            now);
        dbContext.Set<Course>().Add(course);
        dbContext.Set<CourseDraft>().Add(draft);
        dbContext.Set<CourseRelease>().Add(release);
        dbContext.Set<CourseInstructor>().AddRange(
            CourseInstructor.Create(course.Id, editorId, CourseCollaboratorRole.Editor, now),
            CourseInstructor.Create(course.Id, coInstructorId, CourseCollaboratorRole.CoInstructor, now),
            CourseInstructor.Create(course.Id, reviewerId, CourseCollaboratorRole.Reviewer, now),
            CourseInstructor.Create(course.Id, inactiveReviewerId, CourseCollaboratorRole.Reviewer, now));

        Entitlement activeEntitlement = Entitlement.GrantFree(activeLearnerId, course.Id, now);
        Enrollment activeEnrollment = Enrollment.Create(
            activeLearnerId,
            course.Id,
            release.Id,
            activeEntitlement.Id,
            now);
        Entitlement completedEntitlement = Entitlement.GrantFree(completedLearnerId, course.Id, now);
        Enrollment completedEnrollment = Enrollment.Create(
            completedLearnerId,
            course.Id,
            release.Id,
            completedEntitlement.Id,
            now);
        completedEnrollment.Complete(now.AddSeconds(1));
        Entitlement revokedEnrollmentEntitlement = Entitlement.GrantFree(
            revokedEnrollmentLearnerId,
            course.Id,
            now);
        Enrollment revokedEnrollment = Enrollment.Create(
            revokedEnrollmentLearnerId,
            course.Id,
            release.Id,
            revokedEnrollmentEntitlement.Id,
            now);
        revokedEnrollment.Revoke(now.AddSeconds(1));
        Entitlement revokedEntitlement = Entitlement.GrantFree(revokedEntitlementLearnerId, course.Id, now);
        Enrollment revokedEntitlementEnrollment = Enrollment.Create(
            revokedEntitlementLearnerId,
            course.Id,
            release.Id,
            revokedEntitlement.Id,
            now);
        revokedEntitlement.Revoke(now.AddSeconds(1));
        Entitlement expiredEntitlement = Entitlement.GrantFree(
            expiredEntitlementLearnerId,
            course.Id,
            now.AddDays(-2));
        Enrollment expiredEntitlementEnrollment = Enrollment.Create(
            expiredEntitlementLearnerId,
            course.Id,
            release.Id,
            expiredEntitlement.Id,
            now.AddDays(-2));
        dbContext.Entry(expiredEntitlement)
            .Property(entitlement => entitlement.ExpiresAt)
            .CurrentValue = now.AddDays(-1);
        dbContext.Set<Entitlement>().AddRange(
            activeEntitlement,
            completedEntitlement,
            revokedEnrollmentEntitlement,
            revokedEntitlement,
            expiredEntitlement);
        dbContext.Set<Enrollment>().AddRange(
            activeEnrollment,
            completedEnrollment,
            revokedEnrollment,
            revokedEntitlementEnrollment,
            expiredEntitlementEnrollment);
        ApplicationUser inactiveReviewer = await dbContext.Users.SingleAsync(
            user => user.Id == inactiveReviewerId,
            cancellationToken);
        inactiveReviewer.IsActive = false;
        inactiveReviewer.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return course.Id;
    }

    private async Task<Guid> SeedAnnouncementCourseAsync(
        Guid ownerId,
        Guid editorId,
        Guid coInstructorId,
        Guid reviewerId,
        Guid activeLearnerId,
        Guid completedLearnerId,
        Guid revokedEnrollmentLearnerId,
        Guid revokedEntitlementLearnerId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(ownerId, "en", now);
        CourseDraft draft = CourseDraft.Create(course.Id, "Beginner", now);
        CourseRelease release = CourseRelease.Create(
            course.Id,
            draft.Id,
            draft.Version,
            1,
            "en",
            new string('a', 64),
            ownerId,
            now);
        dbContext.Set<Course>().Add(course);
        dbContext.Set<CourseDraft>().Add(draft);
        dbContext.Set<CourseRelease>().Add(release);
        dbContext.Set<CourseInstructor>().AddRange(
            CourseInstructor.Create(course.Id, editorId, CourseCollaboratorRole.Editor, now),
            CourseInstructor.Create(course.Id, coInstructorId, CourseCollaboratorRole.CoInstructor, now),
            CourseInstructor.Create(course.Id, reviewerId, CourseCollaboratorRole.Reviewer, now));

        foreach ((Guid learnerId, string state) in new[]
                 {
                     (activeLearnerId, "Active"),
                     (completedLearnerId, "Completed"),
                     (revokedEnrollmentLearnerId, "RevokedEnrollment"),
                     (revokedEntitlementLearnerId, "RevokedEntitlement"),
                 })
        {
            Entitlement entitlement = Entitlement.GrantFree(learnerId, course.Id, now);
            Enrollment enrollment = Enrollment.Create(learnerId, course.Id, release.Id, entitlement.Id, now);
            if (state == "Completed")
            {
                enrollment.Complete(now.AddSeconds(1));
            }
            else if (state == "RevokedEnrollment")
            {
                enrollment.Revoke(now.AddSeconds(1));
            }
            else if (state == "RevokedEntitlement")
            {
                entitlement.Revoke(now.AddSeconds(1));
            }

            dbContext.Set<Entitlement>().Add(entitlement);
            dbContext.Set<Enrollment>().Add(enrollment);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return course.Id;
    }

    private async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        ApplicationUser user = await dbContext.Users.SingleAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CreateConversationCommand CreateConversation(
        Guid creatorId,
        Guid participantId,
        Guid courseId) => new(
        creatorId,
        [participantId],
        courseId,
        Guid.CreateVersion7().ToString("N"));

    private static async Task<long> CountAdvisoryWaitersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND wait_event = 'advisory'",
            connection);
        object? count = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<long>(count);
    }

    private static async Task WaitForAdvisoryWaitersAsync(
        NpgsqlConnection connection,
        long expectedCount,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (await CountAdvisoryWaitersAsync(connection, cancellationToken) >= expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        Assert.Fail($"Expected at least {expectedCount} transactions to wait on an advisory lock.");
    }

    private async Task<Result<TResponse>> SendInNewScopeAsync<TResponse>(
        IRequest<Result<TResponse>> request,
        CancellationToken cancellationToken)
        where TResponse : notnull
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
    }

    private async Task<Guid> CreateUserAsync(
        string prefix,
        string role = DorosakIdentityConstants.StudentRole)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }
}
