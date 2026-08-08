using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class MediaEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task PrivateMediaStatus_HidesAssetFromAnotherUser()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser owner = await CreateUserAsync("media-owner", cancellationToken);
        SignedInUser other = await CreateUserAsync("media-other", cancellationToken);
        Guid assetId = Guid.NewGuid();

        using HttpRequestMessage request = Authorized(HttpMethod.Get, $"/api/v1/media/{assetId:D}/status", other.AccessToken);
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadCreation_RequiresIdempotencyAndNeverAcceptsClientObjectKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser owner = await CreateUserAsync("media-create", cancellationToken);
        using HttpRequestMessage missingKey = Authorized(HttpMethod.Post, "/api/v1/uploads", owner.AccessToken);
        missingKey.Content = JsonContent.Create(new
        {
            purpose = "CourseDocument",
            expectedBytes = 10,
            fileName = "lesson.pdf",
            contentType = "application/pdf",
        });
        using HttpResponseMessage response = await fixture.Client.SendAsync(missingKey, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CaptionCreation_HidesAnotherOwnersVideo()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser owner = await CreateUserAsync("caption-owner", cancellationToken);
        SignedInUser other = await CreateUserAsync("caption-other", cancellationToken);
        MediaAsset video = MediaAsset.Create(
            Guid.CreateVersion7(),
            owner.UserId,
            null,
            MediaPurpose.SourceVideo,
            "source.mp4",
            "video/mp4",
            100,
            new string('a', 64),
            "quarantine/test/video",
            "Test",
            "test-media",
            DateTimeOffset.UtcNow);
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            DorosakDbContext db = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            db.Set<MediaAsset>().Add(video);
            await db.SaveChangesAsync(cancellationToken);
        }
        using HttpRequestMessage request = Authorized(HttpMethod.Post, $"/api/v1/media/{video.Id:D}/captions", other.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(new { locale = "en", label = "English", expectedBytes = 100, fileName = "english.vtt" });

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<SignedInUser> CreateUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(prefix, $"{prefix}-{Guid.CreateVersion7():N}@example.test", DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<SignInResponse> signIn = await sender.Send(
            new SignInCommand(user.Email!, "correct horse battery staple", new IdentityRequestContext("198.51.100.31", "media test", "en")),
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
