using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dorosak.Application.IntegrationTests.Persistence;

public sealed class DatabaseBootstrapTests
{
    private const string PostgresImage =
        "postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15";

    [Fact]
    public async Task Bootstrap_EnforcesRoleBoundariesAndRejectsMembershipDrift()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage(PostgresImage)
            .WithDatabase("dorosak_bootstrap_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);

        string bootstrapSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "bootstrap-development-database.sql"),
            TestContext.Current.CancellationToken);
        await using var adminConnection = new NpgsqlConnection(postgres.GetConnectionString());
        await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(
            adminConnection,
            "CREATE ROLE dorosak_owner LOGIN CREATEROLE PASSWORD 'owner-test'; ALTER DATABASE dorosak_bootstrap_tests OWNER TO dorosak_owner;",
            TestContext.Current.CancellationToken);

        var ownerConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "dorosak_owner",
            Password = "owner-test",
        };
        await using var ownerConnection = new NpgsqlConnection(ownerConnectionString.ConnectionString);
        await ownerConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(ownerConnection, bootstrapSql, TestContext.Current.CancellationToken);
        await ExecuteAsync(
            adminConnection,
            "GRANT CREATE ON SCHEMA operations TO dorosak_runtime",
            TestContext.Current.CancellationToken);
        await ExecuteAsync(ownerConnection, bootstrapSql, TestContext.Current.CancellationToken);
        await ExecuteAsync(
            ownerConnection,
            "ALTER ROLE dorosak_app PASSWORD 'runtime-test'; ALTER ROLE dorosak_migrator PASSWORD 'migrator-test';",
            TestContext.Current.CancellationToken);

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "dorosak_app",
            Password = "runtime-test",
        };
        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString.ConnectionString);
        await runtimeConnection.OpenAsync(TestContext.Current.CancellationToken);
        string runtimePrivileges = await ExecuteScalarAsync(
            runtimeConnection,
            "SELECT has_schema_privilege(current_user, 'operations', 'USAGE') || '|' || has_schema_privilege(current_user, 'operations', 'CREATE') || '|' || has_schema_privilege(current_user, 'catalog', 'USAGE') || '|' || has_schema_privilege(current_user, 'catalog', 'CREATE') || '|' || has_schema_privilege(current_user, 'authoring', 'USAGE') || '|' || has_schema_privilege(current_user, 'authoring', 'CREATE') || '|' || has_schema_privilege(current_user, 'public', 'USAGE') || '|' || has_database_privilege(current_user, current_database(), 'TEMPORARY') || '|' || pg_has_role(current_user, 'dorosak_schema_owner', 'SET')",
            TestContext.Current.CancellationToken);
        Assert.Equal("true|false|true|false|true|false|false|false|false", runtimePrivileges);

        PostgresException elevationFailure = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            runtimeConnection,
            "SET ROLE dorosak_schema_owner",
            TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, elevationFailure.SqlState);

        var migratorConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "dorosak_migrator",
            Password = "migrator-test",
            Options = "-c role=dorosak_schema_owner",
        };
        await using var migratorConnection = new NpgsqlConnection(migratorConnectionString.ConnectionString);
        await migratorConnection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<DorosakDbContext>()
            .UseNpgsql(migratorConnectionString.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", DorosakDbContext.MigrationsSchema))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var dbContext = new DorosakDbContext(options))
        {
            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal(
            "true|true|false|false|false",
            await ExecuteScalarAsync(
                runtimeConnection,
                "SELECT has_table_privilege(current_user, 'operations.audit_logs', 'SELECT') || '|' || has_table_privilege(current_user, 'operations.audit_logs', 'INSERT') || '|' || has_table_privilege(current_user, 'operations.audit_logs', 'UPDATE') || '|' || has_table_privilege(current_user, 'operations.audit_logs', 'DELETE') || '|' || has_table_privilege(current_user, 'operations.audit_logs', 'TRUNCATE')",
                TestContext.Current.CancellationToken));
        await ExecuteAsync(
            migratorConnection,
            "CREATE TABLE operations.bootstrap_probe (id integer PRIMARY KEY); DROP TABLE operations.bootstrap_probe;",
            TestContext.Current.CancellationToken);

        await ExecuteAsync(
            adminConnection,
            "CREATE ROLE unexpected_login LOGIN; GRANT dorosak_app TO unexpected_login WITH ADMIN FALSE, INHERIT TRUE, SET TRUE",
            TestContext.Current.CancellationToken);
        PostgresException driftFailure = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            ownerConnection,
            bootstrapSql,
            TestContext.Current.CancellationToken));
        Assert.Contains("unexpected role membership", driftFailure.MessageText, StringComparison.Ordinal);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ExecuteScalarAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<string>(result);
    }
}

