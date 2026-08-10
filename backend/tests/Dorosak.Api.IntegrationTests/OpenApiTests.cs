using System.Net;
using System.Text.Json;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class OpenApiTests(ApiFixture fixture)
{
    [Fact]
    public async Task DevelopmentOpenApi_UsesVersion31AndUsableSwaggerCsp()
    {
        using HttpResponseMessage documentResponse = await fixture.Client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        string documentPayload = await documentResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using HttpResponseMessage uiResponse = await fixture.Client.GetAsync(
            "/swagger/index.html",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using JsonDocument document = JsonDocument.Parse(documentPayload);
        Assert.StartsWith("3.1.", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        JsonElement paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty(
            "/api/v1/learning/enrollments/{enrollmentId}/lessons/{lessonId}/discussions",
            out JsonElement learnerDiscussions));
        Assert.True(paths.TryGetProperty(
            "/api/v1/instructor/courses/{courseId}/releases/{releaseId}/discussions",
            out _));
        JsonElement idempotencyParameter = learnerDiscussions
            .GetProperty("post")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => string.Equals(
                parameter.GetProperty("name").GetString(),
                "Idempotency-Key",
                StringComparison.OrdinalIgnoreCase));
        Assert.True(idempotencyParameter.GetProperty("required").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        string csp = uiResponse.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'unsafe-inline'", csp, StringComparison.Ordinal);
    }
}
