using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Persistence;

internal static class DatabaseConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(AssemblyReference.Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", DorosakDbContext.MigrationsSchema);
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            })
            .UseSnakeCaseNamingConvention();
    }
}
