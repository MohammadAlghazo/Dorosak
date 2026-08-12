using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dorosak.Api.Realtime;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CommunicationsRealtimeEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task HubNegotiateRequiresAuthorizationHeaderAndQueryTokenIsTransportOnly()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const string negotiatePath = "/hubs/communications/negotiate?negotiateVersion=1";
        using HttpResponseMessage anonymous = await fixture.Client.PostAsync(
            negotiatePath,
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        string accessToken = await CreateAccessTokenAsync(cancellationToken);
        using HttpResponseMessage queryAuthenticated = await fixture.Client.PostAsync(
            $"{negotiatePath}&access_token={Uri.EscapeDataString(accessToken)}",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, queryAuthenticated.StatusCode);

        using var headerRequest = new HttpRequestMessage(HttpMethod.Post, negotiatePath);
        headerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage headerAuthenticated = await fixture.Client.SendAsync(
            headerRequest,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, headerAuthenticated.StatusCode);
        using (JsonDocument document = JsonDocument.Parse(
                   await headerAuthenticated.Content.ReadAsStringAsync(cancellationToken)))
        {
            Assert.True(document.RootElement.TryGetProperty("connectionToken", out _));
            Assert.NotEmpty(document.RootElement.GetProperty("availableTransports").EnumerateArray());
        }

        using HttpResponseMessage queryTransport = await fixture.Client.GetAsync(
            $"{CommunicationsHub.Path}?access_token={Uri.EscapeDataString(accessToken)}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, queryTransport.StatusCode);

        using HttpResponseMessage queryPostTransport = await fixture.Client.PostAsync(
            $"{CommunicationsHub.Path}?access_token={Uri.EscapeDataString(accessToken)}",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, queryPostTransport.StatusCode);

        using HttpResponseMessage irrelevantRoute = await fixture.Client.GetAsync(
            $"/api/v1/conversations?access_token={Uri.EscapeDataString(accessToken)}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, irrelevantRoute.StatusCode);
    }

    [Fact]
    public void HubExposesNoClientInvokableMethodsAndRealtimeDefaultsAreSafe()
    {
        string[] declaredMethods = typeof(CommunicationsHub)
            .GetMethods(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["OnConnectedAsync", "OnDisconnectedAsync"], declaredMethods);

        CommunicationsRealtimeOptions options = fixture.Factory.Services
            .GetRequiredService<IOptions<CommunicationsRealtimeOptions>>()
            .Value;
        Assert.False(options.DispatcherEnabled);
        Assert.False(options.SingleNodeMode);
        Assert.Equal(TimeSpan.FromSeconds(10), options.SessionValidationInterval);
        Assert.False(options.Redis.Enabled);
        Assert.Equal("RedisRealtime", options.Redis.ConnectionStringName);
        Assert.Equal("dorosak", options.Redis.ChannelPrefixRoot);
    }

    [Fact]
    public void EnablingRedisBackplaneWithoutItsConnectionFailsConfigurationValidation()
    {
        using WebApplicationFactory<Program> factory = fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("CommunicationsRealtime:Redis:Enabled", "true")
            .UseSetting("CommunicationsRealtime:Redis:ConnectionStringName", "MissingRealtimeRedis"));

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains(
            "Communications realtime Redis connection string is required",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EnablingDispatcherWithoutRedisOrExplicitSingleNodeModeFailsConfigurationValidation()
    {
        using WebApplicationFactory<Program> factory = fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("CommunicationsRealtime:DispatcherEnabled", "true")
            .UseSetting("CommunicationsRealtime:SingleNodeMode", "false")
            .UseSetting("CommunicationsRealtime:Redis:Enabled", "false"));

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains(
            "Communications realtime dispatching requires Redis or explicit single-node mode",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task HubTransportRouteIsNotPartOfOpenApi()
    {
        using HttpResponseMessage response = await fixture.Client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty(CommunicationsHub.Path, out _));
    }

    private async Task<string> CreateAccessTokenAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string marker = Guid.CreateVersion7().ToString("N");
        ApplicationUser user = ApplicationUser.Create(
            $"realtime-endpoint-{marker}",
            $"realtime-endpoint-{marker}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);
        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.82", "realtime endpoint test", "en")),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        return signIn.Value.Session!.AccessToken;
    }
}
