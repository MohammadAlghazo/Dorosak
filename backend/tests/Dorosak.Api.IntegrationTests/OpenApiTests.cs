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
        Assert.True(paths.TryGetProperty("/api/v1/reports", out JsonElement reports));
        JsonElement createReport = reports.GetProperty("post");
        Assert.True(FindHeader(createReport, "Idempotency-Key").GetProperty("required").GetBoolean());
        Assert.True(createReport.GetProperty("responses").GetProperty("200").TryGetProperty("content", out _));
        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/moderation-cases/{caseId}/actions",
            out JsonElement moderationActions));
        JsonElement applyAction = moderationActions.GetProperty("post");
        Assert.True(FindHeader(applyAction, "Idempotency-Key").GetProperty("required").GetBoolean());
        Assert.True(FindHeader(applyAction, "X-Audit-Reason").GetProperty("required").GetBoolean());
        Assert.True(applyAction.GetProperty("responses").GetProperty("200").TryGetProperty("content", out _));
        Assert.True(paths.TryGetProperty("/api/v1/conversations", out JsonElement conversations));
        JsonElement createConversation = conversations.GetProperty("post");
        Assert.True(FindHeader(createConversation, "Idempotency-Key").GetProperty("required").GetBoolean());
        Assert.True(createConversation.GetProperty("responses").GetProperty("200").TryGetProperty("content", out _));
        JsonElement createConversationSchema = ResolveRequestSchema(document.RootElement, createConversation);
        Assert.Contains(
            createConversationSchema.GetProperty("required").EnumerateArray(),
            property => string.Equals(property.GetString(), "courseId", StringComparison.Ordinal));
        JsonElement courseId = createConversationSchema.GetProperty("properties").GetProperty("courseId");
        Assert.Equal("string", courseId.GetProperty("type").GetString());
        Assert.False(courseId.TryGetProperty("nullable", out JsonElement nullable) && nullable.GetBoolean());
        Assert.True(paths.TryGetProperty(
            "/api/v1/conversations/{conversationId}/messages",
            out JsonElement messages));
        JsonElement createMessage = messages.GetProperty("post");
        Assert.True(FindHeader(createMessage, "Idempotency-Key").GetProperty("required").GetBoolean());
        Assert.True(createMessage.GetProperty("responses").GetProperty("200").TryGetProperty("content", out _));
        Assert.True(paths.TryGetProperty(
            "/api/v1/conversations/{conversationId}/participants/me",
            out JsonElement participation));
        JsonElement leaveConversation = participation.GetProperty("delete");
        Assert.True(leaveConversation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(leaveConversation.GetProperty("responses").TryGetProperty("404", out _));

        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        string csp = uiResponse.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'unsafe-inline'", csp, StringComparison.Ordinal);
    }

    private static JsonElement FindHeader(JsonElement operation, string name) => operation
        .GetProperty("parameters")
        .EnumerateArray()
        .Single(parameter => string.Equals(
            parameter.GetProperty("name").GetString(),
                name,
                StringComparison.OrdinalIgnoreCase));

    private static JsonElement ResolveRequestSchema(JsonElement document, JsonElement operation)
    {
        JsonElement schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        string reference = Assert.IsType<string>(schema.GetProperty("$ref").GetString());
        string schemaName = reference[(reference.LastIndexOf('/') + 1)..];
        return document.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
    }
}
