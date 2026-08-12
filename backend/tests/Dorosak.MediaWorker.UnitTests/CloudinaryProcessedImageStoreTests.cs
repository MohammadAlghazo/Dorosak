using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Media;
using Microsoft.Extensions.Options;

namespace Dorosak.MediaWorker.UnitTests;

public sealed class CloudinaryProcessedImageStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private const string ApiSecret = "cloudinary-test-secret";

    [Fact]
    public async Task DisabledStore_RejectsCallsWithoutSendingHttpRequests()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("HTTP must not be called."));
        CloudinaryProcessedImageStore store = CreateStore(handler, new CloudinaryOptions());
        (MediaProcessingInput input, MediaVariantFile variant) = CreateImageInput();

        Assert.False(store.CanStore(input, variant));
        await using var content = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<StorageUnavailableException>(() => store.PutAsync(
            new ProcessedImageUploadRequest(input.AssetId, variant.VariantId, variant.ContentType, content, content.Length),
            TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Upload_UsesStableIdempotentPublicIdAndSortedSha1Signature()
    {
        (MediaProcessingInput input, MediaVariantFile variant) = CreateImageInput();
        string publicId = $"dorosak/test-env/{input.AssetId:D}/{variant.VariantId:D}";
        string response = UploadResponse(publicId, "jpg", 42, 3, "cloud-etag");
        var handler = new RecordingHandler(_ => JsonResponse(response));
        CloudinaryProcessedImageStore store = CreateStore(handler, EnabledOptions());

        for (int index = 0; index < 2; index++)
        {
            await using var content = new MemoryStream([1, 2, 3]);
            await store.PutAsync(
                new ProcessedImageUploadRequest(input.AssetId, variant.VariantId, variant.ContentType, content, content.Length),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, handler.Requests.Count);
        string canonical = $"discard_original_filename=true&overwrite=true&public_id={publicId}&timestamp={Now.ToUnixTimeSeconds()}&type=authenticated";
        string expectedSignature = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical + ApiSecret)));
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1_1/test-cloud/image/upload", request.Uri.AbsolutePath);
            Assert.Contains($"name=public_id\r\n\r\n{publicId}", request.Body, StringComparison.Ordinal);
            Assert.Contains("name=discard_original_filename\r\n\r\ntrue", request.Body, StringComparison.Ordinal);
            Assert.Contains("name=overwrite\r\n\r\ntrue", request.Body, StringComparison.Ordinal);
            Assert.Contains("name=type\r\n\r\nauthenticated", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("name=resource_type", request.Body, StringComparison.Ordinal);
            Assert.Contains($"name=signature\r\n\r\n{expectedSignature}", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("original-user-file-name", request.Body, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Upload_MapsCloudinaryStorageMetadata()
    {
        (MediaProcessingInput input, MediaVariantFile variant) = CreateImageInput();
        string publicId = $"dorosak/test-env/{input.AssetId:D}/{variant.VariantId:D}";
        var handler = new RecordingHandler(_ => JsonResponse(
            UploadResponse(publicId, "jpg", 987654321, 1234, "etag-value")));
        CloudinaryProcessedImageStore store = CreateStore(handler, EnabledOptions());
        await using var content = new MemoryStream(new byte[1234]);

        ObjectStoragePutResult result = await store.PutAsync(
            new ProcessedImageUploadRequest(input.AssetId, variant.VariantId, variant.ContentType, content, content.Length),
            TestContext.Current.CancellationToken);

        Assert.Equal("Cloudinary", result.Provider);
        Assert.Equal("test-cloud", result.Container);
        Assert.Equal(publicId, result.ObjectKey);
        Assert.Equal("etag-value", result.ETag);
        Assert.Equal("987654321", result.VersionId);
        Assert.Equal(1234, result.Bytes);
    }

    [Fact]
    public async Task AuthenticatedDownloadAndDelete_NeverExposeApiSecret()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"result\":\"ok\"}"));
        CloudinaryProcessedImageStore store = CreateStore(handler, EnabledOptions());
        const string publicId = "dorosak/test-env/01911111-1111-7111-8111-111111111111/01922222-2222-7222-8222-222222222222";

        Uri download = await store.CreateDownloadUrlAsync(
            publicId,
            "42",
            "course cover.jpg",
            "image/jpeg",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);
        await store.DeleteAsync(publicId, "42", TestContext.Current.CancellationToken);

        Assert.Equal(Uri.UriSchemeHttps, download.Scheme);
        Assert.Equal("api.cloudinary.com", download.Host);
        Assert.Equal("/v1_1/test-cloud/image/download", download.AbsolutePath);
        Assert.Contains("type=authenticated", download.Query, StringComparison.Ordinal);
        Assert.Contains("expires_at=1800000300", download.Query, StringComparison.Ordinal);
        Assert.Contains("attachment=true", download.Query, StringComparison.Ordinal);
        Assert.Contains("target_filename=course_cover.jpg", download.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("version=42", download.Query, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiSecret, download.OriginalString, StringComparison.Ordinal);
        CapturedRequest delete = Assert.Single(handler.Requests);
        Assert.Equal("/v1_1/test-cloud/image/destroy", delete.Uri.AbsolutePath);
        Assert.Contains("type=authenticated", delete.Body, StringComparison.Ordinal);
        Assert.Contains("public_id=dorosak%2Ftest-env", delete.Body, StringComparison.Ordinal);
        Assert.Contains("signature=", delete.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiSecret, delete.Uri.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiSecret, delete.Body, StringComparison.Ordinal);
    }

    private static CloudinaryProcessedImageStore CreateStore(RecordingHandler handler, CloudinaryOptions options)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cloudinary.com/", UriKind.Absolute),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new CloudinaryProcessedImageStore(
            client,
            Options.Create(options),
            Options.Create(new MediaOptions { Environment = "test-env" }),
            new FixedTimeProvider(Now));
    }

    private static CloudinaryOptions EnabledOptions() => new()
    {
        Enabled = true,
        CloudName = "test-cloud",
        ApiKey = "test-api-key",
        ApiSecret = ApiSecret,
        RequestTimeoutSeconds = 5,
        UploadTimeoutSeconds = 5,
    };

    private static (MediaProcessingInput Input, MediaVariantFile Variant) CreateImageInput()
    {
        Guid assetId = Guid.Parse("01911111-1111-7111-8111-111111111111");
        Guid variantId = Guid.Parse("01922222-2222-7222-8222-222222222222");
        var input = new MediaProcessingInput(
            assetId,
            Guid.NewGuid(),
            null,
            MediaPurpose.ProfileImage,
            "original-user-file-name.png",
            "image/png",
            3,
            new string('a', 64),
            "quarantine/test/original");
        var variant = new MediaVariantFile(
            variantId,
            "image-jpeg-320",
            "unused.jpg",
            "image/jpeg",
            "ready/test/unused.jpg",
            new string('b', 64));
        return (input, variant);
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
    };

    private static string UploadResponse(
        string publicId,
        string format,
        long version,
        long bytes,
        string etag)
    {
        string canonical = $"public_id={publicId}&version={version}";
#pragma warning disable CA5350 // The test independently verifies Cloudinary's response signature.
        string signature = Convert.ToHexStringLower(
            SHA1.HashData(Encoding.UTF8.GetBytes(canonical + ApiSecret)));
#pragma warning restore CA5350
        return $$"""
            {"public_id":"{{publicId}}","resource_type":"image","type":"authenticated","format":"{{format}}","version":{{version}},"bytes":{{bytes}},"etag":"{{etag}}","signature":"{{signature}}"}
            """;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            return responseFactory(request);
        }
    }
}
