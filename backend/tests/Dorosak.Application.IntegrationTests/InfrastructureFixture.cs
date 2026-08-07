using Dorosak.Application;
using Dorosak.Infrastructure;
using Dorosak.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Dorosak.Application.IntegrationTests;

public sealed class InfrastructureFixture : IAsyncLifetime
{
    private const string PostgresImage =
        "postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15";
    private const string RedisImage =
        "redis:8.10.0@sha256:39353c6a2f310da333374e1290c91805d15a85def073b5090f58e4ac646d284c";
    private const string RedisPassword = "dorosak-integration-tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("dorosak_tests")
        .WithUsername("dorosak_tests")
        .WithPassword("dorosak_tests")
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

    public ServiceProvider Services { get; private set; } = null!;

    public string DatabaseConnection => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        string redisConnection =
            $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},password={RedisPassword},abortConnect=false";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = DatabaseConnection,
                ["ConnectionStrings:Redis"] = redisConnection,
                ["Email:SmtpHost"] = "127.0.0.1",
                ["Email:SmtpPort"] = "1",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new IntegrationHostEnvironment());
        services.AddApplication(null, null);
        services.AddInfrastructure(configuration);
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is not null)
        {
            await Services.DisposeAsync();
        }

        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class IntegrationHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Dorosak.Application.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
