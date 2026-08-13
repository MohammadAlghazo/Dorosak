using Dorosak.Domain.Common;

namespace Dorosak.Domain.Cms;

public sealed class CmsPage
{
    private static readonly HashSet<string> AllowedSlugs = new(StringComparer.Ordinal)
    {
        "about",
        "contact",
        "privacy",
        "terms",
    };

    private CmsPage()
    {
    }

    private CmsPage(Guid id, string slug, DateTimeOffset now)
    {
        Id = id;
        Slug = NormalizeSlug(slug);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public int CurrentVersion { get; private set; }
    public int? PublishedVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? PublishedByUserId { get; private set; }

    public static CmsPage Create(string slug, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), slug, now);

    public int AddRevision(int expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        CurrentVersion++;
        UpdatedAt = now;
        return CurrentVersion;
    }

    public void Publish(int expectedVersion, Guid actorUserId, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (CurrentVersion == 0)
        {
            throw new DomainRuleException("CMS.PAGE_EMPTY", "A page needs a draft before it can be published.");
        }
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A publishing actor is required.", nameof(actorUserId));
        }

        PublishedVersion = CurrentVersion;
        PublishedAt = now;
        PublishedByUserId = actorUserId;
        UpdatedAt = now;
    }

    public static string NormalizeSlug(string value)
    {
        string slug = value.Trim().ToLowerInvariant();
        return AllowedSlugs.Contains(slug)
            ? slug
            : throw new DomainRuleException("CMS.PAGE_SLUG_INVALID", "The CMS page slug is not supported.");
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != CurrentVersion)
        {
            throw new DomainRuleException("CMS.VERSION_CONFLICT", "The CMS resource changed before this operation.");
        }
    }
}

public sealed class CmsPageRevision
{
    private CmsPageRevision()
    {
    }

    private CmsPageRevision(
        Guid id,
        Guid pageId,
        int version,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        Id = id;
        PageId = pageId;
        Version = version;
        TitleAr = Normalize(titleAr, 200, nameof(titleAr));
        TitleEn = Normalize(titleEn, 200, nameof(titleEn));
        BodyAr = Normalize(bodyAr, 20000, nameof(bodyAr));
        BodyEn = Normalize(bodyEn, 20000, nameof(bodyEn));
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid PageId { get; private set; }
    public int Version { get; private set; }
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public string BodyAr { get; private set; } = string.Empty;
    public string BodyEn { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CmsPageRevision Create(
        Guid pageId,
        int version,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        if (pageId == Guid.Empty || createdByUserId == Guid.Empty || version <= 0)
        {
            throw new ArgumentException("CMS page revision identifiers are invalid.");
        }
        return new CmsPageRevision(
            Guid.CreateVersion7(),
            pageId,
            version,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            createdByUserId,
            createdAt);
    }

    private static string Normalize(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }
}

public sealed class CmsFaq
{
    private CmsFaq()
    {
    }

    private CmsFaq(Guid id, int displayOrder, DateTimeOffset now)
    {
        Id = id;
        SetDisplayOrder(displayOrder);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public int DisplayOrder { get; private set; }
    public int? PublishedDisplayOrder { get; private set; }
    public int CurrentVersion { get; private set; }
    public int? PublishedVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? PublishedByUserId { get; private set; }

    public static CmsFaq Create(int displayOrder, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), displayOrder, now);

    public int AddRevision(int expectedVersion, int displayOrder, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        SetDisplayOrder(displayOrder);
        CurrentVersion++;
        UpdatedAt = now;
        return CurrentVersion;
    }

    public void Publish(int expectedVersion, Guid actorUserId, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (CurrentVersion == 0)
        {
            throw new DomainRuleException("CMS.FAQ_EMPTY", "An FAQ needs a draft before it can be published.");
        }
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A publishing actor is required.", nameof(actorUserId));
        }

        PublishedVersion = CurrentVersion;
        PublishedDisplayOrder = DisplayOrder;
        PublishedAt = now;
        PublishedByUserId = actorUserId;
        UpdatedAt = now;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != CurrentVersion)
        {
            throw new DomainRuleException("CMS.VERSION_CONFLICT", "The CMS resource changed before this operation.");
        }
    }

    private void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder is < 0 or > 10000)
        {
            throw new DomainRuleException("CMS.DISPLAY_ORDER_INVALID", "The display order must be between 0 and 10000.");
        }
        DisplayOrder = displayOrder;
    }
}

public sealed class CmsFaqRevision
{
    private CmsFaqRevision()
    {
    }

    private CmsFaqRevision(
        Guid id,
        Guid faqId,
        int version,
        string questionAr,
        string questionEn,
        string answerAr,
        string answerEn,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        Id = id;
        FaqId = faqId;
        Version = version;
        QuestionAr = Normalize(questionAr, 300, nameof(questionAr));
        QuestionEn = Normalize(questionEn, 300, nameof(questionEn));
        AnswerAr = Normalize(answerAr, 5000, nameof(answerAr));
        AnswerEn = Normalize(answerEn, 5000, nameof(answerEn));
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FaqId { get; private set; }
    public int Version { get; private set; }
    public string QuestionAr { get; private set; } = string.Empty;
    public string QuestionEn { get; private set; } = string.Empty;
    public string AnswerAr { get; private set; } = string.Empty;
    public string AnswerEn { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CmsFaqRevision Create(
        Guid faqId,
        int version,
        string questionAr,
        string questionEn,
        string answerAr,
        string answerEn,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        if (faqId == Guid.Empty || createdByUserId == Guid.Empty || version <= 0)
        {
            throw new ArgumentException("CMS FAQ revision identifiers are invalid.");
        }
        return new CmsFaqRevision(
            Guid.CreateVersion7(),
            faqId,
            version,
            questionAr,
            questionEn,
            answerAr,
            answerEn,
            createdByUserId,
            createdAt);
    }

    private static string Normalize(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }
}

public sealed class PortfolioSettings
{
    public static readonly Guid SingletonId = Guid.Parse("018f3f0e-4380-7b1b-8f8d-b8ea9c546024");

    private PortfolioSettings()
    {
    }

    private PortfolioSettings(DateTimeOffset now)
    {
        Id = SingletonId;
        FeaturedCourseLimit = 3;
        ShowPortfolioNotice = false;
        NoticeAr = "نسخة عرض محلية بلا دفع حقيقي.";
        NoticeEn = "A local showcase with no real payments.";
        Version = 1;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public int FeaturedCourseLimit { get; private set; }
    public bool ShowPortfolioNotice { get; private set; }
    public string NoticeAr { get; private set; } = string.Empty;
    public string NoticeEn { get; private set; } = string.Empty;
    public long Version { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PortfolioSettings CreateDefaults(DateTimeOffset now) => new(now);

    public void Update(
        int featuredCourseLimit,
        bool showPortfolioNotice,
        string noticeAr,
        string noticeEn,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (expectedVersion != Version)
        {
            throw new DomainRuleException("SETTINGS.VERSION_CONFLICT", "The platform settings changed before this operation.");
        }
        if (featuredCourseLimit is < 1 or > 12)
        {
            throw new DomainRuleException("SETTINGS.FEATURED_LIMIT_INVALID", "The featured course limit must be between 1 and 12.");
        }
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A settings actor is required.", nameof(actorUserId));
        }

        string normalizedAr = NormalizeNotice(noticeAr, nameof(noticeAr));
        string normalizedEn = NormalizeNotice(noticeEn, nameof(noticeEn));
        if (showPortfolioNotice && (normalizedAr.Length == 0 || normalizedEn.Length == 0))
        {
            throw new DomainRuleException("SETTINGS.NOTICE_REQUIRED", "Both notice translations are required when shown.");
        }

        FeaturedCourseLimit = featuredCourseLimit;
        ShowPortfolioNotice = showPortfolioNotice;
        NoticeAr = normalizedAr;
        NoticeEn = normalizedEn;
        Version++;
        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
    }

    private static string NormalizeNotice(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        string normalized = value.Trim();
        if (normalized.Length > 240)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }
}
