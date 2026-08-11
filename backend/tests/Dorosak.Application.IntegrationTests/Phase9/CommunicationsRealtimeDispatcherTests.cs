using System.Text.Json;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Communications;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Communications;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Application.IntegrationTests.Phase9;

[Collection(CommunicationsRealtimeInfrastructureTestGroup.Name)]
public sealed class CommunicationsRealtimeDispatcherTests(InfrastructureFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishFailureReleasesForRetryAndTargetsOnlyCurrentAuthorizedParticipants()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var publisher = new RecordingPublisher();
        IServiceCollection services = fixture.CreateServices();
        services.AddSingleton<ICommunicationsRealtimePublisher>(publisher);
        services.AddCommunicationsRealtimeDispatching();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Guid senderId;
        Guid participantId;
        Guid outsiderId;
        Guid outboxId;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser sender = await CreateUserAsync(userManager, "realtime-sender");
            ApplicationUser participant = await CreateUserAsync(userManager, "realtime-participant");
            ApplicationUser outsider = await CreateUserAsync(userManager, "realtime-outsider");
            senderId = sender.Id;
            participantId = participant.Id;
            outsiderId = outsider.Id;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Course course = Course.Create(sender.Id, "en", now);
            CourseInstructor instructor = CourseInstructor.Create(
                course.Id,
                participant.Id,
                CourseCollaboratorRole.CoInstructor,
                now);
            Conversation conversation = Conversation.Create(sender.Id, course.Id, now);
            ConversationParticipant senderParticipant = ConversationParticipant.Join(
                conversation.Id,
                sender.Id,
                now);
            ConversationParticipant recipientParticipant = ConversationParticipant.Join(
                conversation.Id,
                participant.Id,
                now);
            Message message = Message.Create(
                conversation.Id,
                sender.Id,
                Guid.CreateVersion7(),
                "Synthetic content that must never enter realtime payloads.",
                conversation.NextMessageSequence(),
                now);
            conversation.RecordMessage(now);
            string payload = JsonSerializer.Serialize(
                new
                {
                    messageId = message.Id,
                    conversationId = conversation.Id,
                    senderUserId = sender.Id,
                    sequence = message.Sequence,
                    occurredAt = now,
                },
                JsonOptions);
            OutboxMessage outbox = OutboxMessage.Create(
                CommunicationsRealtimeEvents.MessageCreated,
                CommunicationsRealtimeEvents.SchemaVersion,
                payload,
                "{}",
                DateTimeOffset.UnixEpoch);
            outboxId = outbox.Id;
            publisher.FailEventId = outboxId;

            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            dbContext.Set<Course>().Add(course);
            dbContext.Set<CourseInstructor>().Add(instructor);
            dbContext.Set<Conversation>().Add(conversation);
            dbContext.Set<ConversationParticipant>().AddRange(senderParticipant, recipientParticipant);
            dbContext.Set<Message>().Add(message);
            dbContext.Set<OutboxMessage>().Add(outbox);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ICommunicationsRealtimeDispatcher dispatcher = scope.ServiceProvider
                .GetRequiredService<ICommunicationsRealtimeDispatcher>();
            await dispatcher.DispatchPendingAsync(cancellationToken);
        }

        PublishedEvent firstAttempt = Assert.Single(
            publisher.Events,
            published => published.EventId == outboxId);
        Assert.Equal(new[] { senderId, participantId }.Order().ToArray(), firstAttempt.UserIds);
        Assert.DoesNotContain(outsiderId, firstAttempt.UserIds);
        Assert.DoesNotContain("\"body\"", firstAttempt.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"title\"", firstAttempt.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic content", firstAttempt.Json, StringComparison.Ordinal);
        using (JsonDocument document = JsonDocument.Parse(firstAttempt.Json))
        {
            JsonElement root = document.RootElement;
            Assert.Equal(outboxId, root.GetProperty("eventId").GetGuid());
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(1, root.GetProperty("payload").GetProperty("sequence").GetInt64());
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage failed = await dbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                message => message.Id == outboxId,
                cancellationToken);
            Assert.Equal(1, failed.AttemptCount);
            Assert.Null(failed.ProcessedAt);
            Assert.Null(failed.LockToken);
            Assert.Equal(nameof(HttpRequestException), failed.LastErrorCode);
            Assert.True(failed.AvailableAt > failed.OccurredAt);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE operations.outbox_messages SET available_at = {DateTimeOffset.UnixEpoch} WHERE id = {outboxId}",
                cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ICommunicationsRealtimeDispatcher dispatcher = scope.ServiceProvider
                .GetRequiredService<ICommunicationsRealtimeDispatcher>();
            await dispatcher.DispatchPendingAsync(cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage completed = await dbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                message => message.Id == outboxId,
                cancellationToken);
            Assert.Equal(2, completed.AttemptCount);
            Assert.NotNull(completed.ProcessedAt);
            Assert.Null(completed.LockToken);
            Assert.Null(completed.LastErrorCode);
        }
    }

    [Fact]
    public async Task AnnouncementPublishUsesDurableTargetsAndCurrentManagersWithoutContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var publisher = new RecordingPublisher();
        IServiceCollection services = fixture.CreateServices();
        services.AddSingleton<ICommunicationsRealtimePublisher>(publisher);
        services.AddCommunicationsRealtimeDispatching();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Guid managerId;
        Guid targetId;
        Guid outsiderId;
        Guid outboxId;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser manager = await CreateUserAsync(
                userManager,
                "realtime-announcement-manager",
                DorosakIdentityConstants.TeacherRole);
            ApplicationUser target = await CreateUserAsync(userManager, "realtime-announcement-target");
            ApplicationUser outsider = await CreateUserAsync(userManager, "realtime-announcement-outsider");
            managerId = manager.Id;
            targetId = target.Id;
            outsiderId = outsider.Id;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Course course = Course.Create(manager.Id, "en", now);
            Announcement announcement = Announcement.Create(
                course.Id,
                manager.Id,
                "Private synthetic announcement title",
                "Private synthetic announcement body",
                now);
            NotificationSequence sequence = NotificationSequence.Create(target.Id);
            Notification notification = Notification.CreateForAnnouncement(
                target.Id,
                announcement.Id,
                announcement.Version,
                sequence.Advance(),
                announcement.Title,
                announcement.Body,
                now);
            AnnouncementTarget announcementTarget = AnnouncementTarget.Create(
                announcement.Id,
                target.Id,
                announcement.Version,
                notification.Id,
                now);
            string payload = JsonSerializer.Serialize(
                new
                {
                    announcementId = announcement.Id,
                    courseId = course.Id,
                    createdByUserId = manager.Id,
                    version = announcement.Version,
                    targetCount = 1,
                    occurredAt = now,
                },
                JsonOptions);
            OutboxMessage outbox = OutboxMessage.Create(
                CommunicationsRealtimeEvents.AnnouncementCreated,
                CommunicationsRealtimeEvents.SchemaVersion,
                payload,
                "{}",
                DateTimeOffset.UnixEpoch.AddTicks(1));
            outboxId = outbox.Id;

            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            dbContext.Set<Course>().Add(course);
            dbContext.Set<Announcement>().Add(announcement);
            dbContext.Set<NotificationSequence>().Add(sequence);
            dbContext.Set<Notification>().Add(notification);
            dbContext.Set<AnnouncementTarget>().Add(announcementTarget);
            dbContext.Set<OutboxMessage>().Add(outbox);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ICommunicationsRealtimeDispatcher dispatcher = scope.ServiceProvider
                .GetRequiredService<ICommunicationsRealtimeDispatcher>();
            await dispatcher.DispatchPendingAsync(cancellationToken);
        }

        PublishedEvent published = Assert.Single(
            publisher.Events,
            item => item.EventId == outboxId);
        Assert.Equal(new[] { managerId, targetId }.Order().ToArray(), published.UserIds);
        Assert.DoesNotContain(outsiderId, published.UserIds);
        Assert.DoesNotContain("\"body\"", published.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"title\"", published.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private synthetic announcement", published.Json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(published.Json);
        JsonElement realtimePayload = document.RootElement.GetProperty("payload");
        Assert.Equal(1L, realtimePayload.GetProperty("targetCount").GetInt64());
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string prefix,
        string role = DorosakIdentityConstants.StudentRole)
    {
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private sealed class RecordingPublisher : ICommunicationsRealtimePublisher
    {
        private bool _failed;

        public Guid FailEventId { get; set; }

        public List<PublishedEvent> Events { get; } = [];

        public Task PublishAsync<TPayload>(
            IReadOnlyCollection<Guid> userIds,
            CommunicationsRealtimeEnvelope<TPayload> envelope,
            CancellationToken cancellationToken)
            where TPayload : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(new PublishedEvent(
                envelope.EventId,
                userIds.Order().ToArray(),
                JsonSerializer.Serialize(envelope, JsonOptions)));
            if (envelope.EventId == FailEventId && !_failed)
            {
                _failed = true;
                throw new HttpRequestException("Synthetic realtime transport failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record PublishedEvent(Guid EventId, Guid[] UserIds, string Json);
}

[CollectionDefinition(Name)]
public sealed class CommunicationsRealtimeInfrastructureTestGroup : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "Communications realtime infrastructure";
}
