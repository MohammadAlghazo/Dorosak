using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dorosak.Application.IntegrationTests.Identity;

[Collection(InfrastructureTestGroup.Name)]
public sealed class IdentityEmailDispatcherTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task SmtpFailure_ReleasesOutboxMessageForRetry()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid marker = Guid.CreateVersion7();
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<RegistrationAcceptedResponse> result = await sender.Send(
                new RegisterAccountCommand(
                    "Synthetic Email Student",
                    $"email-{marker:N}@example.test",
                    "correct horse battery staple",
                    new IdentityRequestContext(marker.ToString("D"), "Integration test", "en")),
                cancellationToken);
            Assert.True(result.IsSuccess);
        }

        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            IIdentityEmailDispatcher dispatcher = scope.ServiceProvider
                .GetRequiredService<IIdentityEmailDispatcher>();
            Assert.Equal(0, await dispatcher.DispatchPendingAsync(cancellationToken));
        }

        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT attempt_count || '|' || last_error_code || '|' || (lock_token IS NULL) || '|' || (available_at > occurred_at)
            FROM operations.outbox_messages
            WHERE event_type = 'identity.email-verification-requested'
              AND payload ->> 'userId' = (
                  SELECT id::text FROM identity.users WHERE email = @email)
            ORDER BY occurred_at DESC
            LIMIT 1
            """,
            connection);
        command.Parameters.AddWithValue("email", $"email-{marker:N}@example.test");
        object? resultValue = await command.ExecuteScalarAsync(cancellationToken);
        string state = Assert.IsType<string>(resultValue);
        Assert.StartsWith("1|SmtpException|true|true", state, StringComparison.Ordinal);
    }
}
