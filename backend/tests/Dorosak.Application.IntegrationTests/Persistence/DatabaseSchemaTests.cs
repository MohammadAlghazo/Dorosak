using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dorosak.Application.IntegrationTests.Persistence;

[Collection(InfrastructureTestGroup.Name)]
public sealed class DatabaseSchemaTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Migration_CreatesExpectedOperationalSchema()
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        IEnumerable<string> pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(
            TestContext.Current.CancellationToken);
        long expectedMigrationCount = dbContext.Database.GetMigrations().LongCount();

        Assert.Empty(pendingMigrations);

        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedMigrationCount, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM migrations.__ef_migrations_history",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'operations' AND table_name IN ('outbox_messages', 'idempotency_records')",
            TestContext.Current.CancellationToken));
        Assert.Equal("jsonb", await ExecuteScalarAsync<string>(
            connection,
            "SELECT data_type FROM information_schema.columns WHERE table_schema = 'operations' AND table_name = 'outbox_messages' AND column_name = 'payload'",
            TestContext.Current.CancellationToken));
        string compatibilityRange = await ExecuteScalarAsync<string>(
            connection,
            "SELECT minimum_compatible_migration_id || '|' || maximum_compatible_migration_id FROM operations.schema_compatibility WHERE singleton",
            TestContext.Current.CancellationToken);
        string[] boundaries = compatibilityRange.Split('|', 2);
        Assert.Equal("20260806063112_AddSchemaCompatibilityMarker", boundaries[0]);
        Assert.Equal(dbContext.Database.GetMigrations().Last(), boundaries[1]);

        string pendingIndex = await ExecuteScalarAsync<string>(
            connection,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'operations' AND indexname = 'ix_outbox_messages_pending'",
            TestContext.Current.CancellationToken);
        Assert.Contains("processed_at IS NULL", pendingIndex, StringComparison.Ordinal);
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<T>(value);
    }
}
