using Dorosak.Application.Features.Media;

namespace Dorosak.Application.UnitTests.Media;

public sealed class MediaObjectKeysTests
{
    [Fact]
    public void Keys_AreServerStructuredAndAsciiSafe()
    {
        Guid ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid assetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid variantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        Assert.Equal(
            $"quarantine/development/{ownerId:D}/{assetId:D}/original",
            MediaObjectKeys.Quarantine("development", ownerId, assetId));
        Assert.Equal(
            $"ready/development/{assetId:D}/{variantId:D}/course_image.webp",
            MediaObjectKeys.Ready("development", assetId, variantId, "../course image.webp"));
    }

    [Theory]
    [InlineData("../../evil.exe", "evil.exe")]
    [InlineData("درس.pdf", "pdf")]
    [InlineData("..", "file")]
    public void SafeFileName_RemovesPathsAndUnsafeCharacters(string value, string expected)
    {
        Assert.Equal(expected, MediaObjectKeys.SafeFileName(value));
    }
}
