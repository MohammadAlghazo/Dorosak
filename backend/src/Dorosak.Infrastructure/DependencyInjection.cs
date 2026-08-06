using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Persistence;
using Dorosak.Infrastructure.Caching;
using Dorosak.Infrastructure.Idempotency;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string databaseConnection = GetRequiredConnectionString(configuration, "Database");
        string redisConnection = GetRequiredConnectionString(configuration, "Redis");

        services.AddDbContext<DorosakDbContext>(options =>
            DatabaseConfiguration.Configure(options, databaseConnection));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DorosakDbContext>());
        services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "dorosak:";
        });
        services.AddScoped<IQueryCache, DistributedQueryCache>();

        services.ConfigureHttpClientDefaults(httpClient => httpClient.AddStandardResilienceHandler());
        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name)
    {
        string? value = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Connection string '{name}' is required.")
            : value;
    }
}
