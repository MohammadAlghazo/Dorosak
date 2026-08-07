using System.Net;
using System.Text.Json;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class SystemEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Status_ReturnsCachedContractWithFreshCorrelationHeaders()
    {
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/status");
        firstRequest.Headers.Add("X-Correlation-ID", "untrusted-client-value");
        using HttpResponseMessage first = await fixture.Client.SendAsync(
            firstRequest,
            TestContext.Current.CancellationToken);
        string firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        using HttpResponseMessage second = await fixture.Client.GetAsync(
            "/api/v1/system/status",
            TestContext.Current.CancellationToken);
        string secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody, secondBody);

        using JsonDocument document = JsonDocument.Parse(firstBody);
        Assert.Equal("Dorosak.Api", document.RootElement.GetProperty("data").GetProperty("service").GetString());

        string firstCorrelation = first.Headers.GetValues("X-Correlation-ID").Single();
        string secondCorrelation = second.Headers.GetValues("X-Correlation-ID").Single();
        Assert.NotEqual("untrusted-client-value", firstCorrelation);
        Assert.NotEqual(firstCorrelation, secondCorrelation);
        Assert.Equal("nosniff", first.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'none'", first.Headers.GetValues("Content-Security-Policy").Single());
    }
}
