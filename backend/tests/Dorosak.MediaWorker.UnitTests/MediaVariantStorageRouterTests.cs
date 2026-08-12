using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.MediaWorker;

namespace Dorosak.MediaWorker.UnitTests;

public sealed class MediaVariantStorageRouterTests
{
    [Fact]
    public async Task SourceVideoPoster_RemainsInObjectStorage()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"dorosak-video-poster-{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(filePath, [1, 2, 3], TestContext.Current.CancellationToken);
            Guid assetId = Guid.CreateVersion7();
            Guid variantId = Guid.CreateVersion7();
            var input = new MediaProcessingInput(
                assetId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MediaPurpose.SourceVideo,
                "source.mp4",
                "video/mp4",
                3,
                new string('a', 64),
                "quarantine/test/source");
            var variant = new MediaVariantFile(
                variantId,
                "video-poster",
                filePath,
                "image/jpeg",
                "ready/test/video/poster.jpg",
                new string('b', 64));
            var objectStorage = new RecordingObjectStorage();
            var processedImageStore = new RecordingProcessedImageStore(canStore: true);

            MediaVariantUploadBatch result = await MediaVariantStorageRouter.StoreAsync(
                input,
                [variant],
                objectStorage,
                processedImageStore,
                TestContext.Current.CancellationToken);

            Assert.Equal([variant.ObjectKey], objectStorage.UploadedKeys);
            Assert.Empty(processedImageStore.UploadedVariantIds);
            Assert.Equal("S3", result.Uploads[variant.Kind].Provider);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImageAssetVariants_UseProcessedImageStoreWhenAvailable()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"dorosak-profile-image-{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(filePath, [1, 2, 3], TestContext.Current.CancellationToken);
            Guid assetId = Guid.CreateVersion7();
            Guid variantId = Guid.CreateVersion7();
            var input = new MediaProcessingInput(
                assetId,
                Guid.NewGuid(),
                null,
                MediaPurpose.ProfileImage,
                "profile.png",
                "image/png",
                3,
                new string('a', 64),
                "quarantine/test/profile");
            var variant = new MediaVariantFile(
                variantId,
                "image-jpeg-320",
                filePath,
                "image/jpeg",
                "ready/test/profile.jpg",
                new string('b', 64));
            var objectStorage = new RecordingObjectStorage();
            var processedImageStore = new RecordingProcessedImageStore(canStore: true);

            MediaVariantUploadBatch result = await MediaVariantStorageRouter.StoreAsync(
                input,
                [variant],
                objectStorage,
                processedImageStore,
                TestContext.Current.CancellationToken);

            Assert.Empty(objectStorage.UploadedKeys);
            Assert.Equal([variantId], processedImageStore.UploadedVariantIds);
            Assert.Equal("Cloudinary", result.Uploads[variant.Kind].Provider);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task FailedBatchRollsBackBothProcessedImagesAndObjectStorageVariants()
    {
        string firstPath = Path.Combine(Path.GetTempPath(), $"dorosak-image-first-{Guid.NewGuid():N}.jpg");
        string secondPath = Path.Combine(Path.GetTempPath(), $"dorosak-image-second-{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(firstPath, [1, 2, 3], TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(secondPath, [4, 5, 6], TestContext.Current.CancellationToken);
            var input = new MediaProcessingInput(
                Guid.CreateVersion7(),
                Guid.NewGuid(),
                null,
                MediaPurpose.ProfileImage,
                "profile.png",
                "image/png",
                3,
                new string('a', 64),
                "quarantine/test/profile");
            MediaVariantFile[] variants =
            [
                new(Guid.CreateVersion7(), "document", secondPath, "application/pdf", "ready/second.pdf", new string('c', 64)),
                new(Guid.CreateVersion7(), "image-jpeg-320", firstPath, "image/jpeg", "ready/first.jpg", new string('b', 64)),
            ];
            var objectStorage = new RecordingObjectStorage();
            var processedImageStore = new RecordingProcessedImageStore(canStore: true)
            {
                ThrowOnPut = true,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => MediaVariantStorageRouter.StoreAsync(
                input,
                variants,
                objectStorage,
                processedImageStore,
                TestContext.Current.CancellationToken));

            Assert.Equal(["ready/second.pdf"], objectStorage.UploadedKeys);
            Assert.Equal(["ready/second.pdf"], objectStorage.DeletedKeys);
            Assert.Empty(processedImageStore.UploadedVariantIds);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Fact]
    public void ImageVariantIdsAreStableAcrossRetries()
    {
        Guid assetId = Guid.Parse("01911111-1111-7111-8111-111111111111");

        Guid first = FfmpegMediaProcessor.CreateDeterministicImageVariantId(assetId, "image-jpeg-320");
        Guid second = FfmpegMediaProcessor.CreateDeterministicImageVariantId(assetId, "image-jpeg-320");
        Guid differentKind = FfmpegMediaProcessor.CreateDeterministicImageVariantId(assetId, "image-webp-320");

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentKind);
    }

    private sealed class RecordingProcessedImageStore(bool canStore) : IProcessedImageStore
    {
        public List<Guid> UploadedVariantIds { get; } = [];

        public bool ThrowOnPut { get; init; }

        public bool CanStore(MediaProcessingInput input, MediaVariantFile variant) => canStore;

        public Task<ObjectStoragePutResult> PutAsync(ProcessedImageUploadRequest request, CancellationToken cancellationToken)
        {
            if (ThrowOnPut)
            {
                throw new InvalidOperationException("Synthetic processed image failure.");
            }
            UploadedVariantIds.Add(request.VariantId);
            return Task.FromResult(new ObjectStoragePutResult(
                "cloud-etag",
                "42",
                request.ContentLength,
                "Cloudinary",
                "test-cloud",
                $"dorosak/test/{request.AssetId:D}/{request.VariantId:D}"));
        }

        public Task<Uri> CreateDownloadUrlAsync(string objectKey, string? versionId, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, string? versionId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingObjectStorage : IObjectStorage
    {
        public List<string> UploadedKeys { get; } = [];

        public List<string> DeletedKeys { get; } = [];

        public string Provider => "S3";

        public Task<ObjectStoragePutResult> PutObjectAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken)
        {
            UploadedKeys.Add(request.ObjectKey);
            return Task.FromResult(new ObjectStoragePutResult(
                "s3-etag",
                null,
                request.ContentLength,
                Provider,
                "test-bucket",
                request.ObjectKey));
        }

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Uri> CreatePartUploadUrlAsync(string objectKey, string uploadId, int partNumber, long contentLength, string sha256, TimeSpan lifetime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(string objectKey, string uploadId, IReadOnlyList<ObjectStoragePart> parts, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Uri> CreateDownloadUrlAsync(string objectKey, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

    }
}
