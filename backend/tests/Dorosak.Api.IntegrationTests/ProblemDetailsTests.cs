using System.Net;
using System.Text.Json;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ProblemDetailsTests(ApiFixture fixture)
{
    [Fact]
    public async Task UnknownRoute_ReturnsNormalizedProblemDetails()
    {
        using HttpResponseMessage response = await fixture.Client.GetAsync(
            "/api/v1/does-not-exist",
            TestContext.Current.CancellationToken);
        string payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("HTTP.NOT_FOUND", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-ID").Single(),
            root.GetProperty("correlationId").GetString());
    }
}
