using System.Security.Cryptography;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Media;

internal sealed class MediaService(
    DorosakDbContext dbContext,
    IObjectStorage objectStorage,
    IOptions<MediaOptions> mediaOptions,
    IOptions<MediaStorageOptions> storageOptions,
    TimeProvider timeProvider) : IMediaService
{
    private readonly MediaOptions _options = mediaOptions.Value;
    private readonly MediaStorageOptions _storageOptions = storageOptions.Value;

    public async Task<Result<UploadSessionResponse>> CreateUploadSessionAsync(
        CreateUploadSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Purpose, ignoreCase: true, out MediaPurpose purpose))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.PURPOSE_UNSUPPORTED", "The media purpose is not supported."));
        }
        if (purpose == MediaPurpose.AssignmentSubmission)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.ASSIGNMENT_SUBMISSIONS_DEFERRED", "Assignment-submission media is not available until Phase 9 ownership is implemented."));
        }

        long limit = GetLimit(purpose);
        if (request.ExpectedBytes <= 0)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.SIZE_INVALID", "The expected size must be positive."));
        }
        if (request.ExpectedBytes > limit)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.QUOTA_FILE_LIMIT", "The file exceeds the limit for this media purpose."));
        }

        if (!IsContentTypeAllowed(purpose, request.ContentType))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CONTENT_TYPE_UNSUPPORTED", "The declared content type is not supported."));
        }

        Guid? courseId = request.CourseId;
        MediaAsset? captionTarget = null;
        if (purpose == MediaPurpose.Caption)
        {
            if (request.CaptionTargetAssetId is not { } captionTargetAssetId ||
                string.IsNullOrWhiteSpace(request.CaptionLocale) ||
                string.IsNullOrWhiteSpace(request.CaptionLabel) ||
                request.CaptionLocale.Length > 16 ||
                request.CaptionLabel.Trim().Length > 120)
            {
                return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                    "MEDIA.CAPTION_ASSOCIATION_REQUIRED", "Captions must be created through a target media asset."));
            }
            captionTarget = await dbContext.MediaAssets.SingleOrDefaultAsync(
                asset => asset.Id == captionTargetAssetId && asset.Purpose == MediaPurpose.SourceVideo && asset.State != MediaAssetState.Deleted,
                cancellationToken);
            if (captionTarget is null || !await CanManageAssetAsync(captionTarget, request.UserId, cancellationToken))
            {
                return Result.Failure<UploadSessionResponse>(NotFound());
            }
            string normalizedLocale = request.CaptionLocale.Trim().ToLowerInvariant();
            if (await dbContext.CaptionTracks.AnyAsync(
                    track => track.AssetId == captionTarget.Id && track.Locale == normalizedLocale,
                    cancellationToken))
            {
                return Result.Failure<UploadSessionResponse>(ResultError.Conflict(
                    "MEDIA.CAPTION_LOCALE_EXISTS", "A caption track already exists for this locale."));
            }
            courseId = captionTarget.CourseId;
        }
        if (purpose is MediaPurpose.CourseImage or MediaPurpose.CourseDocument or MediaPurpose.SourceVideo && courseId is null)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.COURSE_REQUIRED", "Course media must be associated with an editable course."));
        }
        if (purpose == MediaPurpose.ProfileImage && courseId is not null)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.PROFILE_COURSE_FORBIDDEN", "Profile images cannot be associated with a course."));
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"media-quota:{request.UserId:D}"}, 0))",
            cancellationToken);
        if (courseId is { } lockCourseId)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({$"media-course-quota:{lockCourseId:D}"}, 0))",
                cancellationToken);
        }

        if (await dbContext.UploadSessions.CountAsync(session =>
                session.OwnerUserId == request.UserId &&
                (session.State == UploadSessionState.Initiated || session.State == UploadSessionState.Uploading),
                cancellationToken) >= _options.MaxConcurrentSessions)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.RateLimited(
                "MEDIA.CONCURRENT_UPLOADS", "Too many concurrent uploads.", TimeSpan.FromMinutes(1)));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        bool isTeacher = await dbContext.UserRoles
            .Join(dbContext.Roles, link => link.RoleId, role => role.Id, (link, role) => new { link, role })
            .AnyAsync(item => item.link.UserId == request.UserId && item.role.NormalizedName == "TEACHER", cancellationToken);
        long accountQuota = isTeacher ? _options.TeacherQuotaBytes : _options.StudentQuotaBytes;
        long dailyQuota = isTeacher ? _options.TeacherDailyQuotaBytes : _options.StudentDailyQuotaBytes;
        long storedBytes = await dbContext.MediaAssets
            .Where(asset => asset.OwnerUserId == request.UserId &&
                asset.State != MediaAssetState.Rejected && asset.State != MediaAssetState.Deleted)
            .SumAsync(asset => asset.VerifiedBytes ?? asset.DeclaredBytes, cancellationToken);
        long variantBytes = await dbContext.MediaVariants
            .Join(dbContext.MediaAssets, variant => variant.AssetId, asset => asset.Id, (variant, asset) => new { variant, asset })
            .Where(item => item.asset.OwnerUserId == request.UserId && item.asset.State != MediaAssetState.Deleted)
            .SumAsync(item => (long?)item.variant.Bytes, cancellationToken) ?? 0;
        if (storedBytes > accountQuota - request.ExpectedBytes - variantBytes)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.ACCOUNT_QUOTA_EXCEEDED", "The account media quota would be exceeded."));
        }
        DateTimeOffset startOfDay = new(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        long dailyBytes = await dbContext.UploadSessions
            .Where(session => session.OwnerUserId == request.UserId && session.CreatedAt >= startOfDay &&
                session.State != UploadSessionState.Cancelled && session.State != UploadSessionState.Expired)
            .SumAsync(session => (long?)session.ExpectedBytes, cancellationToken) ?? 0;
        if (dailyBytes > dailyQuota - request.ExpectedBytes)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.DAILY_QUOTA_EXCEEDED", "The daily upload quota would be exceeded."));
        }

        if (courseId is { } editableCourseId && !await CanEditCourseAsync(editableCourseId, request.UserId, cancellationToken))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.NotFound(
                "MEDIA.COURSE_NOT_FOUND", "The course was not found or is not editable."));
        }

        if (courseId is { } quotaCourseId)
        {
            long courseBytes = await dbContext.MediaAssets
                .Where(asset => asset.CourseId == quotaCourseId && asset.State != MediaAssetState.Rejected && asset.State != MediaAssetState.Deleted)
                .SumAsync(asset => asset.VerifiedBytes ?? asset.DeclaredBytes, cancellationToken);
            long courseVariantBytes = await dbContext.MediaVariants
                .Join(dbContext.MediaAssets, variant => variant.AssetId, asset => asset.Id, (variant, asset) => new { variant, asset })
                .Where(item => item.asset.CourseId == quotaCourseId && item.asset.State != MediaAssetState.Deleted)
                .SumAsync(item => (long?)item.variant.Bytes, cancellationToken) ?? 0;
            if (courseBytes > _options.CourseQuotaBytes - request.ExpectedBytes - courseVariantBytes)
            {
                return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                    "MEDIA.COURSE_QUOTA_EXCEEDED", "The course media quota would be exceeded."));
            }
        }

        Guid assetId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();
        string objectKey = MediaObjectKeys.Quarantine(_options.Environment, request.UserId, assetId);
        string fileName = MediaObjectKeys.SafeFileName(request.FileName);
        string sha256 = new string('0', 64);
        MediaAsset asset = MediaAsset.Create(
            assetId,
            request.UserId,
            courseId,
            purpose,
            fileName,
            request.ContentType.Trim().ToLowerInvariant(),
            request.ExpectedBytes,
            sha256,
            objectKey,
            objectStorage.Provider,
            _storageOptions.Bucket,
            now);
        UploadSession session = UploadSession.Create(
            sessionId,
            request.UserId,
            assetId,
            purpose,
            request.ExpectedBytes,
            request.ExpectedBytes,
            fileName,
            request.ContentType.Trim().ToLowerInvariant(),
            courseId,
            objectKey,
            null,
            now,
            now.Add(_options.SessionTtl));
        dbContext.MediaAssets.Add(asset);
        dbContext.UploadSessions.Add(session);
        if (captionTarget is not null)
        {
            Guid trackId = Guid.CreateVersion7();
            dbContext.CaptionTracks.Add(CaptionTrack.CreatePending(
                trackId,
                captionTarget.Id,
                asset.Id,
                request.CaptionLocale!.Trim().ToLowerInvariant(),
                request.CaptionLabel!.Trim(),
                MediaObjectKeys.Caption(_options.Environment, captionTarget.Id, trackId),
                objectStorage.Provider,
                _storageOptions.Bucket,
                now));
        }
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.UserId,
            "media.upload-session-issued",
            "UploadSession",
            session.Id,
            "succeeded",
            null,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(session));
    }

    public Task<Result<UploadSessionResponse>> CreateCaptionUploadAsync(
        CreateCaptionUploadCommand request,
        CancellationToken cancellationToken) =>
        CreateUploadSessionAsync(
            new CreateUploadSessionCommand(
                request.UserId,
                MediaPurpose.Caption.ToString(),
                request.ExpectedBytes,
                request.FileName,
                "text/vtt",
                null,
                request.IdempotencyKey,
                request.AssetId,
                request.Locale,
                request.Label),
            cancellationToken);

    public async Task<Result<UploadSessionResponse>> PutUploadContentAsync(
        PutUploadContentCommand request,
        CancellationToken cancellationToken)
    {
        UploadSession? session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UploadSessionId && candidate.OwnerUserId == request.UserId,
            cancellationToken);
        if (session is null)
        {
            return Result.Failure<UploadSessionResponse>(NotFound());
        }
        if (session.ExpectedBytes > _options.MaxStreamBytes)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.MULTIPART_REQUIRED", "This file must use multipart upload."));
        }
        if (session.State == UploadSessionState.Completed)
        {
            return Result.Success(ToResponse(session));
        }
        if (session.State is UploadSessionState.Cancelled or UploadSessionState.Expired)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.Conflict(
                "MEDIA.SESSION_TERMINAL", "The upload session is no longer active."));
        }
        if (request.ContentLength != session.ExpectedBytes)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CONTENT_LENGTH_MISMATCH", "Content-Length must equal the declared size."));
        }
        if (!string.IsNullOrWhiteSpace(request.ContentType) && !string.Equals(
                request.ContentType,
                session.ContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CONTENT_TYPE_MISMATCH", "The content type does not match the upload session."));
        }
        if (!IsSha256(request.Sha256))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["sha256"] = ["A SHA-256 checksum is required."] }));
        }

        session.BeginUploading();
        var hashingStream = new HashingReadStream(request.Content, request.ContentLength);
        ObjectStoragePutResult stored;
        try
        {
            stored = await objectStorage.PutObjectAsync(
                new ObjectStorageUploadRequest(session.QuarantineObjectKey, session.ContentType, hashingStream, session.ExpectedBytes),
                cancellationToken);
            await hashingStream.EnsureExactLengthAsync(cancellationToken);
        }
        catch (UploadContentLengthException)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CONTENT_LENGTH_MISMATCH", "The streamed body did not match the declared Content-Length."));
        }
        catch (StorageUnavailableException)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
        }

        string computedHash = Convert.ToHexStringLower(hashingStream.Hash);
        if (!string.Equals(computedHash, request.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            await objectStorage.DeleteObjectAsync(session.QuarantineObjectKey, cancellationToken);
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CHECKSUM_MISMATCH", "The uploaded bytes did not match the declared checksum."));
        }

        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == session.AssetId, cancellationToken);
        asset.MarkUploaded(stored.ETag, stored.VersionId, timeProvider.GetUtcNow());
        asset.SetDeclaredChecksum(computedHash);
        session.Complete(timeProvider.GetUtcNow());
        dbContext.MediaProcessingJobs.Add(MediaProcessingJob.Create(asset.Id, timeProvider.GetUtcNow()));
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.UserId,
            "media.upload-session-completed",
            "UploadSession",
            session.Id,
            "succeeded",
            null,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(session));
    }

    public async Task<Result<UploadPartResponse>> IssueUploadPartAsync(
        IssueUploadPartCommand request,
        CancellationToken cancellationToken)
    {
        UploadSession? session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UploadSessionId && candidate.OwnerUserId == request.UserId,
            cancellationToken);
        if (session is null)
        {
            return Result.Failure<UploadPartResponse>(NotFound());
        }
        if (session.ExpectedBytes <= _options.MaxStreamBytes)
        {
            return Result.Failure<UploadPartResponse>(ResultError.BusinessRule(
                "MEDIA.STREAM_REQUIRED", "This file does not require multipart upload."));
        }
        if (session.State is UploadSessionState.Completed or UploadSessionState.Cancelled or UploadSessionState.Expired)
        {
            return Result.Failure<UploadPartResponse>(ResultError.Conflict(
                "MEDIA.SESSION_TERMINAL", "The upload session is no longer active."));
        }
        if (request.PartNumber is < 1 or > 10000)
        {
            return Result.Failure<UploadPartResponse>(ResultError.BusinessRule(
                "MEDIA.PART_NUMBER_INVALID", "The part number must be between 1 and 10000."));
        }
        if (request.ExpectedBytes <= 0)
        {
            return Result.Failure<UploadPartResponse>(ResultError.BusinessRule(
                "MEDIA.PART_SIZE_INVALID", "The part size must be positive."));
        }
        long partOffset = (long)(request.PartNumber - 1) * _options.PartSizeBytes;
        if (partOffset >= session.ExpectedBytes || request.ExpectedBytes != Math.Min(_options.PartSizeBytes, session.ExpectedBytes - partOffset))
        {
            return Result.Failure<UploadPartResponse>(ResultError.BusinessRule(
                "MEDIA.PART_SIZE_INVALID", "The part size does not match the server-selected upload geometry."));
        }
        if (request.ExpectedBytes > _options.MaxPartBytes)
        {
            return Result.Failure<UploadPartResponse>(ResultError.BusinessRule(
                "MEDIA.PART_TOO_LARGE", "The part exceeds the configured limit."));
        }
        if (!IsSha256(request.Sha256))
        {
            return Result.Failure<UploadPartResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["sha256"] = ["A SHA-256 checksum is required."] }));
        }
        if (await dbContext.UploadParts.AnyAsync(
                part => part.UploadSessionId == request.UploadSessionId && part.PartNumber == request.PartNumber,
                cancellationToken))
        {
            return Result.Failure<UploadPartResponse>(ResultError.Conflict(
                "MEDIA.DUPLICATE_PART", "The part number has already been issued."));
        }

        if (session.MultipartUploadId is null)
        {
            ObjectStorageMultipartUpload upload;
            try
            {
                upload = await objectStorage.CreateMultipartUploadAsync(
                    new ObjectStorageUploadRequest(session.QuarantineObjectKey, session.ContentType, Stream.Null, session.ExpectedBytes),
                    cancellationToken);
            }
            catch (StorageUnavailableException)
            {
                return Result.Failure<UploadPartResponse>(ResultError.ServiceUnavailable(
                    "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
            }
            session.BeginUploading();
            session.SetMultipartUploadId(upload.UploadId);
        }

        UploadPart part = UploadPart.Create(
            session.Id,
            request.PartNumber,
            request.ExpectedBytes,
            request.Sha256.ToLowerInvariant(),
            timeProvider.GetUtcNow());
        dbContext.UploadParts.Add(part);
        await dbContext.SaveChangesAsync(cancellationToken);
        Uri url;
        try
        {
            url = await objectStorage.CreatePartUploadUrlAsync(
                session.QuarantineObjectKey,
                session.MultipartUploadId!,
                request.PartNumber,
                request.ExpectedBytes,
                request.Sha256,
                TimeSpan.FromMinutes(_storageOptions.UploadUrlMinutes),
                cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            return Result.Failure<UploadPartResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
        }
        return Result.Success(new UploadPartResponse(
            session.Id,
            request.PartNumber,
            request.ExpectedBytes,
            url.ToString(),
            Convert.ToBase64String(Convert.FromHexString(request.Sha256)),
            timeProvider.GetUtcNow().AddMinutes(_storageOptions.UploadUrlMinutes)));
    }

    public async Task<Result<UploadSessionResponse>> CompleteUploadAsync(
        CompleteUploadCommand request,
        CancellationToken cancellationToken)
    {
        UploadSession? session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UploadSessionId && candidate.OwnerUserId == request.UserId,
            cancellationToken);
        if (session is null)
        {
            return Result.Failure<UploadSessionResponse>(NotFound());
        }
        if (session.State == UploadSessionState.Completed)
        {
            return Result.Success(ToResponse(session));
        }
        if (session.State is UploadSessionState.Cancelled or UploadSessionState.Expired)
        {
            return Result.Success(ToResponse(session));
        }
        if (session.MultipartUploadId is null)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.MULTIPART_NOT_STARTED", "No multipart upload has been started."));
        }
        if (!IsSha256(request.Sha256))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["sha256"] = ["A SHA-256 checksum is required."] }));
        }
        if (request.Parts.Count == 0 || request.Parts.Any(part =>
                part.PartNumber is < 1 or > 10000 || part.Size <= 0 || !IsSha256(part.Sha256) || string.IsNullOrWhiteSpace(part.ETag)))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.PARTS_INVALID", "Every completed part must include valid size, checksum, and ETag metadata."));
        }
        if (request.TotalBytes != session.ExpectedBytes || request.Parts.Sum(part => part.Size) != session.ExpectedBytes)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.SIZE_MISMATCH", "The completed parts do not match the declared file size."));
        }
        UploadPart[] parts = await dbContext.UploadParts
            .Where(part => part.UploadSessionId == session.Id)
            .ToArrayAsync(cancellationToken);
        if (parts.Length != request.Parts.Count ||
            request.Parts.Select(part => part.PartNumber).Distinct().Count() != request.Parts.Count ||
            parts.Any(part => request.Parts.All(input => input.PartNumber != part.PartNumber) ||
                request.Parts.Single(input => input.PartNumber == part.PartNumber).Size != part.ExpectedBytes ||
                !string.Equals(request.Parts.Single(input => input.PartNumber == part.PartNumber).Sha256, part.Sha256, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.PARTS_MISMATCH", "The completed parts do not match issued parts."));
        }

        ObjectStorageCompleteResult completed;
        try
        {
            completed = await objectStorage.CompleteMultipartUploadAsync(
                session.QuarantineObjectKey,
                session.MultipartUploadId,
                request.Parts.OrderBy(part => part.PartNumber).Select(part => new ObjectStoragePart(part.PartNumber, part.ETag)).ToArray(),
                cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
        }
        if (completed.Bytes != session.ExpectedBytes)
        {
            await objectStorage.DeleteObjectAsync(session.QuarantineObjectKey, cancellationToken);
            MediaAsset invalidAsset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == session.AssetId, cancellationToken);
            session.Cancel(timeProvider.GetUtcNow());
            invalidAsset.Reject("MEDIA.SIZE_MISMATCH", timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.SIZE_MISMATCH", "Object storage reported a size that does not match the upload session."));
        }

        foreach (UploadPart part in parts)
        {
            UploadPartInput input = request.Parts.Single(candidate => candidate.PartNumber == part.PartNumber);
            part.Complete(input.ETag, completed.VersionId, timeProvider.GetUtcNow());
        }
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == session.AssetId, cancellationToken);
        asset.MarkUploaded(completed.ETag, completed.VersionId, timeProvider.GetUtcNow());
        asset.SetDeclaredChecksum(request.Sha256);
        session.Complete(timeProvider.GetUtcNow());
        dbContext.MediaProcessingJobs.Add(MediaProcessingJob.Create(asset.Id, timeProvider.GetUtcNow()));
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.UserId,
            "media.upload-session-completed",
            "UploadSession",
            session.Id,
            "succeeded",
            null,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(session));
    }

    public async Task<Result<UploadSessionResponse>> CancelUploadAsync(
        CancelUploadCommand request,
        CancellationToken cancellationToken)
    {
        UploadSession? session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UploadSessionId && candidate.OwnerUserId == request.UserId,
            cancellationToken);
        if (session is null)
        {
            return Result.Failure<UploadSessionResponse>(NotFound());
        }
        if (session.State == UploadSessionState.Completed)
        {
            return Result.Success(ToResponse(session));
        }
        if (session.State is UploadSessionState.Cancelled or UploadSessionState.Expired)
        {
            return Result.Success(ToResponse(session));
        }

        if (session.MultipartUploadId is not null)
        {
            try
            {
                await objectStorage.AbortMultipartUploadAsync(session.QuarantineObjectKey, session.MultipartUploadId, cancellationToken);
            }
            catch (StorageUnavailableException)
            {
                return Result.Failure<UploadSessionResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
            }
        }
        try
        {
            await objectStorage.DeleteObjectAsync(session.QuarantineObjectKey, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            return Result.Failure<UploadSessionResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
        }
        session.Cancel(timeProvider.GetUtcNow());
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == session.AssetId, cancellationToken);
        asset.Reject("MEDIA.CANCELLED", timeProvider.GetUtcNow());
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.UserId,
            "media.upload-session-cancelled",
            "UploadSession",
            session.Id,
            "succeeded",
            null,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(session));
    }

    public async Task<Result<MediaStatusResponse>> GetStatusAsync(
        GetMediaStatusQuery request,
        CancellationToken cancellationToken)
    {
        MediaAsset? asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.AssetId,
            cancellationToken);
        if (asset is null)
        {
            return Result.Failure<MediaStatusResponse>(NotFound());
        }
        MediaVariantResponse[] variants = await dbContext.MediaVariants.AsNoTracking()
            .Where(variant => variant.AssetId == asset.Id)
            .OrderBy(variant => variant.Kind)
            .Select(variant => new MediaVariantResponse(
                variant.Id,
                variant.Kind,
                variant.ContentType,
                variant.Bytes,
                variant.Sha256,
                variant.Width,
                variant.Height,
                variant.DurationSeconds))
            .ToArrayAsync(cancellationToken);
        CaptionTrackResponse[] captions = await dbContext.CaptionTracks.AsNoTracking()
            .Where(track => track.AssetId == asset.Id)
            .OrderBy(track => track.Locale)
            .Select(track => new CaptionTrackResponse(
                track.Id,
                track.SourceMediaAssetId,
                track.Locale,
                track.Label,
                track.State.ToString(),
                track.Bytes,
                track.Sha256))
            .ToArrayAsync(cancellationToken);
        return Result.Success(new MediaStatusResponse(
            asset.Id,
            asset.Purpose.ToString(),
            asset.State.ToString(),
            asset.ContentType,
            asset.DeclaredBytes,
            asset.VerifiedBytes,
            asset.RejectionCode,
            variants,
            captions));
    }

    public async Task<Result<DownloadGrantResponse>> CreateDownloadGrantAsync(
        CreateDownloadGrantCommand request,
        CancellationToken cancellationToken)
    {
        MediaAsset? asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.AssetId && candidate.State == MediaAssetState.Ready,
            cancellationToken);
        if (asset is null)
        {
            return Result.Failure<DownloadGrantResponse>(NotFound());
        }
        MediaVariant? variant = request.VariantId is { } variantId
            ? await dbContext.MediaVariants.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == variantId && candidate.AssetId == asset.Id,
                cancellationToken)
            : await dbContext.MediaVariants.AsNoTracking()
                .OrderBy(variant => variant.Kind.StartsWith("video-fmp4-") ? 0 :
                    variant.Kind.StartsWith("image-jpeg-") ? 2 :
                    variant.Kind == "document" || variant.Kind == "caption" ? 3 : 4)
                .ThenByDescending(variant => variant.Height)
                .FirstOrDefaultAsync(candidate => candidate.AssetId == asset.Id, cancellationToken);
        if (variant is null)
        {
            return Result.Failure<DownloadGrantResponse>(NotFound());
        }

        Uri url;
        try
        {
            url = await objectStorage.CreateDownloadUrlAsync(
                variant.ObjectKey,
                request.DownloadFileName ?? asset.FileName,
                variant.ContentType,
                TimeSpan.FromMinutes(_storageOptions.DownloadUrlMinutes),
                cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            return Result.Failure<DownloadGrantResponse>(ResultError.ServiceUnavailable(
                "MEDIA.STORAGE_UNAVAILABLE", "Media storage is temporarily unavailable.", TimeSpan.FromMinutes(1)));
        }
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.UserId,
            "media.download-grant-issued",
            "MediaAsset",
            asset.Id,
            "succeeded",
            null,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new DownloadGrantResponse(
            asset.Id,
            variant.Id,
            url.ToString(),
            timeProvider.GetUtcNow().AddMinutes(_storageOptions.DownloadUrlMinutes),
            MediaObjectKeys.SafeFileName(request.DownloadFileName ?? asset.FileName),
            variant.ContentType));
    }

    private async Task<bool> CanEditCourseAsync(Guid courseId, Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.Courses.AnyAsync(course =>
            course.Id == courseId && course.DeletedAt == null &&
            (course.OwnerUserId == userId || dbContext.CourseInstructors.Any(instructor =>
                instructor.CourseId == courseId && instructor.UserId == userId &&
                (instructor.Role == CourseCollaboratorRole.Editor || instructor.Role == CourseCollaboratorRole.CoInstructor))),
            cancellationToken))
        {
            return true;
        }
        bool canManageAny = await dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.RoleClaims.AsNoTracking(), role => role.RoleId, claim => claim.RoleId, (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId && item.claim.ClaimType == IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == Permissions.MediaManageAny, cancellationToken);
        return canManageAny && await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId && course.DeletedAt == null,
            cancellationToken);
    }

    private async Task<bool> CanManageAssetAsync(MediaAsset asset, Guid userId, CancellationToken cancellationToken)
    {
        if (asset.OwnerUserId == userId)
        {
            return true;
        }
        bool canManageAny = await dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.RoleClaims.AsNoTracking(), role => role.RoleId, claim => claim.RoleId, (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId && item.claim.ClaimType == IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == Permissions.MediaManageAny, cancellationToken);
        if (canManageAny)
        {
            return true;
        }
        return asset.CourseId is { } courseId && await dbContext.CourseInstructors.AsNoTracking().AnyAsync(
            instructor => instructor.CourseId == courseId && instructor.UserId == userId &&
                (instructor.Role == CourseCollaboratorRole.Editor || instructor.Role == CourseCollaboratorRole.CoInstructor),
            cancellationToken);
    }

    private long GetLimit(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.ProfileImage => _options.ProfileImageMaxBytes,
        MediaPurpose.CourseImage => _options.CourseImageMaxBytes,
        MediaPurpose.Caption => _options.CaptionMaxBytes,
        MediaPurpose.CourseDocument => _options.CourseDocumentMaxBytes,
        MediaPurpose.AssignmentSubmission => _options.AssignmentSubmissionMaxBytes,
        MediaPurpose.SourceVideo => _options.SourceVideoMaxBytes,
        _ => 0,
    };

    private static bool IsContentTypeAllowed(MediaPurpose purpose, string contentType) => purpose switch
    {
        MediaPurpose.ProfileImage or MediaPurpose.CourseImage => contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase),
        MediaPurpose.CourseDocument or MediaPurpose.AssignmentSubmission => contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase),
        MediaPurpose.SourceVideo => contentType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("video/quicktime", StringComparison.OrdinalIgnoreCase),
        MediaPurpose.Caption => contentType.Equals("text/vtt", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static bool IsSha256(string value) => value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static ResultError NotFound() => ResultError.NotFound("MEDIA.NOT_FOUND", "The media resource was not found.");

    private UploadSessionResponse ToResponse(UploadSession session) => new(
        session.Id,
        session.AssetId,
        session.State.ToString(),
        session.ExpectedBytes <= _options.MaxStreamBytes ? "Stream" : "Multipart",
        session.ExpectedBytes,
        session.ExpectedBytes <= _options.MaxStreamBytes ? 0 : _options.PartSizeBytes,
        session.ExpiresAt);

}

internal sealed class MediaAccessReader(DorosakDbContext dbContext)
    : IMediaAccessReader
{
    public Task<bool> CanAccessAssetAsync(Guid assetId, Guid userId, CancellationToken cancellationToken) =>
        CanAccessAssetQuery(assetId, userId, cancellationToken);

    public Task<bool> CanAccessUploadSessionAsync(Guid uploadSessionId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.UploadSessions.AsNoTracking().AnyAsync(
            session => session.Id == uploadSessionId && session.OwnerUserId == userId,
            cancellationToken);

    private async Task<bool> CanAccessAssetQuery(Guid assetId, Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.MediaAssets.AsNoTracking().AnyAsync(asset => asset.Id == assetId && asset.OwnerUserId == userId, cancellationToken))
        {
            return true;
        }
        bool hasManagePermission = await dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.RoleClaims.AsNoTracking(), role => role.RoleId, claim => claim.RoleId, (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId && item.claim.ClaimType == Dorosak.Infrastructure.Identity.IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == Permissions.MediaManageAny, cancellationToken);
        if (hasManagePermission)
        {
            return await dbContext.MediaAssets.AsNoTracking().AnyAsync(asset => asset.Id == assetId, cancellationToken);
        }

        return await dbContext.MediaAssets.AsNoTracking().AnyAsync(asset => asset.Id == assetId && asset.CourseId != null &&
            dbContext.CourseInstructors.Any(instructor => instructor.CourseId == asset.CourseId && instructor.UserId == userId), cancellationToken);
    }
}

internal sealed class HashingReadStream(Stream inner, long contentLength) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly long _contentLength = contentLength;
    private long _remaining = contentLength;

    public byte[] Hash => _hash.GetHashAndReset();

    public async Task EnsureExactLengthAsync(CancellationToken cancellationToken)
    {
        if (_remaining != 0)
        {
            throw new UploadContentLengthException();
        }
        byte[] extra = new byte[1];
        if (await inner.ReadAsync(extra, cancellationToken) > 0)
        {
            throw new UploadContentLengthException();
        }
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _contentLength;
    public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        int allowed = (int)Math.Min(buffer.Length, _remaining + 1);
        int count = inner.Read(buffer[..allowed]);
        if (count > _remaining)
        {
            throw new UploadContentLengthException();
        }
        _remaining -= count;
        _hash.AppendData(buffer[..count]);
        return count;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int allowed = (int)Math.Min(buffer.Length, _remaining + 1);
        int count = await inner.ReadAsync(buffer[..allowed], cancellationToken);
        if (count > _remaining)
        {
            throw new UploadContentLengthException();
        }
        _remaining -= count;
        _hash.AppendData(buffer[..count].Span);
        return count;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class UploadContentLengthException : IOException;
