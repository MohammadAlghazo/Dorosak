using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CommerceEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task DemoCheckout_RequiresPublishedCourseAndNeverAcceptsPaymentCredentials()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser learner = await CreateUserAsync("demo-commerce", cancellationToken);
        using HttpRequestMessage request = Authorized(
            HttpMethod.Post,
            "/api/v1/commerce/demo-checkout",
            learner.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(new
        {
            courseId = Guid.CreateVersion7(),
            outcome = "success",
        });

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("card", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvv", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SignedInUser> CreateUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.52", "demo commerce test", "en")),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        return new SignedInUser(user.Id, signIn.Value.Session!.AccessToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record SignedInUser(Guid UserId, string AccessToken);
}
