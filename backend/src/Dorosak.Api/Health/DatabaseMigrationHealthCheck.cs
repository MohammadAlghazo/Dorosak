using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dorosak.Api.Health;

public sealed class DatabaseMigrationHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            string? expectedMigration = dbContext.Database.GetMigrations().LastOrDefault();
            if (expectedMigration is null)
            {
                return HealthCheckResult.Unhealthy("The application does not define a database schema version.");
            }

            string? compatibilityRange = await dbContext.Database
                .SqlQueryRaw<string>(
                    "SELECT minimum_compatible_migration_id || '|' || maximum_compatible_migration_id AS \"Value\" " +
                    "FROM operations.schema_compatibility WHERE singleton")
                .SingleOrDefaultAsync(cancellationToken);
            string[] boundaries = compatibilityRange?.Split('|', 2) ?? [];
            bool isCompatible = boundaries.Length == 2
                && string.CompareOrdinal(expectedMigration, boundaries[0]) >= 0
                && string.CompareOrdinal(expectedMigration, boundaries[1]) <= 0;

            return isCompatible
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database schema is not compatible with this application version.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The database schema could not be verified.", exception);
        }
    }
}
