using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.MediaWorker;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dorosak.Application.IntegrationTests.Media;

[Collection(InfrastructureTestGroup.Name)]
public sealed class MediaIntegrationTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task StreamLifecycle_ReservesThenReleasesAndQueuesJob()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-lifecycle", cancellationToken);
        Guid courseId = await CreateCourseAsync(userId, cancellationToken);
        string content = "%PDF-1.7 test";
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "CourseDocument", content.Length, "lesson.pdf", "application/pdf", courseId, Guid.NewGuid().ToString("N")), cancellationToken);
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
        Guid courseId = await CreateCourseAsync(userId, cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "SourceVideo", 40L * 1024 * 1024, "video.mp4", "video/mp4", courseId, Guid.NewGuid().ToString("N")), cancellationToken);
        string checksum = new string('a', 64);
        Result<UploadPartResponse> first = await sender.Send(
            new IssueUploadPartCommand(userId, created.Value.UploadSessionId, 1, 16L * 1024 * 1024, checksum), cancellationToken);
        Result<UploadPartResponse> wrongSize = await sender.Send(
            new IssueUploadPartCommand(userId, created.Value.UploadSessionId, 2, 8L * 1024 * 1024, checksum), cancellationToken);
        Result<UploadPartResponse> duplicate = await sender.Send(
            new IssueUploadPartCommand(userId, created.Value.UploadSessionId, 1, 16L * 1024 * 1024, checksum), cancellationToken);
        Result<UploadSessionResponse> cancelled = await sender.Send(
            new CancelUploadCommand(userId, created.Value.UploadSessionId, Guid.NewGuid().ToString("N")), cancellationToken);
        Result<UploadSessionResponse> repeated = await sender.Send(
            new CancelUploadCommand(userId, created.Value.UploadSessionId, Guid.NewGuid().ToString("N")), cancellationToken);

        Assert.True(first.IsSuccess);
        Assert.False(wrongSize.IsSuccess);
        Assert.Equal("MEDIA.PART_SIZE_INVALID", wrongSize.Failure.Code);
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
        Guid courseId = await CreateCourseAsync(userId, cancellationToken);
        await using AsyncServiceScope setupScope = fixture.Services.CreateAsyncScope();
        ISender sender = setupScope.ServiceProvider.GetRequiredService<ISender>();
        const string content = "%PDF-1.7 claim";
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "CourseDocument", content.Length, "claim.pdf", "application/pdf", courseId, Guid.NewGuid().ToString("N")), cancellationToken);
        await using AsyncServiceScope uploadScope = fixture.Services.CreateAsyncScope();
        Result<UploadSessionResponse> uploaded = await uploadScope.ServiceProvider.GetRequiredService<ISender>().Send(
            new PutUploadContentCommand(userId, created.Value.UploadSessionId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "application/pdf", Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)))), cancellationToken);
        Assert.True(uploaded.IsSuccess);

        await using (AsyncServiceScope cleanupScope = fixture.Services.CreateAsyncScope())
        {
            DorosakDbContext cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            await cleanupDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE media.media_processing_jobs SET state = 'Completed', completed_at = CURRENT_TIMESTAMP WHERE asset_id <> {created.Value.AssetId}",
                cancellationToken);
        }

        await using AsyncServiceScope firstScope = fixture.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = fixture.Services.CreateAsyncScope();
        IMediaJobStore firstStore = firstScope.ServiceProvider.GetRequiredService<IMediaJobStore>();
        IMediaJobStore secondStore = secondScope.ServiceProvider.GetRequiredService<IMediaJobStore>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Task<MediaJobClaim?> firstClaim = firstStore.TryClaimAsync(now, TimeSpan.FromMinutes(5), cancellationToken);
        Task<MediaJobClaim?> secondClaim = secondStore.TryClaimAsync(now, TimeSpan.FromMinutes(5), cancellationToken);
        MediaJobClaim?[] claims = await Task.WhenAll(firstClaim, secondClaim);

        Assert.Single(claims, claim => claim is not null);
    }

    [Fact]
    public async Task StreamUpload_StorageOutageFailsClosedAndKeepsReservation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-storage-outage", cancellationToken);
        Guid courseId = await CreateCourseAsync(userId, cancellationToken);
        const string content = "%PDF-1.7 outage%%EOF";
        IServiceCollection serviceCollection = fixture.CreateServices();
        serviceCollection.AddSingleton<IObjectStorage, OutageObjectStorage>();
        ServiceProvider services = serviceCollection.BuildServiceProvider();
        await using (services)
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<UploadSessionResponse> created = await sender.Send(
                new CreateUploadSessionCommand(userId, "CourseDocument", content.Length, "outage.pdf", "application/pdf", courseId, Guid.NewGuid().ToString("N")),
                cancellationToken);
            string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
            Result<UploadSessionResponse> upload = await sender.Send(
                new PutUploadContentCommand(userId, created.Value.UploadSessionId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "application/pdf", hash),
                cancellationToken);

            Assert.False(upload.IsSuccess);
            Assert.Equal("MEDIA.STORAGE_UNAVAILABLE", upload.Failure.Code);
            UploadSession session = await scope.ServiceProvider.GetRequiredService<DorosakDbContext>()
                .Set<UploadSession>().AsNoTracking().SingleAsync(item => item.Id == created.Value.UploadSessionId, cancellationToken);
            Assert.NotEqual(UploadSessionState.Completed, session.State);
            Assert.Equal(content.Length, session.ReservedBytes);
        }
    }

    [Fact]
    public async Task CaptionUpload_AssociatesValidatedTrackAndPersistsReadyMetadata()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-caption", cancellationToken);
        Guid courseId = await CreateCourseAsync(userId, cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> video = await sender.Send(
            new CreateUploadSessionCommand(userId, "SourceVideo", 40L * 1024 * 1024, "source.mp4", "video/mp4", courseId, Guid.NewGuid().ToString("N")),
            cancellationToken);
        Assert.True(video.IsSuccess);
        const string content = "WEBVTT\n\n00:00.000 --> 00:01.000\nHello\n";
        Result<UploadSessionResponse> caption = await sender.Send(
            new CreateCaptionUploadCommand(userId, video.Value.AssetId, "en", "English", content.Length, "english.vtt", Guid.NewGuid().ToString("N")),
            cancellationToken);
        Assert.True(caption.IsSuccess);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
        Result<UploadSessionResponse> uploaded = await sender.Send(
            new PutUploadContentCommand(userId, caption.Value.UploadSessionId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "text/vtt", hash),
            cancellationToken);
        Assert.True(uploaded.IsSuccess);

        DorosakDbContext db = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        CaptionTrack track = await db.Set<CaptionTrack>().AsNoTracking().SingleAsync(item => item.SourceMediaAssetId == caption.Value.AssetId, cancellationToken);
        Assert.Equal(video.Value.AssetId, track.AssetId);
        Assert.Equal(CaptionTrackState.Pending, track.State);
        IMediaProcessingStore store = scope.ServiceProvider.GetRequiredService<IMediaProcessingStore>();
        await store.MarkScanningAsync(caption.Value.AssetId, cancellationToken);
        await store.MarkProcessingAsync(caption.Value.AssetId, cancellationToken);
        var variant = new MediaVariantFile(track.Id, "caption", "caption.vtt", "text/vtt", track.ObjectKey, hash);
        await store.MarkReadyAsync(
            caption.Value.AssetId,
            content.Length,
            hash,
            [variant],
            new Dictionary<string, ObjectStoragePutResult> { ["caption"] = new("caption-etag", "caption-version", content.Length, "Test", "test-media") },
            DateTimeOffset.UtcNow,
            cancellationToken);

        CaptionTrack ready = await db.Set<CaptionTrack>().AsNoTracking().SingleAsync(item => item.Id == track.Id, cancellationToken);
        Assert.Equal(CaptionTrackState.Ready, ready.State);
        Assert.Equal(hash, ready.Sha256);
        Result<MediaStatusResponse> status = await sender.Send(new GetMediaStatusQuery(userId, video.Value.AssetId), cancellationToken);
        Assert.Equal(track.Id, Assert.Single(status.Value.Captions).Id);
    }

    [Fact]
    public async Task AssignmentSubmissionCreation_RequiresConcreteSubmissionContext()
    {
        Guid userId = await CreateUserAsync("media-assignment-deferred", TestContext.Current.CancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        Result<UploadSessionResponse> result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateUploadSessionCommand(
                userId,
                "AssignmentSubmission",
                100,
                "answer.pdf",
                "application/pdf",
                null,
                Guid.NewGuid().ToString("N"),
                ClientFileId: Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("MEDIA.ASSIGNMENT_SUBMISSION_REQUIRED", result.Failure.Code);
    }

    [Fact]
    public async Task StreamUpload_RejectsBodyLargerThanDeclaredLength()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-oversized-stream", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "ProfileImage", 4, "avatar.jpg", "image/jpeg", null, Guid.NewGuid().ToString("N")),
            cancellationToken);
        byte[] bytes = [1, 2, 3, 4, 5];
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes.AsSpan(0, 4)));

        Result<UploadSessionResponse> result = await sender.Send(
            new PutUploadContentCommand(userId, created.Value.UploadSessionId, new MemoryStream(bytes), 4, "image/jpeg", hash),
            cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("MEDIA.CONTENT_LENGTH_MISMATCH", result.Failure.Code);
    }

    [Fact]
    public async Task DownloadGrant_IsUnavailableUntilAssetIsReady()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-ready-grant", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<UploadSessionResponse> created = await sender.Send(
            new CreateUploadSessionCommand(userId, "ProfileImage", 4, "avatar.jpg", "image/jpeg", null, Guid.NewGuid().ToString("N")),
            cancellationToken);

        Result<DownloadGrantResponse> grant = await sender.Send(
            new CreateDownloadGrantCommand(userId, created.Value.AssetId, null, null),
            cancellationToken);

        Assert.False(grant.IsSuccess);
        Assert.Equal("MEDIA.NOT_FOUND", grant.Failure.Code);
    }

    [Fact]
    public async Task Cleanup_AbortsInterruptedMultipartThenDeletesAfterGracePeriod()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-cleanup", cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid assetId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();
        const string objectKey = "quarantine/test/interrupted/original";
        await using (AsyncServiceScope setup = fixture.Services.CreateAsyncScope())
        {
            DorosakDbContext db = setup.ServiceProvider.GetRequiredService<DorosakDbContext>();
            MediaAsset asset = MediaAsset.Create(assetId, userId, null, MediaPurpose.ProfileImage, "avatar.jpg", "image/jpeg", 100, new string('a', 64), objectKey, "Test", "test-media", now.AddDays(-2));
            UploadSession session = UploadSession.Create(sessionId, userId, assetId, MediaPurpose.ProfileImage, 100, 100, "avatar.jpg", "image/jpeg", null, objectKey, null, now.AddDays(-2), now.AddHours(-1));
            session.BeginUploading();
            session.SetMultipartUploadId("interrupted-upload");
            db.Set<MediaAsset>().Add(asset);
            db.Set<UploadSession>().Add(session);
            await db.SaveChangesAsync(cancellationToken);
        }
        TestObjectStorage storage = Assert.IsType<TestObjectStorage>(fixture.Services.GetRequiredService<IObjectStorage>());
        storage.ClearObservations();
        var timeProvider = new MutableTimeProvider(now);
        var worker = new MediaCleanupWorker(
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MediaOptions { OrphanGracePeriod = TimeSpan.FromHours(24) }),
            NullLogger<MediaCleanupWorker>.Instance,
            timeProvider);

        await worker.CleanupOnceAsync(cancellationToken);
        Assert.Contains("interrupted-upload", storage.AbortedUploadIds);
        Assert.DoesNotContain(objectKey, storage.DeletedObjectKeys);
        timeProvider.SetUtcNow(now.AddHours(25));
        await worker.CleanupOnceAsync(cancellationToken);
        Assert.Contains(objectKey, storage.DeletedObjectKeys);
        await using AsyncServiceScope verify = fixture.Services.CreateAsyncScope();
        MediaAsset deleted = await verify.ServiceProvider.GetRequiredService<DorosakDbContext>().Set<MediaAsset>().AsNoTracking().SingleAsync(item => item.Id == assetId, cancellationToken);
        Assert.Equal(MediaAssetState.Deleted, deleted.State);
    }

    [Fact]
    public async Task AccountQuota_IncludesGeneratedVariantBytes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateUserAsync("media-variant-quota", cancellationToken);
        Guid assetId = Guid.CreateVersion7();
        await using (AsyncServiceScope setup = fixture.Services.CreateAsyncScope())
        {
            DorosakDbContext db = setup.ServiceProvider.GetRequiredService<DorosakDbContext>();
            db.Set<MediaAsset>().Add(MediaAsset.Create(assetId, userId, null, MediaPurpose.ProfileImage, "existing.jpg", "image/jpeg", 1, new string('a', 64), "quarantine/test/quota", "Test", "test-media", DateTimeOffset.UtcNow));
            db.Set<MediaVariant>().Add(MediaVariant.Create(
                Guid.CreateVersion7(), assetId, "image-jpeg-320", "image/jpeg", "ready/test/quota.jpg", "Test", "test-media",
                10L * 1024 * 1024 * 1024, new string('b', 64), "etag", null, 320, 180, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        Result<UploadSessionResponse> result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateUploadSessionCommand(userId, "ProfileImage", 1, "new.jpg", "image/jpeg", null, Guid.NewGuid().ToString("N")),
            cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("MEDIA.ACCOUNT_QUOTA_EXCEEDED", result.Failure.Code);
    }

    [Fact]
    public async Task CourseMedia_AllowsCurrentCoInstructorCollaborator()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid ownerId = await CreateUserAsync("media-course-owner", cancellationToken);
        Guid collaboratorId = await CreateUserAsync("media-course-collaborator", cancellationToken);
        Guid courseId = await CreateCourseAsync(ownerId, cancellationToken);
        await using (AsyncServiceScope setup = fixture.Services.CreateAsyncScope())
        {
            DorosakDbContext db = setup.ServiceProvider.GetRequiredService<DorosakDbContext>();
            db.Set<CourseInstructor>().Add(CourseInstructor.Create(courseId, collaboratorId, CourseCollaboratorRole.CoInstructor, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        Result<UploadSessionResponse> result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateUploadSessionCommand(collaboratorId, "CourseImage", 100, "cover.jpg", "image/jpeg", courseId, Guid.NewGuid().ToString("N")),
            cancellationToken);

        Assert.True(result.IsSuccess);
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

    private async Task<Guid> CreateCourseAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        Course course = Course.Create(userId, "en", DateTimeOffset.UtcNow);
        scope.ServiceProvider.GetRequiredService<DorosakDbContext>().Set<Course>().Add(course);
        await scope.ServiceProvider.GetRequiredService<DorosakDbContext>().SaveChangesAsync(cancellationToken);
        return course.Id;
    }
}

internal sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
{
    private DateTimeOffset _value = value;

    public override DateTimeOffset GetUtcNow() => _value;

    public void SetUtcNow(DateTimeOffset value) => _value = value;
}

internal sealed class OutageObjectStorage : IObjectStorage
{
    public string Provider => "Outage";

    public Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken) =>
        Task.FromException<ObjectStorageMultipartUpload>(Unavailable());
    public Task<ObjectStoragePutResult> PutObjectAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken) =>
        Task.FromException<ObjectStoragePutResult>(Unavailable());
    public Task<Uri> CreatePartUploadUrlAsync(string objectKey, string uploadId, int partNumber, long contentLength, string sha256, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromException<Uri>(Unavailable());
    public Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(string objectKey, string uploadId, IReadOnlyList<ObjectStoragePart> parts, CancellationToken cancellationToken) =>
        Task.FromException<ObjectStorageCompleteResult>(Unavailable());
    public Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken) => Task.FromException(Unavailable());
    public Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken) => Task.FromException<ObjectStorageReadResult>(Unavailable());
    public Task<Uri> CreateDownloadUrlAsync(string objectKey, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromException<Uri>(Unavailable());
    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken) => Task.FromException(Unavailable());

    private static StorageUnavailableException Unavailable() => new("Test storage outage.");
}
