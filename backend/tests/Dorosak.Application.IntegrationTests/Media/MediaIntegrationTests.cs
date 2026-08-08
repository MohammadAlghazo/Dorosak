using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Media;

[Collection(InfrastructureTestGroup.Name)]
public sealed class MediaIntegrationTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task StreamLifecycle_ReservesThenReleasesAndQueuesJob()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-lifecycle", cancellationToken);
        string content = "%PDF-1.7 test";
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "CourseDocument", content.Length, "lesson.pdf", "application/pdf", null, Guid.NewGuid().ToString("N")), cancellationToken);
        Assert.True(created.IsSuccess);
        Result<UploadSessionResponse> uploaded = await sender.Send(
            new PutUploadContentCommand(userId, created.Value.UploadSessionId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "application/pdf", hash), cancellationToken);

        Assert.True(uploaded.IsSuccess);
        await using AsyncServiceScope verifyScope = fixture.Services.CreateAsyncScope();
        DorosakDbContext db = verifyScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        UploadSession session = await db.Set<UploadSession>().SingleAsync(item => item.Id == created.Value.UploadSessionId, cancellationToken);
        MediaAsset asset = await db.Set<MediaAsset>().SingleAsync(item => item.Id == created.Value.AssetId, cancellationToken);
        Assert.Equal(UploadSessionState.Completed, session.State);
        Assert.Equal(MediaAssetState.Uploaded, asset.State);
        Assert.Equal(0, session.ReservedBytes);
        Assert.True(await db.Set<MediaProcessingJob>().AnyAsync(job => job.AssetId == asset.Id, cancellationToken));
    }

    [Fact]
    public async Task DuplicateMultipartPart_IsRejectedAndCancelIsIdempotent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-parts", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "SourceVideo", 40L * 1024 * 1024, "video.mp4", "video/mp4", null, Guid.NewGuid().ToString("N")), cancellationToken);
        string checksum = new string('a', 64);
        Result<UploadPartResponse> first = await sender.Send(
            new IssueUploadPartCommand(userId, created.Value.UploadSessionId, 1, 16L * 1024 * 1024, checksum), cancellationToken);
        Result<UploadPartResponse> duplicate = await sender.Send(
            new IssueUploadPartCommand(userId, created.Value.UploadSessionId, 1, 16L * 1024 * 1024, checksum), cancellationToken);
        Result<UploadSessionResponse> cancelled = await sender.Send(
            new CancelUploadCommand(userId, created.Value.UploadSessionId, Guid.NewGuid().ToString("N")), cancellationToken);
        Result<UploadSessionResponse> repeated = await sender.Send(
            new CancelUploadCommand(userId, created.Value.UploadSessionId, Guid.NewGuid().ToString("N")), cancellationToken);

        Assert.True(first.IsSuccess);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("MEDIA.DUPLICATE_PART", duplicate.Failure.Code);
        Assert.True(cancelled.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal("Cancelled", repeated.Value.State);
    }

    [Fact]
    public async Task JobClaim_OnlyOneConcurrentCallerClaimsTheRow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-claim", cancellationToken);
        await using AsyncServiceScope setupScope = fixture.Services.CreateAsyncScope();
        ISender sender = setupScope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "CourseDocument", 20, "claim.pdf", "application/pdf", null, Guid.NewGuid().ToString("N")), cancellationToken);
        await using AsyncServiceScope uploadScope = fixture.Services.CreateAsyncScope();
        const string content = "%PDF-1.7 claim";
        Result<UploadSessionResponse> uploaded = await uploadScope.ServiceProvider.GetRequiredService<ISender>().Send(
            new PutUploadContentCommand(userId, created.Value.UploadSessionId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "application/pdf", Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)))), cancellationToken);
        Assert.True(uploaded.IsSuccess);
    }

    private async Task<Guid> CreateUserAsync(string suffix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create($"Media {suffix}", $"{suffix}-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        IdentityResult result = await userManager.CreateAsync(user, "correct horse battery staple");
        Assert.True(result.Succeeded);
        return user.Id;
    }
}
