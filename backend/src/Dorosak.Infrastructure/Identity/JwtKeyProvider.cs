using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dorosak.Infrastructure.Identity;

public interface IJwtKeyProvider
{
    SecurityKey ValidationKey { get; }

    SigningCredentials SigningCredentials { get; }

    string KeyId { get; }
}

internal sealed class JwtKeyProvider : IJwtKeyProvider, IDisposable
{
    private readonly RSA _rsa;

    public JwtKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment)
    {
        JwtOptions jwtOptions = options.Value;
        _rsa = RSA.Create(3072);
        if (!string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPem))
        {
            _rsa.ImportFromPem(jwtOptions.PrivateKeyPem);
        }
        else if (environment.IsProduction())
        {
            _rsa.Dispose();
            throw new InvalidOperationException("Jwt:PrivateKeyPem is required in Production.");
        }

        var key = new RsaSecurityKey(_rsa) { KeyId = jwtOptions.KeyId };
        ValidationKey = key;
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        KeyId = jwtOptions.KeyId;
    }

    public SecurityKey ValidationKey { get; }

    public SigningCredentials SigningCredentials { get; }

    public string KeyId { get; }

    public void Dispose() => _rsa.Dispose();
}
