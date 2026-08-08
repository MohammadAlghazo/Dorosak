using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Media;

internal sealed class MediaJobStore(DorosakDbContext dbContext) : IMediaJobStore
{
    public async Task<MediaJobClaim?> TryClaimAsync(
        DateTimeOffset now,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            MediaProcessingJob? job = await dbContext.MediaProcessingJobs
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM media.media_processing_jobs
                    WHERE completed_at IS NULL
                      AND available_at <= {{now}}
                      AND (locked_until IS NULL OR locked_until <= {{now}})
                      AND state IN ('Pending', 'Processing')
                    ORDER BY available_at, created_at, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (job is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            Guid lockToken = Guid.NewGuid();
            if (!job.TryClaim(now, lockDuration, lockToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MediaJobClaim(job.Id, job.AssetId, lockToken, job.AttemptCount);
        });
    }

    public Task CompleteAsync(MediaJobClaim claim, DateTimeOffset now, CancellationToken cancellationToken) =>
        UpdateAsync(claim, job => job.Complete(now, claim.LockToken), cancellationToken);

    public Task RetryAsync(
        MediaJobClaim claim,
        DateTimeOffset now,
        string errorCode,
        TimeSpan delay,
        CancellationToken cancellationToken) =>
        UpdateAsync(claim, job => job.Retry(now, claim.LockToken, errorCode, delay), cancellationToken);

    public Task FailAsync(
        MediaJobClaim claim,
        DateTimeOffset now,
        string errorCode,
        CancellationToken cancellationToken) =>
        UpdateAsync(claim, job => job.Fail(now, claim.LockToken, errorCode), cancellationToken);

    private async Task UpdateAsync(MediaJobClaim claim, Action<MediaProcessingJob> update, CancellationToken cancellationToken)
    {
        MediaProcessingJob job = await dbContext.MediaProcessingJobs.SingleAsync(
            candidate => candidate.Id == claim.JobId && candidate.LockToken == claim.LockToken,
            cancellationToken);
        update(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class MediaProcessingStore(
    DorosakDbContext dbContext,
    IOptions<MediaOptions> options,
    TimeProvider timeProvider) : IMediaProcessingStore
{
    private readonly MediaOptions _options = options.Value;

    public async Task<MediaAssetWorkItem?> GetWorkItemAsync(Guid assetId, CancellationToken cancellationToken)
    {
        MediaAsset? asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == assetId,
            cancellationToken);
        if (asset is null)
        {
            return null;
        }
        MediaVariantResponse[] variants = await dbContext.MediaVariants.AsNoTracking()
            .Where(variant => variant.AssetId == assetId)
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
        CaptionTrack? caption = asset.Purpose == MediaPurpose.Caption
            ? await dbContext.CaptionTracks.AsNoTracking().SingleOrDefaultAsync(
                track => track.SourceMediaAssetId == asset.Id,
                cancellationToken)
            : null;
        return new MediaAssetWorkItem(
            new MediaProcessingInput(
                asset.Id,
                asset.OwnerUserId,
                asset.CourseId,
                asset.Purpose,
                asset.FileName,
                asset.ContentType,
                asset.DeclaredBytes,
                asset.DeclaredSha256,
                asset.QuarantineObjectKey,
                caption?.Id,
                caption?.AssetId,
                caption?.ObjectKey),
            asset.State.ToString(),
            variants);
    }

    public Task MarkScanningAsync(Guid assetId, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, asset => asset.MarkScanning(timeProvider.GetUtcNow()), cancellationToken);

    public Task MarkProcessingAsync(Guid assetId, CancellationToken cancellationToken) =>
        UpdateAssetAsync(
            assetId,
            asset => asset.MarkProcessing(timeProvider.GetUtcNow()),
            "media.scan-clean",
            "succeeded",
            null,
            cancellationToken);

    public Task ResetForRetryAsync(Guid assetId, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, asset => asset.ResumeScanning(timeProvider.GetUtcNow()), cancellationToken);

    public async Task RejectAsync(Guid assetId, string code, CancellationToken cancellationToken)
    {
        string action = code switch
        {
            "MEDIA.MALWARE_DETECTED" => "media.scan-infected",
            "MEDIA.SCANNER_UNAVAILABLE" => "media.scan-failed",
            _ => "media.processing-rejected",
        };
        DateTimeOffset now = timeProvider.GetUtcNow();
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == assetId, cancellationToken);
        asset.Reject(code, now);
        CaptionTrack? caption = await dbContext.CaptionTracks.SingleOrDefaultAsync(
            track => track.SourceMediaAssetId == asset.Id,
            cancellationToken);
        caption?.Reject(code, now);
        dbContext.AuditLogs.Add(AuditLog.Create(asset.OwnerUserId, action, "MediaAsset", asset.Id, "rejected", code, now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkReadyAsync(
        Guid assetId,
        long verifiedBytes,
        string verifiedSha256,
        IReadOnlyList<MediaVariantFile> variants,
        IReadOnlyDictionary<string, ObjectStoragePutResult> uploads,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == assetId, cancellationToken);
        if (asset.State == MediaAssetState.Ready)
        {
            return;
        }

        foreach (MediaVariantFile variant in variants)
        {
            ObjectStoragePutResult upload = uploads[variant.Kind];
            dbContext.MediaVariants.Add(MediaVariant.Create(
                variant.VariantId,
                asset.Id,
                variant.Kind,
                variant.ContentType,
                variant.ObjectKey,
                asset.StorageProvider,
                asset.StorageContainer,
                upload.Bytes,
                variant.Sha256,
                upload.ETag,
                upload.VersionId,
                variant.Width,
                variant.Height,
                variant.DurationSeconds,
                now));
        }

        if (asset.Purpose == MediaPurpose.Caption)
        {
            CaptionTrack caption = await dbContext.CaptionTracks.SingleAsync(
                track => track.SourceMediaAssetId == asset.Id,
                cancellationToken);
            MediaVariantFile variant = variants.Single(item => item.Kind == "caption");
            ObjectStoragePutResult upload = uploads[variant.Kind];
            caption.MarkReady(upload.Bytes, variant.Sha256, upload.ETag, upload.VersionId, now);
        }

        asset.MarkReady(verifiedBytes, verifiedSha256, now);
        dbContext.AuditLogs.Add(AuditLog.Create(
            asset.OwnerUserId,
            "media.processing-ready",
            "MediaAsset",
            asset.Id,
            "succeeded",
            null,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task UpdateAssetAsync(Guid assetId, Action<MediaAsset> update, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, update, null, null, null, cancellationToken);

    private async Task UpdateAssetAsync(
        Guid assetId,
        Action<MediaAsset> update,
        string? auditAction,
        string? auditResult,
        string? auditReason,
        CancellationToken cancellationToken)
    {
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == assetId, cancellationToken);
        update(asset);
        if (auditAction is not null)
        {
            dbContext.AuditLogs.Add(AuditLog.Create(
                asset.OwnerUserId,
                auditAction,
                "MediaAsset",
                asset.Id,
                auditResult!,
                auditReason,
                timeProvider.GetUtcNow()));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
