using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Shared;

/// <summary>
/// Shared static helper methods used across multiple infrastructure services.
/// Extracted from the monolithic Phase6Service to enable clean separation.
/// </summary>
internal static class InfrastructureHelpers
{
    private const string InvalidCursorCode = "CURSOR.INVALID";

    public static Result<T> CursorFailure<T>() => Result.Failure<T>(ResultError.BusinessRule(
        InvalidCursorCode,
        "The cursor is invalid or does not match this query."));

    public static Result<T> VersionConflict<T>(long currentVersion) => Result.Failure<T>(ResultError.PreconditionFailed(
        "COURSE.VERSION_CONFLICT",
        "The course draft was changed by another request.",
        ETag(currentVersion)));

    public static Result<T> PreconditionRequired<T>() => Result.Failure<T>(ResultError.PreconditionRequired(
        "COURSE.IF_MATCH_REQUIRED",
        "The If-Match precondition is required."));

    public static ResultError CourseNotFound() => ResultError.NotFound(
        "COURSE.NOT_FOUND",
        "The course was not found or is not available to this account.");

    public static string ETag(long version) => $"\"v{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static int NormalizeLimit(int limit, int defaultValue) => limit <= 0 ? defaultValue : Math.Min(limit, 100);

    public static string NormalizeLocale(string locale) => locale.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Only ar and en are supported."),
    };

    public static string NormalizeLevel(string level) => level.Trim().ToLowerInvariant() switch
    {
        "beginner" => "Beginner",
        "intermediate" => "Intermediate",
        "advanced" => "Advanced",
        "alllevels" => "AllLevels",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static string PublicLevel(string level) => level switch
    {
        "Beginner" => "beginner",
        "Intermediate" => "intermediate",
        "Advanced" => "advanced",
        "AllLevels" => "beginner",
        _ => throw new InvalidOperationException("The catalog level is invalid."),
    };

    public static Result<T> FailAndClear<T>(DorosakDbContext dbContext, ResultError error)
    {
        dbContext.ChangeTracker.Clear();
        return Result.Failure<T>(error);
    }

    public static void AddAudit(
        DorosakDbContext dbContext,
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string? reason,
        TimeProvider timeProvider) =>
        dbContext.AuditLogs.Add(AuditLog.Create(
            actorUserId,
            action,
            targetType,
            targetId,
            "Succeeded",
            reason,
            timeProvider.GetUtcNow()));

    public static PagedResponse<TResponse> Page<TEntity, TResponse>(
        List<TEntity> source,
        int limit,
        Func<TEntity, TResponse> map,
        Func<TEntity, DateTimeOffset> date,
        Func<TEntity, Guid> id,
        string scope,
        string canonical,
        CatalogCursorCodec cursorCodec)
    {
        bool hasMore = source.Count > limit;
        List<TEntity> items = source.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create(scope, canonical, date(items[^1]), id(items[^1]))
            : null;
        return new PagedResponse<TResponse>(items.Select(map).ToArray(), nextCursor, hasMore);
    }

    public static string GenerateSlug(string title, Guid courseId)
    {
        string normalized = title.Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        bool containedLatin = false;
        foreach (char character in normalized)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                containedLatin = containedLatin || char.IsAsciiLetter(character);
            }
            else if (TryTransliterateArabic(character, out string? transliteration))
            {
                builder.Append(transliteration);
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                builder.Append('-');
            }
        }

        string value = CollapseHyphens(builder.ToString()).Trim('-');
        if (value.Length == 0)
        {
            value = "course";
        }
        if (!containedLatin)
        {
            string suffix = Convert.ToHexString(SHA256.HashData(courseId.ToByteArray()))[..8].ToLowerInvariant();
            value = $"{value}-{suffix}";
        }
        return value.Length <= 160 ? value : value[..160].TrimEnd('-');
    }

    private static string CollapseHyphens(string value)
    {
        while (value.Contains("--", StringComparison.Ordinal))
        {
            value = value.Replace("--", "-", StringComparison.Ordinal);
        }
        return value;
    }

    private static bool TryTransliterateArabic(char character, out string? value)
    {
        value = character switch
        {
            'ا' or 'أ' or 'إ' or 'آ' => "a",
            'ب' => "b",
            'ت' or 'ة' => "t",
            'ث' => "th",
            'ج' => "j",
            'ح' => "h",
            'خ' => "kh",
            'د' => "d",
            'ذ' => "dh",
            'ر' => "r",
            'ز' => "z",
            'س' => "s",
            'ش' => "sh",
            'ص' => "s",
            'ض' => "d",
            'ط' => "t",
            'ظ' => "z",
            'ع' => "a",
            'غ' => "gh",
            'ف' => "f",
            'ق' => "q",
            'ك' => "k",
            'ل' => "l",
            'م' => "m",
            'ن' => "n",
            'ه' => "h",
            'و' or 'ؤ' => "w",
            'ي' or 'ى' or 'ئ' => "y",
            'ء' => string.Empty,
            _ => null,
        };
        return value is not null;
    }
}
