using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dorosak.Api.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    private const string PostgresImage =
        "postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("dorosak_api_tests")
        .WithUsername("dorosak_api_tests")
        .WithPassword("dorosak_api_tests")
        .Build();

    public DorosakApiFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = CreateFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public DorosakApiFactory CreateFactory() => new(_postgres.GetConnectionString());

    public async Task<string> CreateEmptyDatabaseAsync(CancellationToken cancellationToken)
    {
        string databaseName = $"dorosak_empty_{Guid.CreateVersion7():N}";
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        using var commandBuilder = new NpgsqlCommandBuilder();
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE {commandBuilder.QuoteIdentifier(databaseName)}",
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = databaseName,
        };
        return connectionString.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
