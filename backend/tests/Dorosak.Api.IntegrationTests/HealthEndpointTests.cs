using System.Net;
using System.Text.Json;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class HealthEndpointTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public async Task HealthEndpoints_ReturnMinimalHealthyResponse(string path)
    {
        using HttpResponseMessage response = await fixture.Client.GetAsync(
            path,
            TestContext.Current.CancellationToken);
        string payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonProperty property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("status", property.Name);
        Assert.Equal("healthy", property.Value.GetString());
    }

    [Fact]
    public async Task Readiness_RejectsDatabaseWithoutRequiredMigrations()
    {
        string emptyDatabase = await fixture.CreateEmptyDatabaseAsync(TestContext.Current.CancellationToken);
        await using var factory = new DorosakApiFactory(emptyDatabase);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);
        string payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonProperty property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("unhealthy", property.Value.GetString());
    }
}
