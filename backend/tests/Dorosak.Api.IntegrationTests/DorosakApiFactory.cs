using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dorosak.Api.IntegrationTests;

public sealed class DorosakApiFactory(string databaseConnection) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Development")
            .UseSetting("AllowedHosts", "*")
            .UseSetting("ConnectionStrings:Database", databaseConnection)
            .UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,abortConnect=false,connectTimeout=100")
            .UseSetting("Cors:AllowedOrigins:0", "https://app.dorosak.test");
    }
}
