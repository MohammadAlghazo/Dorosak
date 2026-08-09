using System.Globalization;
using Dorosak.Application.Common.Authorization;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using Dorosak.Domain.Media;

namespace Dorosak.Application.Features.Media;

public sealed record CreateUploadSessionCommand(
    Guid UserId,
    string Purpose,
    long ExpectedBytes,
    string FileName,
    string ContentType,
    Guid? CourseId,
    string IdempotencyKey,
    Guid? CaptionTargetAssetId = null,
    string? CaptionLocale = null,
    string? CaptionLabel = null,
    Guid? EnrollmentId = null,
    Guid? AssignmentVersionId = null,
    Guid? AssignmentSubmissionId = null,
    Guid? ClientFileId = null) : IIdempotentCommand<UploadSessionResponse>
{
    public string IdempotencyOperation => "media.upload-session.create";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new
    {
        Purpose,
        ExpectedBytes,
        FileName,
        ContentType,
        CourseId,
        CaptionTargetAssetId,
        CaptionLocale,
        CaptionLabel,
        EnrollmentId,
        AssignmentVersionId,
        AssignmentSubmissionId,
        ClientFileId,
    };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(25);
}

public sealed record CreateCaptionUploadCommand(
    Guid UserId,
    Guid AssetId,
    string Locale,
    string Label,
    long ExpectedBytes,
    string FileName,
    string IdempotencyKey) : IIdempotentCommand<UploadSessionResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => AssetId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.Asset;

    public string IdempotencyOperation => "media.caption-upload.create";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { AssetId, Locale, Label, ExpectedBytes, FileName };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(25);
}

public sealed record PutUploadContentCommand(
    Guid UserId,
    Guid UploadSessionId,
    Stream Content,
    long ContentLength,
    string? ContentType,
    string Sha256) : ICommand<UploadSessionResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => UploadSessionId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.UploadSession;
}

public sealed record IssueUploadPartCommand(
    Guid UserId,
    Guid UploadSessionId,
    int PartNumber,
    long ExpectedBytes,
    string Sha256) : ITransactionalCommand<UploadPartResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => UploadSessionId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.UploadSession;
}

public sealed record UploadPartInput(int PartNumber, long Size, string Sha256, string ETag);

public sealed record CompleteUploadCommand(
    Guid UserId,
    Guid UploadSessionId,
    long TotalBytes,
    string Sha256,
    IReadOnlyList<UploadPartInput> Parts,
    string IdempotencyKey) : IIdempotentCommand<UploadSessionResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => UploadSessionId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.UploadSession;

    public string IdempotencyOperation => "media.upload-session.complete";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { UploadSessionId, TotalBytes, Sha256, Parts };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(25);
}

public sealed record CancelUploadCommand(
    Guid UserId,
    Guid UploadSessionId,
    string IdempotencyKey) : IIdempotentCommand<UploadSessionResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => UploadSessionId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.UploadSession;

    public string IdempotencyOperation => "media.upload-session.cancel";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { UploadSessionId };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(25);
}

public sealed record GetMediaStatusQuery(Guid UserId, Guid AssetId) : IQuery<MediaStatusResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => AssetId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.Asset;
}

public sealed record CreateDownloadGrantCommand(
    Guid UserId,
    Guid AssetId,
    Guid? VariantId,
    string? DownloadFileName) : ICommand<DownloadGrantResponse>, IMediaAuthorizedRequest
{
    Guid IMediaAuthorizedRequest.UserId => UserId;

    Guid IMediaAuthorizedRequest.MediaId => AssetId;

    MediaAuthorizationTarget IMediaAuthorizedRequest.Target => MediaAuthorizationTarget.Asset;
}

public sealed record UploadSessionResponse(
    Guid UploadSessionId,
    Guid AssetId,
    string State,
    string Mode,
    long ExpectedBytes,
    long PartSize,
    DateTimeOffset ExpiresAt);

public sealed record UploadPartResponse(
    Guid UploadSessionId,
    int PartNumber,
    long ExpectedBytes,
    string UploadUrl,
    string RequiredChecksumSha256,
    DateTimeOffset UrlExpiresAt);

public sealed record MediaVariantResponse(
    Guid Id,
    string Kind,
    string ContentType,
    long Bytes,
    string Sha256,
    int? Width,
    int? Height,
    decimal? DurationSeconds);

public sealed record CaptionTrackResponse(
    Guid Id,
    Guid SourceMediaAssetId,
    string Locale,
    string Label,
    string State,
    long? Bytes,
    string? Sha256);

public sealed record MediaStatusResponse(
    Guid AssetId,
    string Purpose,
    string State,
    string ContentType,
    long DeclaredBytes,
    long? VerifiedBytes,
    string? RejectionCode,
    IReadOnlyList<MediaVariantResponse> Variants,
    IReadOnlyList<CaptionTrackResponse> Captions);

public sealed record DownloadGrantResponse(
    Guid AssetId,
    Guid VariantId,
    string Url,
    DateTimeOffset ExpiresAt,
    string FileName,
    string ContentType);

public sealed record MediaProcessingInput(
    Guid AssetId,
    Guid OwnerUserId,
    Guid? CourseId,
    MediaPurpose Purpose,
    string FileName,
    string ContentType,
    long DeclaredBytes,
    string DeclaredSha256,
    string QuarantineObjectKey,
    Guid? CaptionTrackId = null,
    Guid? CaptionTargetAssetId = null,
    string? CaptionObjectKey = null);

public sealed record MediaVariantFile(
    Guid VariantId,
    string Kind,
    string FilePath,
    string ContentType,
    string ObjectKey,
    string Sha256,
    int? Width = null,
    int? Height = null,
    decimal? DurationSeconds = null,
    string? FileName = null);

public sealed record MediaProcessingResult(IReadOnlyList<MediaVariantFile> Variants);

public sealed record MediaJobClaim(Guid JobId, Guid AssetId, Guid LockToken, int AttemptCount);

public sealed record MediaAssetWorkItem(
    MediaProcessingInput Input,
    string? ExistingState,
    IReadOnlyList<MediaVariantResponse> ExistingVariants);

public enum MediaAuthorizationTarget
{
    Asset,
    UploadSession,
}

public interface IMediaAuthorizedRequest : IAuthorizedRequest
{
    Guid UserId { get; }

    Guid MediaId { get; }

    MediaAuthorizationTarget Target { get; }
}

public interface IMediaService
{
    Task<Result<UploadSessionResponse>> CreateUploadSessionAsync(CreateUploadSessionCommand request, CancellationToken cancellationToken);

    Task<Result<UploadSessionResponse>> CreateCaptionUploadAsync(CreateCaptionUploadCommand request, CancellationToken cancellationToken);

    Task<Result<UploadSessionResponse>> PutUploadContentAsync(PutUploadContentCommand request, CancellationToken cancellationToken);

    Task<Result<UploadPartResponse>> IssueUploadPartAsync(IssueUploadPartCommand request, CancellationToken cancellationToken);

    Task<Result<UploadSessionResponse>> CompleteUploadAsync(CompleteUploadCommand request, CancellationToken cancellationToken);

    Task<Result<UploadSessionResponse>> CancelUploadAsync(CancelUploadCommand request, CancellationToken cancellationToken);

    Task<Result<MediaStatusResponse>> GetStatusAsync(GetMediaStatusQuery request, CancellationToken cancellationToken);

    Task<Result<DownloadGrantResponse>> CreateDownloadGrantAsync(CreateDownloadGrantCommand request, CancellationToken cancellationToken);
}

public interface IMediaAccessReader
{
    Task<bool> CanAccessAssetAsync(Guid assetId, Guid userId, CancellationToken cancellationToken);

    Task<bool> CanAccessUploadSessionAsync(Guid uploadSessionId, Guid userId, CancellationToken cancellationToken);
}

public interface IObjectStorage
{
    string Provider { get; }

    Task<ObjectStorageMultipartUpload> CreateMultipartUploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken);

    Task<ObjectStoragePutResult> PutObjectAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken);

    Task<Uri> CreatePartUploadUrlAsync(
        string objectKey,
        string uploadId,
        int partNumber,
        long contentLength,
        string sha256,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<ObjectStorageCompleteResult> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<ObjectStoragePart> parts,
        CancellationToken cancellationToken);

    Task AbortMultipartUploadAsync(string objectKey, string uploadId, CancellationToken cancellationToken);

    Task<ObjectStorageReadResult> OpenReadAsync(string objectKey, CancellationToken cancellationToken);

    Task<Uri> CreateDownloadUrlAsync(string objectKey, string fileName, string contentType, TimeSpan lifetime, CancellationToken cancellationToken);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class StorageUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed record ObjectStorageUploadRequest(
    string ObjectKey,
    string ContentType,
    Stream Content,
    long ContentLength);

public sealed record ObjectStorageMultipartUpload(string UploadId, string? VersionId);

public sealed record ObjectStoragePutResult(string ETag, string? VersionId, long Bytes, string Provider = "", string Container = "");

public sealed record ObjectStoragePart(int PartNumber, string ETag);

public sealed record ObjectStorageCompleteResult(string ETag, string? VersionId, long Bytes);

public sealed record ObjectStorageReadResult(Stream Content, string? ETag, string? VersionId, long? ContentLength, string? ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public enum MalwareScanStatus
{
    Clean,
    Infected,
    Unavailable,
}

public sealed record MalwareScanResult(MalwareScanStatus Status, string? Signature = null);

public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken);
}

public sealed record MediaValidationResult(bool IsValid, string? Code, string? DetectedType);

public interface IMediaContentValidator
{
    Task<MediaValidationResult> ValidateAsync(
        string filePath,
        MediaPurpose purpose,
        string declaredContentType,
        string fileName,
        CancellationToken cancellationToken);
}

public interface IMediaProcessor
{
    Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingInput input,
        string sourceFilePath,
        string outputDirectory,
        CancellationToken cancellationToken);
}

public interface IMediaJobStore
{
    Task<MediaJobClaim?> TryClaimAsync(DateTimeOffset now, TimeSpan lockDuration, CancellationToken cancellationToken);

    Task CompleteAsync(MediaJobClaim claim, DateTimeOffset now, CancellationToken cancellationToken);

    Task RetryAsync(MediaJobClaim claim, DateTimeOffset now, string errorCode, TimeSpan delay, CancellationToken cancellationToken);

    Task FailAsync(MediaJobClaim claim, DateTimeOffset now, string errorCode, CancellationToken cancellationToken);
}

public interface IMediaProcessingStore
{
    Task<MediaAssetWorkItem?> GetWorkItemAsync(Guid assetId, CancellationToken cancellationToken);

    Task MarkScanningAsync(Guid assetId, CancellationToken cancellationToken);

    Task MarkProcessingAsync(Guid assetId, CancellationToken cancellationToken);

    Task ResetForRetryAsync(Guid assetId, CancellationToken cancellationToken);

    Task RejectAsync(Guid assetId, string code, CancellationToken cancellationToken);

    Task MarkReadyAsync(
        Guid assetId,
        long verifiedBytes,
        string verifiedSha256,
        IReadOnlyList<MediaVariantFile> variants,
        IReadOnlyDictionary<string, ObjectStoragePutResult> uploads,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public static class MediaObjectKeys
{
    public static string Quarantine(string environment, Guid ownerUserId, Guid assetId) =>
        $"quarantine/{AsciiSegment(environment)}/{ownerUserId:D}/{assetId:D}/original";

    public static string Ready(string environment, Guid assetId, Guid variantId, string fileName) =>
        $"ready/{AsciiSegment(environment)}/{assetId:D}/{variantId:D}/{SafeFileName(fileName)}";

    public static string Caption(string environment, Guid assetId, Guid captionId) =>
        $"captions/{AsciiSegment(environment)}/{assetId:D}/{captionId:D}.vtt";

    public static string SafeFileName(string fileName)
    {
        string candidate = Path.GetFileName(fileName.Trim());
        if (candidate.Length == 0)
        {
            return "file";
        }

        Span<char> buffer = stackalloc char[Math.Min(candidate.Length, 180)];
        int length = 0;
        foreach (char character in candidate)
        {
            if (length == buffer.Length)
            {
                break;
            }

            buffer[length++] = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_'
                ? character
                : '_';
        }

        string result = new string(buffer[..length]).Trim('.', '_', '-');
        return result.Length == 0 ? "file" : result;
    }

    private static string AsciiSegment(string value)
    {
        string segment = SafeFileName(value);
        return segment.Length > 64 ? segment[..64] : segment;
    }
}

public sealed class MediaStorageOptions
{
    public const string SectionName = "Media:Storage";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = string.Empty;

    public string? PublicEndpoint { get; set; }

    public string Bucket { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; } = true;

    public int UploadUrlMinutes { get; set; } = 10;

    public int DownloadUrlMinutes { get; set; } = 5;

    public bool CreateBucketIfMissing { get; set; }
}

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string Environment { get; set; } = "development";

    public long MaxStreamBytes { get; set; } = 32L * 1024 * 1024;

    public long MultipartMinimumBytes { get; set; } = 8L * 1024 * 1024;

    public long PartSizeBytes { get; set; } = 16L * 1024 * 1024;

    public long MaxPartBytes { get; set; } = 64L * 1024 * 1024;

    public long ProfileImageMaxBytes { get; set; } = 10L * 1024 * 1024;

    public long CourseImageMaxBytes { get; set; } = 20L * 1024 * 1024;

    public long CaptionMaxBytes { get; set; } = 10L * 1024 * 1024;

    public long CourseDocumentMaxBytes { get; set; } = 100L * 1024 * 1024;

    public long AssignmentSubmissionMaxBytes { get; set; } = 250L * 1024 * 1024;

    public int AssignmentSubmissionMaxFiles { get; set; } = 5;

    public long SourceVideoMaxBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(24);

    public long TeacherQuotaBytes { get; set; } = 500L * 1024 * 1024 * 1024;

    public long CourseQuotaBytes { get; set; } = 200L * 1024 * 1024 * 1024;

    public long StudentQuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    public long TeacherDailyQuotaBytes { get; set; } = 100L * 1024 * 1024 * 1024;

    public long StudentDailyQuotaBytes { get; set; } = 20L * 1024 * 1024 * 1024;

    public int MaxConcurrentSessions { get; set; } = 3;

    public int WorkerMaxAttempts { get; set; } = 5;

    public int WorkerConcurrency { get; set; } = 2;

    public TimeSpan WorkerLockDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan OrphanGracePeriod { get; set; } = TimeSpan.FromHours(24);

    public long PdfParserMaxBytes { get; set; } = 250L * 1024 * 1024;

    public int PdfParserMaxPages { get; set; } = 2000;

    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromMinutes(15);

    public int ProcessOutputCharacterLimit { get; set; } = 65536;
}
