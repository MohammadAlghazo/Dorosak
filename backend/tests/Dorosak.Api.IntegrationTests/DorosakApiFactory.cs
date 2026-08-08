using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dorosak.Api.IntegrationTests;

public sealed class DorosakApiFactory(
    string databaseConnection,
    string redisConnection = "127.0.0.1:1,abortConnect=false,connectTimeout=100") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Development")
            .UseSetting("AllowedHosts", "*")
            .UseSetting("ConnectionStrings:Database", databaseConnection)
            .UseSetting("ConnectionStrings:Redis", redisConnection)
            .UseSetting("ConnectionStrings:RedisSecurity", redisConnection)
            .UseSetting("App:PublicUrl", "https://app.dorosak.test")
            .UseSetting("Identity:RefreshRaceWindowSeconds", "1")
            .UseSetting("Media:Storage:Enabled", "false")
            .UseSetting("Cors:AllowedOrigins:0", "https://app.dorosak.test");
    }
}
