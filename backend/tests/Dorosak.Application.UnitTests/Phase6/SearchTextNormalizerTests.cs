using Dorosak.Application.Features.Phase6;

namespace Dorosak.Application.UnitTests.Phase6;

public sealed class SearchTextNormalizerTests
{
    [Theory]
    [InlineData("إِدارة الـبَيانات", "ادارة البيانات")]
    [InlineData("آفاق على", "افاق علي")]
    public void ArabicV1_NormalizesWithoutChangingContractVersion(string input, string expected)
    {
        Assert.Equal(expected, SearchTextNormalizer.Normalize(input, "ar"));
        Assert.Equal("ar-v1", SearchTextNormalizer.ArabicVersion);
    }

    [Fact]
    public void English_NormalizationUsesInvariantLowercase()
    {
        Assert.Equal("postgresql basics", SearchTextNormalizer.Normalize(" PostgreSQL Basics ", "en"));
    }
}
