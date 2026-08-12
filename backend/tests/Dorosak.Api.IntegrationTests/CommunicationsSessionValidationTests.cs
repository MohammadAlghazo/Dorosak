using System.Collections.Concurrent;
using Dorosak.Api.Realtime;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Domain.Identity;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CommunicationsSessionValidationTests(ApiFixture fixture)
{
    [Fact]
    public void RegistryStoresOnlyConnectionSessionMetadataAndRemovesAtomically()
    {
        var registry = new CommunicationsConnectionRegistry();
        int abortCount = 0;
        CommunicationsConnectionRegistration registration = registry.Register(
            "connection-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            3,
            () => Interlocked.Increment(ref abortCount));

        CommunicationsConnectionRegistration stored = Assert.Single(registry.Snapshot());
        Assert.Equal(registration.ConnectionId, stored.ConnectionId);
        Assert.Equal(registration.UserId, stored.UserId);
        Assert.Equal(registration.SessionId, stored.SessionId);
        Assert.Equal(3, stored.AuthorizationVersion);
        Assert.DoesNotContain(
            typeof(CommunicationsConnectionRegistration).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        Assert.True(registry.Remove(registration));
        Assert.Empty(registry.Snapshot());
        Assert.Equal(0, abortCount);
    }

    [Fact]
    public async Task WorkerKeepsOnlyActiveMatchingSessions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AuthenticatedSessionResponse valid = await CreateSessionAsync("session-worker-valid", cancellationToken);
        AuthenticatedSessionResponse expired = await CreateSessionAsync("session-worker-expired", cancellationToken);
        AuthenticatedSessionResponse inactive = await CreateSessionAsync("session-worker-inactive", cancellationToken);
        AuthenticatedSessionResponse authorizationChanged = await CreateSessionAsync(
            "session-worker-authz-changed",
            cancellationToken);

        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            DateTimeOffset expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE identity.refresh_sessions
                SET idle_expires_at = {expiredAt.AddSeconds(-1)},
                    absolute_expires_at = {expiredAt}
                WHERE id = {expired.Identity.SessionId}
                """,
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE identity.users SET is_active = false WHERE id = {inactive.Identity.UserId}",
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE identity.users
                SET authorization_version = authorization_version + 1
                WHERE id = {authorizationChanged.Identity.UserId}
                """,
                cancellationToken);
        }

        CommunicationsConnectionRegistry registry = fixture.Factory.Services
            .GetRequiredService<CommunicationsConnectionRegistry>();
        var aborts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        CommunicationsConnectionRegistration validRegistration = Register(
            registry,
            "valid",
            valid,
            valid.Identity.AuthorizationVersion,
            aborts);
        CommunicationsConnectionRegistration wrongVersion = Register(
            registry,
            "wrong-version",
            valid,
            valid.Identity.AuthorizationVersion + 1,
            aborts);
        CommunicationsConnectionRegistration expiredRegistration = Register(
            registry,
            "expired",
            expired,
            expired.Identity.AuthorizationVersion,
            aborts);
        CommunicationsConnectionRegistration inactiveRegistration = Register(
            registry,
            "inactive",
            inactive,
            inactive.Identity.AuthorizationVersion,
            aborts);
        CommunicationsConnectionRegistration changedAuthorizationRegistration = Register(
            registry,
            "authz-changed",
            authorizationChanged,
            authorizationChanged.Identity.AuthorizationVersion,
            aborts);
        CommunicationsConnectionRegistration missingRegistration = registry.Register(
            "missing",
            valid.Identity.UserId,
            Guid.CreateVersion7(),
            valid.Identity.AuthorizationVersion,
            () => aborts.AddOrUpdate("missing", 1, (_, count) => count + 1));

        try
        {
            CommunicationsSessionValidationWorker worker = fixture.Factory.Services
                .GetRequiredService<CommunicationsSessionValidationWorker>();
            await worker.ValidateOnceAsync(cancellationToken);

            CommunicationsConnectionRegistration[] remaining = registry.Snapshot();
            Assert.Contains(validRegistration, remaining);
            Assert.DoesNotContain(wrongVersion, remaining);
            Assert.DoesNotContain(expiredRegistration, remaining);
            Assert.DoesNotContain(inactiveRegistration, remaining);
            Assert.DoesNotContain(changedAuthorizationRegistration, remaining);
            Assert.DoesNotContain(missingRegistration, remaining);
            Assert.Equal(1, aborts["wrong-version"]);
            Assert.Equal(1, aborts["expired"]);
            Assert.Equal(1, aborts["inactive"]);
            Assert.Equal(1, aborts["authz-changed"]);
            Assert.Equal(1, aborts["missing"]);
        }
        finally
        {
            registry.Remove(validRegistration);
        }
    }

    [Fact]
    public async Task RevokingSessionAbortsARealLongPollingConnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AuthenticatedSessionResponse session = await CreateSessionAsync(
            "session-worker-transport",
            cancellationToken);
        await using HubConnection connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Client.BaseAddress!, CommunicationsHub.Path),
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                    options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
        var closed = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += exception =>
        {
            closed.TrySetResult(exception);
            return Task.CompletedTask;
        };

        await connection.StartAsync(cancellationToken);
        CommunicationsConnectionRegistry registry = fixture.Factory.Services
            .GetRequiredService<CommunicationsConnectionRegistry>();
        Assert.Contains(
            registry.Snapshot(),
            registration => registration.ConnectionId == connection.ConnectionId &&
                registration.SessionId == session.Identity.SessionId);

        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            RefreshSession refreshSession = await dbContext.Set<RefreshSession>().SingleAsync(
                candidate => candidate.Id == session.Identity.SessionId,
                cancellationToken);
            refreshSession.Revoke(DateTimeOffset.UtcNow, "integration-test-revocation");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await fixture.Factory.Services
            .GetRequiredService<CommunicationsSessionValidationWorker>()
            .ValidateOnceAsync(cancellationToken);

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
        Assert.DoesNotContain(
            registry.Snapshot(),
            registration => registration.ConnectionId == connection.ConnectionId);
    }

    private async Task<AuthenticatedSessionResponse> CreateSessionAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        string marker = Guid.CreateVersion7().ToString("N");
        ApplicationUser user = ApplicationUser.Create(
            $"{prefix}-{marker}",
            $"{prefix}-{marker}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);
        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.83", "realtime session test", "en")),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        return signIn.Value.Session!;
    }

    private static CommunicationsConnectionRegistration Register(
        CommunicationsConnectionRegistry registry,
        string name,
        AuthenticatedSessionResponse session,
        int authorizationVersion,
        ConcurrentDictionary<string, int> aborts) =>
        registry.Register(
            name,
            session.Identity.UserId,
            session.Identity.SessionId,
            authorizationVersion,
            () => aborts.AddOrUpdate(name, 1, (_, count) => count + 1));
}
