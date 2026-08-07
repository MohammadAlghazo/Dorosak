namespace Dorosak.Domain.Catalog;

public sealed class CourseLocalization
{
    private CourseLocalization()
    {
    }

    private CourseLocalization(
        Guid courseId,
        string locale,
        string title,
        string subtitle,
        string description,
        Guid currentSlugId,
        DateTimeOffset now)
    {
        CourseId = courseId;
        Locale = locale;
        Title = title;
        Subtitle = subtitle;
        Description = description;
        CurrentSlugId = currentSlugId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid CourseId { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid CurrentSlugId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CourseLocalization Create(
        Guid courseId,
        string locale,
        string title,
        string subtitle,
        string description,
        Guid currentSlugId,
        DateTimeOffset now) => new(
            courseId,
            NormalizeLocale(locale),
            Require(title),
            subtitle.Trim(),
            Require(description),
            currentSlugId,
            now);

    public void Update(
        string title,
        string subtitle,
        string description,
        Guid currentSlugId,
        DateTimeOffset now)
    {
        Title = Require(title);
        Subtitle = subtitle.Trim();
        Description = Require(description);
        CurrentSlugId = currentSlugId;
        UpdatedAt = now;
    }

    private static string Require(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string NormalizeLocale(string locale) => locale.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(locale)),
    };
}

public sealed class CourseSlug
{
    private CourseSlug()
    {
    }

    private CourseSlug(Guid id, Guid courseId, string locale, string slug, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        Locale = locale;
        Slug = slug;
        IsCurrent = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool IsCurrent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public static CourseSlug Create(Guid courseId, string locale, string slug, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), courseId, locale, slug, now);

    public void Retire(DateTimeOffset now)
    {
        IsCurrent = false;
        RetiredAt = now;
    }
}
