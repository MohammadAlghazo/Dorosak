using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dorosak.Infrastructure.Identity;

internal sealed class JwtTokenIssuer(
    IJwtKeyProvider keyProvider,
    IOptions<JwtOptions> jwtOptions,
    IOptions<IdentitySecurityOptions> securityOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly IdentitySecurityOptions _securityOptions = securityOptions.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(
        Guid userId,
        Guid sessionId,
        DateTimeOffset authenticatedAt,
        int authorizationVersion,
        IReadOnlyList<string> authenticationMethods,
        DateTimeOffset now)
    {
        DateTimeOffset expiresAt = now.AddMinutes(_securityOptions.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtRegisteredClaimNames.Sid, sessionId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
            new("auth_time", authenticatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("authz_ver", authorizationVersion.ToString(CultureInfo.InvariantCulture)),
            new("token_type", "access"),
        };
        claims.AddRange(authenticationMethods.Select(method => new Claim(JwtRegisteredClaimNames.Amr, method)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = keyProvider.SigningCredentials,
            TokenType = "at+jwt",
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }
}
