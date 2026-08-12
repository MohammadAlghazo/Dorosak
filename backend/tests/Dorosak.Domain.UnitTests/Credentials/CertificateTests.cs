using Dorosak.Domain.Credentials;

namespace Dorosak.Domain.UnitTests.Credentials;

public sealed class CertificateTests
{
    [Fact]
    public void CertificateCanBeRevokedOnlyOnceWithoutChangingIssueData()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Certificate certificate = Certificate.Issue(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Demo Learner",
            "Demo Course",
            "en",
            now.AddDays(-1),
            "verification_code_123",
            now);
        Guid actorId = Guid.CreateVersion7();

        certificate.Revoke(actorId, "Portfolio demo revocation", now.AddMinutes(1));
        certificate.Revoke(actorId, "Portfolio demo revocation", now.AddMinutes(2));

        Assert.Equal(CertificateStatus.Revoked, certificate.Status);
        Assert.Equal("Demo Learner", certificate.LearnerName);
        Assert.Equal("Demo Course", certificate.CourseTitle);
        Assert.Equal("Portfolio demo revocation", certificate.RevocationReason);
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    public void CertificateAcceptsSupportedLocales(string locale)
    {
        Certificate certificate = Certificate.Issue(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Demo Learner",
            "Demo Course",
            locale,
            DateTimeOffset.UtcNow,
            "verification_code_123",
            DateTimeOffset.UtcNow);

        Assert.Equal(locale, certificate.Locale);
    }
}
