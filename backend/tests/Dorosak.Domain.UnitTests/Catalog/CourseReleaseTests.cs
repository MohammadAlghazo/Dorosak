using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;

namespace Dorosak.Domain.UnitTests.Catalog;

public sealed class CourseReleaseTests
{
    [Fact]
    public void Lifecycle_TracksActiveSupersededAndUnpublishedStates()
    {
        CourseRelease release = Create();

        release.Supersede();
        Assert.Equal(CourseReleaseState.Superseded, release.State);

        release.Activate();
        release.Unpublish();
        Assert.Equal(CourseReleaseState.Unpublished, release.State);

        release.Activate();
        Assert.Equal(CourseReleaseState.Active, release.State);
    }

    [Fact]
    public void InvalidLifecycleTransition_IsRejected()
    {
        CourseRelease release = Create();
        release.Supersede();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(release.Supersede);

        Assert.Equal("RELEASE.INVALID_TRANSITION", exception.Code);
    }

    [Fact]
    public void Create_NormalizesManifestHashAndDefaultLocale()
    {
        CourseRelease release = CourseRelease.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            2,
            1,
            "EN",
            new string('A', 64),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow);

        Assert.Equal("en", release.DefaultLocale);
        Assert.Equal(new string('a', 64), release.ManifestHash);
    }

    private static CourseRelease Create() => CourseRelease.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        1,
        1,
        "en",
        new string('a', 64),
        Guid.CreateVersion7(),
        DateTimeOffset.UtcNow);
}
