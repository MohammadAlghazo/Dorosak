using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Catalog;

internal sealed class CatalogCursorCodec(IOptions<CatalogCursorOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.SigningKey);

    public string Create(
        string scope,
        string canonicalQuery,
        DateTimeOffset? afterUpdatedAt,
        Guid? afterId,
        string? afterKey = null)
    {
        CursorPayload payload = new(
            1,
            scope,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalQuery))).ToLowerInvariant(),
            afterUpdatedAt,
            afterId,
            afterKey);
        string encodedPayload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        string signature = WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(encodedPayload)));
        return $"{encodedPayload}.{signature}";
    }

    public bool TryRead(
        string? cursor,
        string scope,
        string canonicalQuery,
        out DateTimeOffset? afterUpdatedAt,
        out Guid? afterId)
    {
        return TryRead(cursor, scope, canonicalQuery, out afterUpdatedAt, out afterId, out _);
    }

    public bool TryRead(
        string? cursor,
        string scope,
        string canonicalQuery,
        out DateTimeOffset? afterUpdatedAt,
        out Guid? afterId,
        out string? afterKey)
    {
        afterUpdatedAt = null;
        afterId = null;
        afterKey = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        string[] parts = cursor.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = WebEncoders.Base64UrlDecode(parts[0]);
            signature = WebEncoders.Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] expectedSignature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(parts[0]));
        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
        {
            return false;
        }

        CursorPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CursorPayload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        string queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalQuery))).ToLowerInvariant();
        if (payload is null || payload.Version != 1 || !string.Equals(payload.Scope, scope, StringComparison.Ordinal) ||
            !string.Equals(payload.QueryHash, queryHash, StringComparison.Ordinal))
        {
            return false;
        }

        afterUpdatedAt = payload.AfterUpdatedAt;
        afterId = payload.AfterId;
        afterKey = payload.AfterKey;
        return true;
    }

    private sealed record CursorPayload(
        int Version,
        string Scope,
        string QueryHash,
        DateTimeOffset? AfterUpdatedAt,
        Guid? AfterId,
        string? AfterKey);
}
