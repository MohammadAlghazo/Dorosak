using System.Collections.Concurrent;
using System.Text.Json;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Communications;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Communications;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Application.IntegrationTests.Phase9;

[Collection(CommunicationsRealtimeInfrastructureTestGroup.Name)]
public sealed class OutboxHardeningTests(InfrastructureFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task KnownSchemaAndPayloadProblemsBecomeTerminalButUnknownTypesRemainPending()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid schemaId;
        Guid payloadId;
        Guid unsupportedId;
        IServiceCollection services = fixture.CreateServices();
        services.AddSingleton<ICommunicationsRealtimePublisher, NoOpPublisher>();
        services.AddCommunicationsRealtimeDispatching();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            OutboxMessage schema = CreateOutbox(
                CommunicationsRealtimeEvents.MessageCreated,
                2,
                "{}",
                now);
            OutboxMessage payload = CreateOutbox(
                CommunicationsRealtimeEvents.MessageCreated,
                1,
                "{}",
                now.AddTicks(1));
            OutboxMessage unsupported = CreateOutbox(
                "identity.email-verification-requested",
                1,
                "{}",
                now.AddTicks(2));
            schemaId = schema.Id;
            payloadId = payload.Id;
            unsupportedId = unsupported.Id;
            dbContext.Set<OutboxMessage>().AddRange(
                schema,
                payload,
                unsupported);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            int processed = await scope.ServiceProvider
                .GetRequiredService<ICommunicationsRealtimeDispatcher>()
                .DispatchPendingAsync(cancellationToken);
            Assert.Equal(2, processed);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage schema = await dbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                message => message.Id == schemaId,
                cancellationToken);
            OutboxMessage payload = await dbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                message => message.Id == payloadId,
                cancellationToken);
            OutboxMessage unsupported = await dbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                message => message.Id == unsupportedId,
                cancellationToken);
            Assert.NotNull(schema.ProcessedAt);
            Assert.Equal("REALTIME.DEAD_LETTER.SCHEMA_INVALID", schema.LastErrorCode);
            Assert.NotNull(payload.ProcessedAt);
            Assert.Equal("REALTIME.DEAD_LETTER.PAYLOAD_INVALID", payload.LastErrorCode);
            Assert.Null(unsupported.ProcessedAt);
            Assert.Equal(0, unsupported.AttemptCount);
        }
    }

    [Fact]
    public async Task EighthTransientAttemptBecomesTerminalWithoutLoggingBody()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var loggerProvider = new CapturingLoggerProvider();
        IServiceCollection services = fixture.CreateServices();
        services.AddLogging(builder => builder.ClearProviders().AddProvider(loggerProvider));
        services.AddSingleton<ICommunicationsRealtimePublisher, ThrowingPublisher>();
        services.AddCommunicationsRealtimeDispatching();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Guid outboxId;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext database = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = ApplicationUser.Create(
                $"outbox-max-{Guid.CreateVersion7():N}",
                $"outbox-max-{Guid.CreateVersion7():N}@example.test",
                DateTimeOffset.UtcNow);
            user.EmailConfirmed = true;
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Course course = Course.Create(user.Id, "en", now);
            Conversation conversation = Conversation.Create(user.Id, course.Id, now);
            ConversationParticipant participant = ConversationParticipant.Join(
                conversation.Id,
                user.Id,
                now);
            var payload = new ConversationCreatedRealtimePayload(
                conversation.Id,
                user.Id,
                course.Id);
            OutboxMessage outbox = OutboxMessage.Create(
                CommunicationsRealtimeEvents.ConversationCreated,
                CommunicationsRealtimeEvents.SchemaVersion,
                JsonSerializer.Serialize(payload, JsonOptions),
                "{}",
                now);
            outboxId = outbox.Id;
            database.Set<Course>().Add(course);
            database.Set<Conversation>().Add(conversation);
            database.Set<ConversationParticipant>().Add(participant);
            database.Set<OutboxMessage>().Add(outbox);
            await database.SaveChangesAsync(cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE operations.outbox_messages SET attempt_count = 7 WHERE id = {outboxId}",
                cancellationToken);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            int processed = await scope.ServiceProvider
                .GetRequiredService<ICommunicationsRealtimeDispatcher>()
                .DispatchPendingAsync(cancellationToken);
            Assert.Equal(1, processed);
        }

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext database = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage message = await database.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                candidate => candidate.Id == outboxId,
                cancellationToken);
            Assert.Equal(8, message.AttemptCount);
            Assert.NotNull(message.ProcessedAt);
            Assert.Equal("REALTIME.DEAD_LETTER.MAX_RETRIES", message.LastErrorCode);
            Assert.Null(message.LockToken);
        }

        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains("private body that must not be logged", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StaleOwnersCannotCompleteOrReleaseANewerLease()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IServiceCollection services = fixture.CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Guid messageId;
        Guid staleToken = Guid.CreateVersion7();
        Guid newerToken = Guid.CreateVersion7();
        var leaseLogs = new ConcurrentQueue<string>();
        var leaseLogger = new CapturingLogger(leaseLogs);

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage message = CreateOutbox(
                "identity.email-verification-requested",
                1,
                "{}",
                DateTimeOffset.UtcNow);
            messageId = message.Id;
            dbContext.Set<OutboxMessage>().Add(message);
            await dbContext.SaveChangesAsync(cancellationToken);
            DateTimeOffset leaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE operations.outbox_messages SET lock_token = {staleToken}, locked_until = {leaseUntil} WHERE id = {messageId}",
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE operations.outbox_messages SET lock_token = {newerToken}, locked_until = {leaseUntil} WHERE id = {messageId}",
                cancellationToken);
        }

        Task<bool> staleComplete;
        Task<bool> staleRelease;
        await using (AsyncServiceScope completeScope = provider.CreateAsyncScope())
        await using (AsyncServiceScope releaseScope = provider.CreateAsyncScope())
        {
            staleComplete = OutboxLease.CompleteAsync(
                completeScope.ServiceProvider.GetRequiredService<DorosakDbContext>(),
                messageId,
                staleToken,
                DateTimeOffset.UtcNow,
                leaseLogger,
                cancellationToken);
            staleRelease = OutboxLease.ReleaseAsync(
                releaseScope.ServiceProvider.GetRequiredService<DorosakDbContext>(),
                messageId,
                staleToken,
                DateTimeOffset.UtcNow.AddMinutes(1),
                "stale",
                leaseLogger,
                cancellationToken);
            await Task.WhenAll(staleComplete, staleRelease);
        }

        Assert.False(await staleComplete);
        Assert.False(await staleRelease);
        Assert.Equal(2, leaseLogs.Count(message =>
            message.Contains("lease was lost", StringComparison.Ordinal)));
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            DorosakDbContext database = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            OutboxMessage message = await database.Set<OutboxMessage>().AsNoTracking().SingleAsync(
                candidate => candidate.Id == messageId,
                cancellationToken);
            Assert.Null(message.ProcessedAt);
            Assert.Equal(newerToken, message.LockToken);

            Assert.True(await OutboxLease.TerminateAsync(
                database,
                messageId,
                newerToken,
                DateTimeOffset.UtcNow,
                "REALTIME.DEAD_LETTER.TEST_CLEANUP",
                NullLogger.Instance,
                cancellationToken));
        }
    }

    [Fact]
    public void RetryDelayJitterDoesNotExceedTwentyPercent()
    {
        for (int index = 0; index < 100; index++)
        {
            double seconds = OutboxLease.GetRetryDelay(3).TotalSeconds;
            Assert.InRange(seconds, 8, 9.6);
        }
    }

    private static OutboxMessage CreateOutbox(
        string eventType,
        int schemaVersion,
        string payload,
        DateTimeOffset occurredAt) =>
        OutboxMessage.Create(
            eventType,
            schemaVersion,
            payload,
            "{}",
            occurredAt);

    private sealed class NoOpPublisher : ICommunicationsRealtimePublisher
    {
        public Task PublishAsync<TPayload>(
            IReadOnlyCollection<Guid> userIds,
            CommunicationsRealtimeEnvelope<TPayload> envelope,
            CancellationToken cancellationToken)
            where TPayload : class => Task.CompletedTask;
    }

    private sealed class ThrowingPublisher : ICommunicationsRealtimePublisher
    {
        public Task PublishAsync<TPayload>(
            IReadOnlyCollection<Guid> userIds,
            CommunicationsRealtimeEnvelope<TPayload> envelope,
            CancellationToken cancellationToken) where TPayload : class =>
            throw new HttpRequestException("private body that must not be logged");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                messages.Enqueue(exception.ToString());
            }
        }
    }
}
