using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Dorosak.Application.Features.Media;
using Dorosak.Infrastructure.Media;

namespace Dorosak.Application.IntegrationTests.Media;

public sealed class RealMinioObjectStorageTests
{
    [Fact]
    public async Task Multipart_PresignUploadCompleteHeadGetAndAbort_WorkAgainstComposeMinio()
    {
        string? port = null;
        string? accessKey = null;
        string? secretKey = null;
        int parsedPort = 0;
        if (!TryGetComposeValue("DOROSAK_MINIO_API_PORT", out port) ||
            !TryGetComposeValue("DOROSAK_MINIO_ROOT_USER", out accessKey) ||
            !TryGetComposeValue("DOROSAK_MINIO_ROOT_PASSWORD", out secretKey) ||
            !int.TryParse(port, out parsedPort) ||
            !await IsAvailableAsync(parsedPort))
        {
            Assert.Skip("The local Compose MinIO service is unavailable.");
        }
        using var storage = new S3ObjectStorage(new MediaStorageOptions
        {
            Enabled = true,
            Endpoint = $"http://127.0.0.1:{parsedPort}",
            PublicEndpoint = $"http://127.0.0.1:{parsedPort}",
            Bucket = "dorosak-media-integration",
            AccessKey = accessKey!,
            SecretKey = secretKey!,
            ForcePathStyle = true,
            CreateBucketIfMissing = true,
        });
        byte[] bytes = new byte[6 * 1024 * 1024];
        RandomNumberGenerator.Fill(bytes);
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string objectKey = $"integration/{Guid.CreateVersion7():N}/multipart.bin";
        string abortKey = $"integration/{Guid.CreateVersion7():N}/aborted.bin";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ObjectStorageMultipartUpload upload = await storage.CreateMultipartUploadAsync(
                new ObjectStorageUploadRequest(objectKey, "application/octet-stream", Stream.Null, bytes.Length),
                cancellationToken);
            Uri signedUrl = await storage.CreatePartUploadUrlAsync(
                objectKey,
                upload.UploadId,
                1,
                bytes.Length,
                sha256,
                TimeSpan.FromMinutes(5),
                cancellationToken);
            using var client = new HttpClient();
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentLength = bytes.Length;
            content.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", Convert.ToBase64String(Convert.FromHexString(sha256)));
            using HttpResponseMessage response = await client.PutAsync(signedUrl, content, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EntityTagHeaderValue etag = Assert.IsType<EntityTagHeaderValue>(response.Headers.ETag);

            ObjectStorageCompleteResult completed = await storage.CompleteMultipartUploadAsync(
                objectKey,
                upload.UploadId,
                [new ObjectStoragePart(1, etag.Tag)],
                cancellationToken);
            Assert.Equal(bytes.Length, completed.Bytes);
            await using ObjectStorageReadResult read = await storage.OpenReadAsync(objectKey, cancellationToken);
            using var downloaded = new MemoryStream();
            await read.Content.CopyToAsync(downloaded, cancellationToken);
            Assert.Equal(bytes, downloaded.ToArray());
            Assert.Equal(bytes.Length, read.ContentLength);

            ObjectStorageMultipartUpload aborted = await storage.CreateMultipartUploadAsync(
                new ObjectStorageUploadRequest(abortKey, "application/octet-stream", Stream.Null, bytes.Length),
                cancellationToken);
            await storage.AbortMultipartUploadAsync(abortKey, aborted.UploadId, cancellationToken);
            await Assert.ThrowsAsync<StorageUnavailableException>(() => storage.CompleteMultipartUploadAsync(
                abortKey,
                aborted.UploadId,
                [new ObjectStoragePart(1, etag.Tag)],
                cancellationToken));
        }
        finally
        {
            await storage.DeleteObjectAsync(objectKey, CancellationToken.None);
            await storage.DeleteObjectAsync(abortKey, CancellationToken.None);
        }
    }

    private static bool TryGetComposeValue(string name, out string? value)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
        {
            directory = directory.Parent;
        }
        string? environmentPath = directory is null ? null : Path.Combine(directory.FullName, ".env.local");
        if (environmentPath is null || !File.Exists(environmentPath))
        {
            value = null;
            return false;
        }
        string prefix = name + "=";
        string? line = File.ReadLines(environmentPath).FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        value = line?[prefix.Length..];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<bool> IsAvailableAsync(int port)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{port}/minio/health/live");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
