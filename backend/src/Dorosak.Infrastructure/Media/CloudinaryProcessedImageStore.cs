using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Media;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Media:Cloudinary";

    public bool Enabled { get; set; }

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int UploadTimeoutSeconds { get; set; } = 300;
}

internal sealed class CloudinaryProcessedImageStore(
    HttpClient httpClient,
    IOptions<CloudinaryOptions> cloudinaryOptions,
    IOptions<MediaOptions> mediaOptions,
    TimeProvider timeProvider) : IProcessedImageStore
{
    internal const string ProviderName = "Cloudinary";

    private readonly HttpClient _httpClient = httpClient;
    private readonly CloudinaryOptions _options = cloudinaryOptions.Value;
    private readonly string _environment = ToPublicIdSegment(mediaOptions.Value.Environment);
    private readonly TimeProvider _timeProvider = timeProvider;

    public bool CanStore(MediaProcessingInput input, MediaVariantFile variant) =>
        _options.Enabled &&
        input.Purpose is MediaPurpose.ProfileImage or MediaPurpose.CourseImage &&
        variant.Kind.StartsWith("image-", StringComparison.Ordinal) &&
        variant.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public async Task<ObjectStoragePutResult> PutAsync(
        ProcessedImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        if (request.AssetId == Guid.Empty || request.VariantId == Guid.Empty)
        {
            throw new ArgumentException("Processed image identifiers are required.", nameof(request));
        }
        if (!request.Content.CanRead || request.ContentLength <= 0)
        {
            throw new ArgumentException("Processed image content must be readable and non-empty.", nameof(request));
        }

        string format = GetImageFormat(request.ContentType);
        string publicId = CreatePublicId(request.AssetId, request.VariantId);
        string timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["discard_original_filename"] = "true",
            ["overwrite"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
            ["type"] = "authenticated",
        };

        using var form = new MultipartFormDataContent();
        AddFields(form, signedParameters);
        form.Add(new StringContent(_options.ApiKey, Encoding.UTF8), "api_key");
        form.Add(new StringContent(CreateSignature(signedParameters), Encoding.UTF8), "signature");
        var fileContent = new StreamContent(new LeaveOpenReadStream(request.Content));
        fileContent.Headers.ContentLength = request.ContentLength;
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        form.Add(fileContent, "file", $"processed-image.{format}");

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/upload")
        {
            Content = form,
        };
        using CancellationTokenSource timeout = CreateTimeout(_options.UploadTimeoutSeconds, cancellationToken);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new StorageUnavailableException(
                    $"Cloudinary image upload returned HTTP {(int)response.StatusCode}.");
            }

            await using Stream responseContent = await response.Content.ReadAsStreamAsync(timeout.Token);
            CloudinaryUploadResponse? payload = await JsonSerializer.DeserializeAsync<CloudinaryUploadResponse>(
                responseContent,
                cancellationToken: timeout.Token);
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.PublicId) ||
                !string.Equals(payload.PublicId, publicId, StringComparison.Ordinal) ||
                !string.Equals(payload.ResourceType, "image", StringComparison.Ordinal) ||
                !string.Equals(payload.Type, "authenticated", StringComparison.Ordinal) ||
                !string.Equals(payload.Format, format, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(payload.ETag) ||
                string.IsNullOrWhiteSpace(payload.Signature) ||
                payload.Version <= 0 ||
                payload.Bytes != request.ContentLength ||
                !VerifyUploadResponseSignature(payload.PublicId, payload.Version, payload.Signature))
            {
                throw new StorageUnavailableException("Cloudinary returned invalid image upload metadata.");
            }

            return new ObjectStoragePutResult(
                payload.ETag,
                payload.Version.ToString(CultureInfo.InvariantCulture),
                payload.Bytes,
                ProviderName,
                _options.CloudName,
                publicId);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StorageUnavailableException("Cloudinary image upload timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new StorageUnavailableException("Cloudinary image upload was unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new StorageUnavailableException("Cloudinary image upload could not be completed.", exception);
        }
        catch (JsonException exception)
        {
            throw new StorageUnavailableException("Cloudinary returned invalid image upload metadata.", exception);
        }
    }

    public Task<Uri> CreateDownloadUrlAsync(
        string objectKey,
        string? versionId,
        string fileName,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        cancellationToken.ThrowIfCancellationRequested();
        _ = versionId;
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Cloudinary download lifetimes must be positive and no longer than one hour.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var signedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["attachment"] = "true",
            ["expires_at"] = now.Add(lifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["format"] = GetImageFormat(contentType),
            ["public_id"] = objectKey,
            ["target_filename"] = MediaObjectKeys.SafeFileName(fileName),
            ["timestamp"] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["type"] = "authenticated",
        };
        var queryParameters = new SortedDictionary<string, string>(signedParameters, StringComparer.Ordinal)
        {
            ["api_key"] = _options.ApiKey,
            ["signature"] = CreateSignature(signedParameters),
        };
        var builder = new UriBuilder(
            Uri.UriSchemeHttps,
            "api.cloudinary.com",
            -1,
            $"v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/download")
        {
            Query = CreateQueryString(queryParameters),
        };
        return Task.FromResult(builder.Uri);
    }

    public async Task DeleteAsync(string objectKey, string? versionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        _ = versionId;
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        string timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["invalidate"] = "true",
            ["public_id"] = objectKey,
            ["timestamp"] = timestamp,
            ["type"] = "authenticated",
        };
        var requestParameters = new SortedDictionary<string, string>(signedParameters, StringComparer.Ordinal)
        {
            ["api_key"] = _options.ApiKey,
            ["signature"] = CreateSignature(signedParameters),
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/destroy")
        {
            Content = new FormUrlEncodedContent(requestParameters),
        };
        using CancellationTokenSource timeout = CreateTimeout(_options.RequestTimeoutSeconds, cancellationToken);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new StorageUnavailableException(
                    $"Cloudinary image deletion returned HTTP {(int)response.StatusCode}.");
            }

            await using Stream responseContent = await response.Content.ReadAsStreamAsync(timeout.Token);
            CloudinaryDeleteResponse? payload = await JsonSerializer.DeserializeAsync<CloudinaryDeleteResponse>(
                responseContent,
                cancellationToken: timeout.Token);
            if (payload?.Result is not ("ok" or "not found"))
            {
                throw new StorageUnavailableException("Cloudinary returned invalid image deletion metadata.");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StorageUnavailableException("Cloudinary image deletion timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new StorageUnavailableException("Cloudinary image deletion was unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new StorageUnavailableException("Cloudinary image deletion could not be completed.", exception);
        }
        catch (JsonException exception)
        {
            throw new StorageUnavailableException("Cloudinary returned invalid image deletion metadata.", exception);
        }
    }

    private static void AddFields(MultipartFormDataContent content, IEnumerable<KeyValuePair<string, string>> fields)
    {
        foreach ((string name, string value) in fields)
        {
            content.Add(new StringContent(value, Encoding.UTF8), name);
        }
    }

    private string CreatePublicId(Guid assetId, Guid variantId) =>
        $"dorosak/{_environment}/{assetId:D}/{variantId:D}";

    private string CreateSignature(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        string canonical = string.Join(
            '&',
            parameters.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}={parameter.Value}"));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical + _options.ApiSecret));
        return Convert.ToHexStringLower(hash);
    }

    private bool VerifyUploadResponseSignature(string publicId, long version, string signature)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId,
            ["version"] = version.ToString(CultureInfo.InvariantCulture),
        };
        string canonical = string.Join(
            '&',
            parameters.Select(parameter => $"{parameter.Key}={parameter.Value}"));
#pragma warning disable CA5350 // Cloudinary's upload response signature uses SHA-1.
        string expected = Convert.ToHexStringLower(
            SHA1.HashData(Encoding.UTF8.GetBytes(canonical + _options.ApiSecret)));
#pragma warning restore CA5350
        if (signature.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }

    private static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join(
            '&',
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

    private static string GetImageFormat(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/avif" => "avif",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        _ => throw new ArgumentException("The processed image content type is unsupported.", nameof(contentType)),
    };

    private static string ToPublicIdSegment(string value)
    {
        string segment = MediaObjectKeys.SafeFileName(value);
        return segment.Length <= 64 ? segment : segment[..64];
    }

    private static CancellationTokenSource CreateTimeout(int timeoutSeconds, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return timeout;
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new StorageUnavailableException("Cloudinary processed image storage is disabled.");
        }
    }

    private sealed class CloudinaryUploadResponse
    {
        [JsonPropertyName("public_id")]
        public string? PublicId { get; init; }

        [JsonPropertyName("resource_type")]
        public string? ResourceType { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("version")]
        public long Version { get; init; }

        [JsonPropertyName("bytes")]
        public long Bytes { get; init; }

        [JsonPropertyName("etag")]
        public string? ETag { get; init; }

        [JsonPropertyName("signature")]
        public string? Signature { get; init; }
    }

    private sealed class CloudinaryDeleteResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; init; }
    }

    private sealed class LeaveOpenReadStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => base.DisposeAsync();
    }
}
