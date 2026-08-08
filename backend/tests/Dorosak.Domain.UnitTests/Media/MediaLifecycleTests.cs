using Dorosak.Domain.Common;
using Dorosak.Domain.Media;

namespace Dorosak.Domain.UnitTests.Media;

public sealed class MediaLifecycleTests
{
    [Fact]
    public void UploadSession_CompleteAndCancelAreIdempotent()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UploadSession completed = CreateSession(now);
        completed.BeginUploading();
        completed.Complete(now.AddMinutes(1));
        completed.Complete(now.AddMinutes(2));
        completed.Cancel(now.AddMinutes(3));

        Assert.Equal(UploadSessionState.Completed, completed.State);
        Assert.Equal(0, completed.ReservedBytes);
        Assert.Equal(now.AddMinutes(1), completed.CompletedAt);

        UploadSession cancelled = CreateSession(now);
        cancelled.Cancel(now.AddMinutes(1));
        cancelled.Cancel(now.AddMinutes(2));
        Assert.Equal(UploadSessionState.Cancelled, cancelled.State);
        Assert.Equal(now.AddMinutes(1), cancelled.CancelledAt);
    }

    [Fact]
    public void MediaAsset_RequiresOrderedReadyLifecycle()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MediaAsset asset = CreateAsset(now);

        Assert.Throws<DomainRuleException>(() => asset.MarkProcessing(now));
        asset.MarkUploaded("etag", "version", now.AddMinutes(1));
        asset.MarkScanning(now.AddMinutes(2));
        asset.MarkProcessing(now.AddMinutes(3));
        asset.MarkReady(10, new string('a', 64), now.AddMinutes(4));

        Assert.Equal(MediaAssetState.Ready, asset.State);
        Assert.Equal(10, asset.VerifiedBytes);
        Assert.Equal(new string('a', 64), asset.VerifiedSha256);
    }

    [Fact]
    public void MediaAsset_RejectionNeverBecomesReady()
    {
        MediaAsset asset = CreateAsset(DateTimeOffset.UtcNow);
        asset.Reject("MEDIA.MALWARE_DETECTED", DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(() => asset.MarkProcessing(DateTimeOffset.UtcNow));
        Assert.Equal(MediaAssetState.Rejected, asset.State);
    }

    private static UploadSession CreateSession(DateTimeOffset now) => UploadSession.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MediaPurpose.CourseDocument, 100, 100,
        "document.pdf", "application/pdf", null, "quarantine/test/key", null, now, now.AddHours(24));

    private static MediaAsset CreateAsset(DateTimeOffset now) => MediaAsset.Create(
        Guid.NewGuid(), Guid.NewGuid(), null, MediaPurpose.CourseDocument, "document.pdf", "application/pdf",
        10, new string('0', 64), "quarantine/test/key", now);
}
