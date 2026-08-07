using Dorosak.Domain.Authoring;
using Dorosak.Domain.Common;

namespace Dorosak.Domain.UnitTests.Authoring;

public sealed class CourseDraftTests
{
    [Fact]
    public void Advance_RequiresCurrentVersion()
    {
        CourseDraft draft = CourseDraft.Create(Guid.CreateVersion7(), "Beginner", DateTimeOffset.UtcNow);

        draft.Advance(1, DateTimeOffset.UtcNow);

        Assert.Equal(2, draft.Version);
        DomainRuleException exception = Assert.Throws<DomainRuleException>(() =>
            draft.Advance(1, DateTimeOffset.UtcNow));
        Assert.Equal("COURSE.VERSION_CONFLICT", exception.Code);
    }
}
