namespace Dorosak.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Dorosak";

    public string Audience { get; init; } = "Dorosak.Web";

    public string KeyId { get; init; } = "development";

    public string? PrivateKeyPem { get; init; }
}
