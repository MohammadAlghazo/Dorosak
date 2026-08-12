using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Dorosak.Application.Features.Media;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Media;

internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signingClient;
    private readonly MediaStorageOptions _options;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    public S3ObjectStorage(MediaStorageOptions options)
    {
        _options = options;
        if (!options.Enabled)
        {
            throw new InvalidOperationException("The S3 object storage adapter cannot be created while media storage is disabled.");
        }

        _client = new AmazonS3Client(
            options.AccessKey,
            options.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                UseHttp = options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            });
        _signingClient = string.IsNullOrWhiteSpace(options.PublicEndpoint)
            ? _client
            : new AmazonS3Client(options.AccessKey, options.SecretKey, new AmazonS3Config
            {
                ServiceURL = options.PublicEndpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                UseHttp = options.PublicEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            });
    }

    public string Provider => "S3";

    public async Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureBucketAsync(cancellationToken);
            var response = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = _options.Bucket,
                Key = request.ObjectKey,
                ContentType = request.ContentType,
            }, cancellationToken);
            return new ObjectStorageMultipartUpload(response.UploadId, null);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not create a multipart upload.", exception);
        }
    }

    public async Task<ObjectStoragePutResult> PutObjectAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureBucketAsync(cancellationToken);
            var response = await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = request.ObjectKey,
                InputStream = request.Content,
                ContentType = request.ContentType,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
            }, cancellationToken);
            return new ObjectStoragePutResult(
                response.ETag,
                response.VersionId,
                request.ContentLength,
                Provider,
                _options.Bucket,
                request.ObjectKey);
        }
        catch (UploadContentLengthException)
        {
            throw;
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not store the object.", exception);
        }
    }

    public Task<Uri> CreatePartUploadUrlAsync(
        string objectKey,
        string uploadId,
        int partNumber,
        long contentLength,
        string sha256,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(lifetime),
                UploadId = uploadId,
                PartNumber = partNumber,
            };
            request.Headers.ContentLength = contentLength;
            request.Headers["x-amz-checksum-sha256"] = Convert.ToBase64String(Convert.FromHexString(sha256));
            return Task.FromResult(NormalizeSignedUrl(_signingClient.GetPreSignedURL(request)));
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not sign the upload part.", exception);
        }
    }

    public async Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<ObjectStoragePart> parts,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                UploadId = uploadId,
                PartETags = parts.Select(part => new PartETag(part.PartNumber, part.ETag)).ToList(),
            }, cancellationToken);
            GetObjectMetadataResponse metadata = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                VersionId = response.VersionId,
            }, cancellationToken);
            return new ObjectStorageCompleteResult(response.ETag, response.VersionId, metadata.ContentLength);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not complete the multipart upload.", exception);
        }
    }

    public async Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                UploadId = uploadId,
            }, cancellationToken);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not abort the multipart upload.", exception);
        }
    }

    public async Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            GetObjectResponse response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
            }, cancellationToken);
            return new ObjectStorageReadResult(
                response.ResponseStream,
                response.ETag,
                response.VersionId,
                response.Headers.ContentLength,
                response.Headers.ContentType);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not open the object.", exception);
        }
    }

    public Task<Uri> CreateDownloadUrlAsync(
        string objectKey,
        string fileName,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(lifetime),
                ResponseHeaderOverrides =
                {
                    ContentType = contentType,
                    ContentDisposition = $"attachment; filename=\"{MediaObjectKeys.SafeFileName(fileName)}\"",
                },
            };
            return Task.FromResult(NormalizeSignedUrl(_signingClient.GetPreSignedURL(request)));
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not sign the download.", exception);
        }
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
            }, cancellationToken);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw new StorageUnavailableException("Object storage could not delete the object.", exception);
        }
    }

    public void Dispose()
    {
        if (!ReferenceEquals(_client, _signingClient))
        {
            _signingClient.Dispose();
        }
        _client.Dispose();
        _bucketGate.Dispose();
    }

    private static bool IsStorageException(Exception exception) =>
        exception is AmazonS3Exception or HttpRequestException or IOException or TaskCanceledException;

    private Uri NormalizeSignedUrl(string value)
    {
        var signed = new Uri(value, UriKind.Absolute);
        string endpointValue = string.IsNullOrWhiteSpace(_options.PublicEndpoint) ? _options.Endpoint : _options.PublicEndpoint;
        var endpoint = new Uri(endpointValue, UriKind.Absolute);
        return signed.Scheme == endpoint.Scheme
            ? signed
            : new UriBuilder(signed) { Scheme = endpoint.Scheme, Port = endpoint.Port }.Uri;
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!_options.CreateBucketIfMissing || _bucketReady)
        {
            return;
        }
        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }
            try
            {
                if (!await AmazonS3Util.DoesS3BucketExistV2Async(_client, _options.Bucket))
                {
                    await _client.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
                }
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.Forbidden)
            {
                if (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new StorageUnavailableException("Object storage rejected bucket initialization.", exception);
                }
            }
            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }
}

internal sealed class DisabledObjectStorage : IObjectStorage
{
    public string Provider => "Disabled";

    public Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken) =>
        Fail<ObjectStorageMultipartUpload>();

    public Task<ObjectStoragePutResult> PutObjectAsync(ObjectStorageUploadRequest request, CancellationToken cancellationToken) =>
        Fail<ObjectStoragePutResult>();

    public Task<Uri> CreatePartUploadUrlAsync(string objectKey, string uploadId, int partNumber, long contentLength, string sha256, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Fail<Uri>();

    public Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(string objectKey, string uploadId, IReadOnlyList<ObjectStoragePart> parts, CancellationToken cancellationToken) =>
        Fail<ObjectStorageCompleteResult>();

    public Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken) =>
        Task.FromException(new StorageUnavailableException("Object storage is disabled."));

    public Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
        Fail<ObjectStorageReadResult>();

    public Task<Uri> CreateDownloadUrlAsync(string objectKey, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Fail<Uri>();

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromException(new StorageUnavailableException("Object storage is disabled."));

    private static Task<T> Fail<T>() => Task.FromException<T>(new StorageUnavailableException("Object storage is disabled."));
}
