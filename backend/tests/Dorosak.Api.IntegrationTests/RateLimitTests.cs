using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(ApiFixture fixture)
{
    [Fact]
    public async Task PublicLimit_ReturnsProblemDetailsAndRetryAfter()
    {
        await using DorosakApiFactory factory = fixture.CreateFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        for (int attempt = 0; attempt < 120; attempt++)
        {
            using HttpResponseMessage accepted = await client.GetAsync(
                "/api/v1/system/status",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using HttpResponseMessage rejected = await client.GetAsync(
            "/api/v1/system/status",
            TestContext.Current.CancellationToken);
        string payload = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(payload);
        Assert.Equal("RATE_LIMIT.EXCEEDED", document.RootElement.GetProperty("code").GetString());
    }
}
