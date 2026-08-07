using Dorosak.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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
    private const string RedisImage =
        "redis:8.10.0@sha256:39353c6a2f310da333374e1290c91805d15a85def073b5090f58e4ac646d284c";
    private const string RedisPassword = "dorosak-api-tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("dorosak_api_tests")
        .WithUsername("dorosak_api_tests")
        .WithPassword("dorosak_api_tests")
        .Build();

    private readonly IContainer _redis = new ContainerBuilder(RedisImage)
        .WithCommand("redis-server", "--save", "", "--appendonly", "no", "--requirepass", RedisPassword)
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
            "redis-cli",
            "-a",
            RedisPassword,
            "ping"))
        .Build();

    private string _redisConnection = string.Empty;

    public DorosakApiFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public string DatabaseConnection => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        _redisConnection =
            $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},password={RedisPassword},abortConnect=false";
        Factory = CreateFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://app.dorosak.test"),
            HandleCookies = false,
        });

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public DorosakApiFactory CreateFactory() => new(_postgres.GetConnectionString(), _redisConnection);

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
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
