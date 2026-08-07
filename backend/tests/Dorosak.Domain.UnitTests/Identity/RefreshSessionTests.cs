using Dorosak.Domain.Identity;

namespace Dorosak.Domain.UnitTests.Identity;

public sealed class RefreshSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateAndTouch_RespectAbsoluteExpiry()
    {
        RefreshSession session = RefreshSession.Create(
            Guid.CreateVersion7(),
            Now,
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(30),
            "Test browser",
            new string('a', 64),
            "pwd",
            1);

        session.Touch(Now.AddDays(10), TimeSpan.FromDays(14));

        Assert.True(session.IsActive(Now.AddDays(10)));
        Assert.Equal(Now.AddDays(24), session.IdleExpiresAt);
        Assert.Equal(Now.AddDays(30), session.AbsoluteExpiresAt);
    }

    [Fact]
    public void Revoke_IsIdempotentAndDeactivatesSession()
    {
        RefreshSession session = RefreshSession.Create(
            Guid.CreateVersion7(),
            Now,
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(30),
            "Test browser",
            new string('a', 64),
            "pwd otp",
            1);

        session.Revoke(Now.AddMinutes(1), "user-sign-out");
        session.Revoke(Now.AddMinutes(2), "second-reason");

        Assert.False(session.IsActive(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
        Assert.Equal("user-sign-out", session.RevocationReason);
    }
}
