using Dorosak.Domain.Common;

namespace Dorosak.Domain.Media;

public enum MediaPurpose
{
    ProfileImage,
    CourseImage,
    CourseDocument,
    AssignmentSubmission,
    SourceVideo,
}

public enum UploadSessionState
{
    Initiated,
    Uploading,
    Completed,
    Cancelled,
    Expired,
}

public enum MediaAssetState
{
    Initiated,
    Uploaded,
    Scanning,
    Processing,
    Ready,
    Rejected,
    RecoveryPending,
    Deleted,
}

public enum MediaJobState
{
    Pending,
    Processing,
    Completed,
    Failed,
}

public sealed class UploadSession
{
    private UploadSession()
    {
    }

    private UploadSession(
        Guid id,
        Guid ownerUserId,
        Guid assetId,
        MediaPurpose purpose,
        long expectedBytes,
        long reservedBytes,
        string fileName,
        string contentType,
        Guid? courseId,
        string quarantineObjectKey,
        string? multipartUploadId,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        AssetId = assetId;
        Purpose = purpose;
        ExpectedBytes = expectedBytes;
        ReservedBytes = reservedBytes;
        FileName = fileName;
        ContentType = contentType;
        CourseId = courseId;
        QuarantineObjectKey = quarantineObjectKey;
        MultipartUploadId = multipartUploadId;
        State = UploadSessionState.Initiated;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public Guid AssetId { get; private set; }

    public Guid? CourseId { get; private set; }

    public MediaPurpose Purpose { get; private set; }

    public UploadSessionState State { get; private set; }

    public long ExpectedBytes { get; private set; }

    public long ReservedBytes { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string QuarantineObjectKey { get; private set; } = string.Empty;

    public string? MultipartUploadId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public static UploadSession Create(
        Guid id,
        Guid ownerUserId,
        Guid assetId,
        MediaPurpose purpose,
        long expectedBytes,
        long reservedBytes,
        string fileName,
        string contentType,
        Guid? courseId,
        string quarantineObjectKey,
        string? multipartUploadId,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || assetId == Guid.Empty || ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Media identifiers are required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reservedBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(quarantineObjectKey);
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "An upload session must expire in the future.");
        }

        return new UploadSession(
            id,
            ownerUserId,
            assetId,
            purpose,
            expectedBytes,
            reservedBytes,
            fileName,
            contentType,
            courseId,
            quarantineObjectKey,
            multipartUploadId,
            now,
            expiresAt);
    }

    public void BeginUploading()
    {
        if (State == UploadSessionState.Initiated)
        {
            State = UploadSessionState.Uploading;
        }
    }

    public void SetMultipartUploadId(string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        MultipartUploadId ??= uploadId;
    }

    public void Complete(DateTimeOffset now)
    {
        if (State is UploadSessionState.Completed or UploadSessionState.Cancelled or UploadSessionState.Expired)
        {
            return;
        }

        State = UploadSessionState.Completed;
        CompletedAt = now;
        ReservedBytes = 0;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (State is UploadSessionState.Completed or UploadSessionState.Expired)
        {
            return;
        }
        if (State == UploadSessionState.Cancelled)
        {
            return;
        }

        State = UploadSessionState.Cancelled;
        CancelledAt = now;
        ReservedBytes = 0;
    }

    public void Expire(DateTimeOffset now)
    {
        if (State is UploadSessionState.Initiated or UploadSessionState.Uploading)
        {
            State = UploadSessionState.Expired;
            CancelledAt = now;
            ReservedBytes = 0;
        }
    }
}

public sealed class UploadPart
{
    private UploadPart()
    {
    }

    private UploadPart(Guid id, Guid uploadSessionId, int partNumber, long expectedBytes, string sha256, DateTimeOffset now)
    {
        Id = id;
        UploadSessionId = uploadSessionId;
        PartNumber = partNumber;
        ExpectedBytes = expectedBytes;
        Sha256 = sha256;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UploadSessionId { get; private set; }

    public int PartNumber { get; private set; }

    public long ExpectedBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public string? ETag { get; private set; }

    public string? VersionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static UploadPart Create(Guid uploadSessionId, int partNumber, long expectedBytes, string sha256, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), uploadSessionId, partNumber, expectedBytes, sha256, now);

    public void Complete(string etag, string? versionId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);
        if (ETag is not null && !string.Equals(ETag, etag, StringComparison.Ordinal))
        {
            throw new DomainRuleException("MEDIA.PART_ETAG_CHANGED", "A completed part cannot change its ETag.");
        }

        ETag = etag;
        VersionId = versionId;
        CompletedAt = now;
    }
}

public sealed class MediaAsset
{
    private MediaAsset()
    {
    }

    private MediaAsset(
        Guid id,
        Guid ownerUserId,
        Guid? courseId,
        MediaPurpose purpose,
        string fileName,
        string contentType,
        long declaredBytes,
        string declaredSha256,
        string quarantineObjectKey,
        DateTimeOffset now)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        CourseId = courseId;
        Purpose = purpose;
        FileName = fileName;
        ContentType = contentType;
        DeclaredBytes = declaredBytes;
        DeclaredSha256 = declaredSha256;
        QuarantineObjectKey = quarantineObjectKey;
        State = MediaAssetState.Initiated;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public Guid? CourseId { get; private set; }

    public MediaPurpose Purpose { get; private set; }

    public MediaAssetState State { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long DeclaredBytes { get; private set; }

    public long? VerifiedBytes { get; private set; }

    public string DeclaredSha256 { get; private set; } = string.Empty;

    public string? VerifiedSha256 { get; private set; }

    public string QuarantineObjectKey { get; private set; } = string.Empty;

    public string? QuarantineETag { get; private set; }

    public string? QuarantineVersionId { get; private set; }

    public string? RejectionCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReadyAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static MediaAsset Create(
        Guid id,
        Guid ownerUserId,
        Guid? courseId,
        MediaPurpose purpose,
        string fileName,
        string contentType,
        long declaredBytes,
        string declaredSha256,
        string quarantineObjectKey,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(declaredBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredSha256);
        return new MediaAsset(
            id,
            ownerUserId,
            courseId,
            purpose,
            fileName,
            contentType,
            declaredBytes,
            declaredSha256,
            quarantineObjectKey,
            now);
    }

    public void MarkUploaded(string etag, string? versionId, DateTimeOffset now)
    {
        if (State is MediaAssetState.Uploaded or MediaAssetState.Scanning or MediaAssetState.Processing or MediaAssetState.Ready)
        {
            return;
        }
        if (State is MediaAssetState.Rejected or MediaAssetState.Deleted)
        {
            throw new DomainRuleException("MEDIA.ASSET_TERMINAL", "The media asset is terminal.");
        }

        State = MediaAssetState.Uploaded;
        QuarantineETag = etag;
        QuarantineVersionId = versionId;
        UpdatedAt = now;
    }

    public void SetDeclaredChecksum(string sha256)
    {
        if (sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 checksum is required.", nameof(sha256));
        }
        DeclaredSha256 = sha256.ToLowerInvariant();
    }

    public void MarkScanning(DateTimeOffset now)
    {
        if (State == MediaAssetState.Scanning)
        {
            return;
        }
        if (State != MediaAssetState.Uploaded)
        {
            throw new DomainRuleException("MEDIA.INVALID_SCAN_TRANSITION", "The media asset is not ready for scanning.");
        }
        State = MediaAssetState.Scanning;
        UpdatedAt = now;
    }

    public void MarkProcessing(DateTimeOffset now)
    {
        if (State == MediaAssetState.Processing)
        {
            return;
        }
        if (State != MediaAssetState.Scanning)
        {
            throw new DomainRuleException("MEDIA.INVALID_PROCESSING_TRANSITION", "The media asset is not ready for processing.");
        }
        State = MediaAssetState.Processing;
        UpdatedAt = now;
    }

    public void MarkReady(long verifiedBytes, string verifiedSha256, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(verifiedBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedSha256);
        if (State == MediaAssetState.Ready)
        {
            return;
        }
        if (State != MediaAssetState.Processing)
        {
            throw new DomainRuleException("MEDIA.INVALID_READY_TRANSITION", "The media asset is not processing.");
        }

        State = MediaAssetState.Ready;
        VerifiedBytes = verifiedBytes;
        VerifiedSha256 = verifiedSha256;
        ReadyAt = now;
        UpdatedAt = now;
    }

    public void Reject(string code, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (State == MediaAssetState.Ready || State == MediaAssetState.Deleted)
        {
            return;
        }
        State = MediaAssetState.Rejected;
        RejectionCode = code.Length <= 100 ? code : code[..100];
        UpdatedAt = now;
    }

    public void MarkRecoveryPending(DateTimeOffset now)
    {
        if (State is MediaAssetState.Ready or MediaAssetState.Rejected or MediaAssetState.Deleted)
        {
            return;
        }
        State = MediaAssetState.RecoveryPending;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now)
    {
        if (State == MediaAssetState.Ready)
        {
            throw new DomainRuleException("MEDIA.REFERENCED_ASSET", "A ready asset cannot be deleted by cleanup.");
        }
        State = MediaAssetState.Deleted;
        DeletedAt = now;
        UpdatedAt = now;
    }
}

public sealed class MediaVariant
{
    private MediaVariant()
    {
    }

    private MediaVariant(
        Guid id,
        Guid assetId,
        string kind,
        string contentType,
        string objectKey,
        long bytes,
        string etag,
        string? versionId,
        int? width,
        int? height,
        decimal? durationSeconds,
        DateTimeOffset now)
    {
        Id = id;
        AssetId = assetId;
        Kind = kind;
        ContentType = contentType;
        ObjectKey = objectKey;
        Bytes = bytes;
        ETag = etag;
        VersionId = versionId;
        Width = width;
        Height = height;
        DurationSeconds = durationSeconds;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid AssetId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public long Bytes { get; private set; }

    public string ETag { get; private set; } = string.Empty;

    public string? VersionId { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public decimal? DurationSeconds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaVariant Create(
        Guid id,
        Guid assetId,
        string kind,
        string contentType,
        string objectKey,
        long bytes,
        string etag,
        string? versionId,
        int? width,
        int? height,
        decimal? durationSeconds,
        DateTimeOffset now) =>
        new(id, assetId, kind, contentType, objectKey, bytes, etag, versionId, width, height, durationSeconds, now);
}

public sealed class CaptionTrack
{
    private CaptionTrack()
    {
    }

    private CaptionTrack(
        Guid id,
        Guid assetId,
        string locale,
        string label,
        string objectKey,
        long bytes,
        string etag,
        DateTimeOffset now)
    {
        Id = id;
        AssetId = assetId;
        Locale = locale;
        Label = label;
        ObjectKey = objectKey;
        Bytes = bytes;
        ETag = etag;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid AssetId { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public long Bytes { get; private set; }

    public string ETag { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static CaptionTrack Create(
        Guid assetId,
        string locale,
        string label,
        string objectKey,
        long bytes,
        string etag,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(), assetId, locale, label, objectKey, bytes, etag, now);
}

public sealed class MediaProcessingJob
{
    private MediaProcessingJob()
    {
    }

    private MediaProcessingJob(Guid id, Guid assetId, DateTimeOffset now)
    {
        Id = id;
        AssetId = assetId;
        State = MediaJobState.Pending;
        AvailableAt = now;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid AssetId { get; private set; }

    public MediaJobState State { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public Guid? LockToken { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaProcessingJob Create(Guid assetId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), assetId, now);

    public bool TryClaim(DateTimeOffset now, TimeSpan lockDuration, Guid lockToken)
    {
        if (State is MediaJobState.Completed or MediaJobState.Failed || AvailableAt > now || LockedUntil > now)
        {
            return false;
        }

        State = MediaJobState.Processing;
        AttemptCount++;
        LockedUntil = now.Add(lockDuration);
        LockToken = lockToken;
        return true;
    }

    public void Complete(DateTimeOffset now, Guid lockToken)
    {
        EnsureLock(lockToken);
        State = MediaJobState.Completed;
        CompletedAt = now;
        LockedUntil = null;
        LockToken = null;
        LastErrorCode = null;
    }

    public void Retry(DateTimeOffset now, Guid lockToken, string errorCode, TimeSpan delay)
    {
        EnsureLock(lockToken);
        State = MediaJobState.Pending;
        AvailableAt = now.Add(delay);
        LastErrorCode = errorCode.Length <= 200 ? errorCode : errorCode[..200];
        LockedUntil = null;
        LockToken = null;
    }

    public void Fail(DateTimeOffset now, Guid lockToken, string errorCode)
    {
        EnsureLock(lockToken);
        State = MediaJobState.Failed;
        CompletedAt = now;
        LastErrorCode = errorCode.Length <= 200 ? errorCode : errorCode[..200];
        LockedUntil = null;
        LockToken = null;
    }

    private void EnsureLock(Guid lockToken)
    {
        if (LockToken != lockToken)
        {
            throw new InvalidOperationException("The media job is not owned by this worker lock.");
        }
    }
}
