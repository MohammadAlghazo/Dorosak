using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Media;

internal sealed class MediaJobStore(DorosakDbContext dbContext) : IMediaJobStore
{
    public async Task<MediaJobClaim?> TryClaimAsync(
        DateTimeOffset now,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
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
                variant.Width,
                variant.Height,
                variant.DurationSeconds))
            .ToArrayAsync(cancellationToken);
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
                asset.QuarantineObjectKey),
            asset.State.ToString(),
            variants);
    }

    public Task MarkScanningAsync(Guid assetId, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, asset => asset.MarkScanning(timeProvider.GetUtcNow()), cancellationToken);

    public Task MarkProcessingAsync(Guid assetId, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, asset => asset.MarkProcessing(timeProvider.GetUtcNow()), cancellationToken);

    public Task RejectAsync(Guid assetId, string code, CancellationToken cancellationToken) =>
        UpdateAssetAsync(assetId, asset => asset.Reject(code, timeProvider.GetUtcNow()), cancellationToken);

    public async Task MarkReadyAsync(
        Guid assetId,
        long verifiedBytes,
        string verifiedSha256,
        IReadOnlyList<MediaVariantFile> variants,
        IReadOnlyDictionary<string, ObjectStoragePutResult> uploads,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == assetId, cancellationToken);
        if (asset.State == MediaAssetState.Ready)
        {
            await transaction.CommitAsync(cancellationToken);
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
                upload.Bytes,
                upload.ETag,
                upload.VersionId,
                variant.Width,
                variant.Height,
                variant.DurationSeconds,
                now));
        }

        asset.MarkReady(verifiedBytes, verifiedSha256, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpdateAssetAsync(Guid assetId, Action<MediaAsset> update, CancellationToken cancellationToken)
    {
        MediaAsset asset = await dbContext.MediaAssets.SingleAsync(candidate => candidate.Id == assetId, cancellationToken);
        update(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
