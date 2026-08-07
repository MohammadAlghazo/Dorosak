using Dorosak.Domain.Identity;

namespace Dorosak.Domain.UnitTests.Identity;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Consume_TracksReplacementAndRaceWindow()
    {
        RefreshToken token = RefreshToken.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new string('a', 64),
            Now,
            Now.AddDays(30));
        Guid replacementId = Guid.CreateVersion7();

        token.Consume(Now.AddSeconds(1), replacementId);

        Assert.False(token.IsActive(Now.AddSeconds(2)));
        Assert.Equal(replacementId, token.ReplacedByTokenId);
        Assert.True(token.WasConsumedRecently(Now.AddSeconds(10), TimeSpan.FromSeconds(10)));
        Assert.False(token.WasConsumedRecently(Now.AddSeconds(12), TimeSpan.FromSeconds(10)));
        Assert.Throws<InvalidOperationException>(() => token.Consume(Now.AddSeconds(3), Guid.CreateVersion7()));
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        RefreshToken token = RefreshToken.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new string('b', 64),
            Now,
            Now.AddDays(30));

        token.Revoke(Now.AddMinutes(1), "session-revoked");
        token.Revoke(Now.AddMinutes(2), "second-reason");

        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
        Assert.Equal("session-revoked", token.RevocationReason);
    }
}
