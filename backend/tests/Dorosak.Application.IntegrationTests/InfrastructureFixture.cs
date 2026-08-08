using Dorosak.Application;
using Dorosak.Application.Features.Media;
using Dorosak.Infrastructure;
using Dorosak.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

    private string RedisConnection { get; set; } = string.Empty;

    public IServiceCollection CreateServices()
    {
        IConfiguration configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new IntegrationHostEnvironment());
        services.AddApplication(null, null);
        services.AddInfrastructure(configuration);
        services.AddSingleton<IObjectStorage, TestObjectStorage>();
        return services;
    }

    public string DatabaseConnection => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        string redisConnection =
            $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},password={RedisPassword},abortConnect=false";
        RedisConnection = redisConnection;
        IConfiguration configuration = CreateConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new IntegrationHostEnvironment());
        services.AddApplication(null, null);
        services.AddInfrastructure(configuration);
        services.AddSingleton<IObjectStorage, TestObjectStorage>();
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "DO $role$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN CREATE ROLE dorosak_runtime NOLOGIN; END IF; END $role$;",
            TestContext.Current.CancellationToken);
        await dbContext.Database.MigrateAsync();
    }

    private IConfiguration CreateConfiguration() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = DatabaseConnection,
                ["ConnectionStrings:Redis"] = RedisConnection,
                ["Email:SmtpHost"] = "127.0.0.1",
                ["Email:SmtpPort"] = "1",
                ["AdminBootstrap:Enabled"] = "true",
                ["AdminBootstrap:Email"] = "bootstrap-admin@example.test",
                ["AdminBootstrap:DisplayName"] = "Bootstrap Administrator",
                ["AdminBootstrap:TemporaryPassword"] = "temporary bootstrap password",
                ["AdminBootstrap:TotpSecret"] = "JBSWY3DPEHPK3PXP",
                ["Catalog:Cursors:SigningKey"] = "Dorosak-integration-cursor-signing-key-2026",
                ["Catalog:Cursors:Environment"] = "integration",
                ["Media:Storage:Enabled"] = "false",
            })
            .Build();

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

internal sealed class TestObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _multipartLengths = new(StringComparer.Ordinal);

    public List<string> AbortedUploadIds { get; } = [];

    public List<string> DeletedObjectKeys { get; } = [];

    public string Provider => "Test";

    public Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken)
    {
        string uploadId = Guid.NewGuid().ToString("N");
        _multipartLengths[uploadId] = request.ContentLength;
        return Task.FromResult(new ObjectStorageMultipartUpload(uploadId, null));
    }

    public async Task<ObjectStoragePutResult> PutObjectAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        byte[] bytes = memory.ToArray();
        _objects[request.ObjectKey] = bytes;
        return new ObjectStoragePutResult($"\"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))}\"", null, bytes.Length, "Test", "test-media");
    }

    public Task<Uri> CreatePartUploadUrlAsync(string objectKey, string uploadId, int partNumber, long contentLength, string sha256, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(new Uri($"https://storage.test/upload/{uploadId}/{partNumber}", UriKind.Absolute));

    public Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(string objectKey, string uploadId, IReadOnlyList<ObjectStoragePart> parts, CancellationToken cancellationToken) =>
        Task.FromResult(new ObjectStorageCompleteResult("\"multipart-etag\"", null, _multipartLengths[uploadId]));

    public Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken)
    {
        AbortedUploadIds.Add(uploadId);
        return Task.CompletedTask;
    }

    public Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (!_objects.TryGetValue(objectKey, out byte[]? bytes))
        {
            throw new StorageUnavailableException("Test object does not exist.");
        }
        return Task.FromResult<ObjectStorageReadResult>(new ObjectStorageReadResult(new MemoryStream(bytes), null, null, bytes.Length, "application/octet-stream"));
    }

    public Task<Uri> CreateDownloadUrlAsync(string objectKey, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(new Uri($"https://storage.test/download/{Guid.NewGuid():N}", UriKind.Absolute));

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        _objects.Remove(objectKey);
        DeletedObjectKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public void ClearObservations()
    {
        AbortedUploadIds.Clear();
        DeletedObjectKeys.Clear();
    }
}
