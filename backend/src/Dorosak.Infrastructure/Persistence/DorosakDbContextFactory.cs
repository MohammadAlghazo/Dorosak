using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dorosak.Infrastructure.Persistence;

public sealed class DorosakDbContextFactory : IDesignTimeDbContextFactory<DorosakDbContext>
{
    public DorosakDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("Migrations__ConnectionString")
            ?? throw new InvalidOperationException(
                "Migrations__ConnectionString must contain the direct migrator connection string.");

        var options = new DbContextOptionsBuilder<DorosakDbContext>();
        DatabaseConfiguration.Configure(options, connectionString);
        return new DorosakDbContext(options.Options);
    }
}
