using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Administration;
using Dorosak.Domain.Cms;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Administration;

internal sealed class AdministrationService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider,
    CatalogCursorCodec cursorCodec) : IAdministrationService
{
    public async Task<Result<AdminCmsResponse>> GetAdminCmsAsync(CancellationToken cancellationToken)
    {
        CmsPage[] pages = await dbContext.CmsPages.AsNoTracking().OrderBy(page => page.Slug).ToArrayAsync(cancellationToken);
        CmsFaq[] faqs = await dbContext.CmsFaqs.AsNoTracking()
            .OrderBy(faq => faq.DisplayOrder).ThenBy(faq => faq.Id).ToArrayAsync(cancellationToken);
        CmsPageRevision[] pageRevisions = await dbContext.CmsPageRevisions.AsNoTracking()
            .Where(revision => pages.Select(page => page.Id).Contains(revision.PageId))
            .ToArrayAsync(cancellationToken);
        CmsFaqRevision[] faqRevisions = await dbContext.CmsFaqRevisions.AsNoTracking()
            .Where(revision => faqs.Select(faq => faq.Id).Contains(revision.FaqId))
            .ToArrayAsync(cancellationToken);
        return Result.Success(new AdminCmsResponse(
            pages.Select(page => MapPage(
                page,
                pageRevisions.SingleOrDefault(revision => revision.PageId == page.Id && revision.Version == page.CurrentVersion),
                page.PublishedVersion is { } published
                    ? pageRevisions.SingleOrDefault(revision => revision.PageId == page.Id && revision.Version == published)
                    : null)).ToArray(),
            faqs.Select(faq => MapFaq(
                faq,
                faqRevisions.SingleOrDefault(revision => revision.FaqId == faq.Id && revision.Version == faq.CurrentVersion),
                faq.PublishedVersion is { } published
                    ? faqRevisions.SingleOrDefault(revision => revision.FaqId == faq.Id && revision.Version == published)
                    : null)).ToArray()));
    }

    public async Task<Result<PublicCmsPageResponse>> GetPublicCmsPageAsync(
        GetPublicCmsPageQuery request,
        CancellationToken cancellationToken)
    {
        string slug;
        try
        {
            slug = CmsPage.NormalizeSlug(request.Slug);
        }
        catch (Domain.Common.DomainRuleException)
        {
            return Result.Failure<PublicCmsPageResponse>(ResultError.NotFound(
                "CMS.PAGE_NOT_FOUND",
                "The requested page was not found."));
        }

        CmsPage? page = await dbContext.CmsPages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == slug && item.PublishedVersion != null, cancellationToken);
        if (page?.PublishedVersion is not { } version)
        {
            return Result.Failure<PublicCmsPageResponse>(ResultError.NotFound(
                "CMS.PAGE_NOT_FOUND",
                "The requested page was not found."));
        }

        CmsPageRevision? revision = await dbContext.CmsPageRevisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PageId == page.Id && item.Version == version, cancellationToken);
        return revision is null || page.PublishedAt is null
            ? Result.Failure<PublicCmsPageResponse>(ResultError.NotFound("CMS.PAGE_NOT_FOUND", "The requested page was not found."))
            : Result.Success(new PublicCmsPageResponse(
                page.Slug,
                request.Locale,
                request.Locale == "ar" ? revision.TitleAr : revision.TitleEn,
                request.Locale == "ar" ? revision.BodyAr : revision.BodyEn,
                revision.Version,
                page.PublishedAt.Value));
    }

    public async Task<Result<IReadOnlyList<PublicCmsFaqResponse>>> GetPublicFaqsAsync(
        GetPublicFaqsQuery request,
        CancellationToken cancellationToken)
    {
        CmsFaq[] faqs = await dbContext.CmsFaqs.AsNoTracking()
            .Where(faq => faq.PublishedVersion != null && faq.PublishedDisplayOrder != null)
            .OrderBy(faq => faq.PublishedDisplayOrder).ThenBy(faq => faq.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] ids = faqs.Select(faq => faq.Id).ToArray();
        CmsFaqRevision[] revisions = await dbContext.CmsFaqRevisions.AsNoTracking()
            .Where(revision => ids.Contains(revision.FaqId))
            .ToArrayAsync(cancellationToken);

        var result = new List<PublicCmsFaqResponse>(faqs.Length);
        foreach (CmsFaq faq in faqs)
        {
            if (faq.PublishedVersion is not { } version || faq.PublishedAt is null)
            {
                continue;
            }
            CmsFaqRevision? revision = revisions.SingleOrDefault(item => item.FaqId == faq.Id && item.Version == version);
            if (revision is null)
            {
                continue;
            }
            result.Add(new PublicCmsFaqResponse(
                faq.Id,
                request.Locale,
                request.Locale == "ar" ? revision.QuestionAr : revision.QuestionEn,
                request.Locale == "ar" ? revision.AnswerAr : revision.AnswerEn,
                version,
                faq.PublishedDisplayOrder!.Value,
                faq.PublishedAt.Value));
        }
        return Result.Success<IReadOnlyList<PublicCmsFaqResponse>>(result);
    }

    public async Task<Result<PortfolioSettingsResponse>> GetAdminSettingsAsync(CancellationToken cancellationToken)
    {
        PortfolioSettings? settings = await dbContext.PortfolioSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == PortfolioSettings.SingletonId, cancellationToken);
        return settings is null
            ? Result.Failure<PortfolioSettingsResponse>(ResultError.Failure("SETTINGS.NOT_INITIALIZED", "The portfolio settings row has not been initialized."))
            : Result.Success(MapSettings(settings));
    }

    public async Task<Result<PublicPortfolioSettingsResponse>> GetPublicSettingsAsync(
        GetPublicSettingsQuery request,
        CancellationToken cancellationToken)
    {
        PortfolioSettings? settings = await dbContext.PortfolioSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == PortfolioSettings.SingletonId, cancellationToken);
        return settings is null
            ? Result.Failure<PublicPortfolioSettingsResponse>(ResultError.Failure(
                "SETTINGS.NOT_INITIALIZED",
                "The portfolio settings row has not been initialized."))
            : Result.Success(new PublicPortfolioSettingsResponse(
                request.Locale,
                settings.FeaturedCourseLimit,
                settings.ShowPortfolioNotice,
                request.Locale == "ar" ? settings.NoticeAr : settings.NoticeEn));
    }

    public async Task<Result<CmsPageResponse>> UpsertCmsPageDraftAsync(
        UpsertCmsPageDraftCommand request,
        CancellationToken cancellationToken)
    {
        string slug;
        try
        {
            slug = CmsPage.NormalizeSlug(request.Slug);
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<CmsPageResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        await LockAsync($"cms-page:{slug}", cancellationToken);

        CmsPage? page = await dbContext.CmsPages.SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (page is null)
            {
                if (request.ExpectedVersion != 0)
                {
                    return Result.Failure<CmsPageResponse>(ResultError.Conflict("CMS.VERSION_CONFLICT", "The CMS resource changed before this operation."));
                }
                page = CmsPage.Create(slug, now);
                dbContext.CmsPages.Add(page);
            }
            int version = page.AddRevision(request.ExpectedVersion, now);
            CmsPageRevision revision = CmsPageRevision.Create(
                page.Id,
                version,
                request.TitleAr,
                request.TitleEn,
                request.BodyAr,
                request.BodyEn,
                request.ActorUserId,
                now);
            dbContext.CmsPageRevisions.Add(revision);
            CmsPageRevision? publishedRevision = page.PublishedVersion is { } publishedVersion
                ? await dbContext.CmsPageRevisions.SingleAsync(
                    item => item.PageId == page.Id && item.Version == publishedVersion,
                    cancellationToken)
                : null;
            AddAudit(request.ActorUserId, "cms.page-draft-saved", "CmsPage", page.Id, request.AuditReason, now);
            return Result.Success(MapPage(page, revision, publishedRevision));
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<CmsPageResponse>(MapDomainFailure(exception));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<CmsPageResponse>(ResultError.BusinessRule("CMS.VALUE_INVALID", exception.Message));
        }
    }

    public async Task<Result<CmsPageResponse>> PublishCmsPageAsync(
        PublishCmsPageCommand request,
        CancellationToken cancellationToken)
    {
        string slug = request.Slug.Trim().ToLowerInvariant();
        await LockAsync($"cms-page:{slug}", cancellationToken);
        CmsPage? page = await dbContext.CmsPages.SingleOrDefaultAsync(
            item => item.Slug == slug,
            cancellationToken);
        if (page is null)
        {
            return Result.Failure<CmsPageResponse>(ResultError.NotFound("CMS.PAGE_NOT_FOUND", "The CMS page was not found."));
        }
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (page.PublishedVersion == page.CurrentVersion && page.CurrentVersion == request.ExpectedVersion)
            {
                CmsPageRevision publishedRevision = await dbContext.CmsPageRevisions.SingleAsync(
                    item => item.PageId == page.Id && item.Version == page.CurrentVersion,
                    cancellationToken);
                return Result.Success(MapPage(page, publishedRevision, publishedRevision));
            }
            page.Publish(request.ExpectedVersion, request.ActorUserId, now);
            CmsPageRevision revision = await dbContext.CmsPageRevisions.SingleAsync(
                item => item.PageId == page.Id && item.Version == page.CurrentVersion,
                cancellationToken);
            AddAudit(request.ActorUserId, "cms.page-published", "CmsPage", page.Id, request.AuditReason, now);
            return Result.Success(MapPage(page, revision, revision));
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<CmsPageResponse>(MapDomainFailure(exception));
        }
    }

    public async Task<Result<CmsFaqResponse>> UpsertCmsFaqDraftAsync(
        UpsertCmsFaqDraftCommand request,
        CancellationToken cancellationToken)
    {
        await LockAsync($"cms-faq:{request.FaqId?.ToString("D") ?? "new"}", cancellationToken);
        CmsFaq? faq = request.FaqId is { } faqId
            ? await dbContext.CmsFaqs.SingleOrDefaultAsync(item => item.Id == faqId, cancellationToken)
            : null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (faq is null)
            {
                if (request.FaqId is not null || request.ExpectedVersion != 0)
                {
                    return Result.Failure<CmsFaqResponse>(ResultError.NotFound("CMS.FAQ_NOT_FOUND", "The FAQ was not found."));
                }
                faq = CmsFaq.Create(request.DisplayOrder, now);
                dbContext.CmsFaqs.Add(faq);
            }
            int version = faq.AddRevision(request.ExpectedVersion, request.DisplayOrder, now);
            CmsFaqRevision revision = CmsFaqRevision.Create(
                faq.Id,
                version,
                request.QuestionAr,
                request.QuestionEn,
                request.AnswerAr,
                request.AnswerEn,
                request.ActorUserId,
                now);
            dbContext.CmsFaqRevisions.Add(revision);
            CmsFaqRevision? publishedRevision = faq.PublishedVersion is { } publishedVersion
                ? await dbContext.CmsFaqRevisions.SingleAsync(
                    item => item.FaqId == faq.Id && item.Version == publishedVersion,
                    cancellationToken)
                : null;
            AddAudit(request.ActorUserId, "cms.faq-draft-saved", "CmsFaq", faq.Id, request.AuditReason, now);
            return Result.Success(MapFaq(faq, revision, publishedRevision));
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<CmsFaqResponse>(MapDomainFailure(exception));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<CmsFaqResponse>(ResultError.BusinessRule("CMS.VALUE_INVALID", exception.Message));
        }
    }

    public async Task<Result<CmsFaqResponse>> PublishCmsFaqAsync(
        PublishCmsFaqCommand request,
        CancellationToken cancellationToken)
    {
        await LockAsync($"cms-faq:{request.FaqId:D}", cancellationToken);
        CmsFaq? faq = await dbContext.CmsFaqs.SingleOrDefaultAsync(item => item.Id == request.FaqId, cancellationToken);
        if (faq is null)
        {
            return Result.Failure<CmsFaqResponse>(ResultError.NotFound("CMS.FAQ_NOT_FOUND", "The FAQ was not found."));
        }
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (faq.PublishedVersion == faq.CurrentVersion && faq.CurrentVersion == request.ExpectedVersion)
            {
                CmsFaqRevision publishedRevision = await dbContext.CmsFaqRevisions.SingleAsync(
                    item => item.FaqId == faq.Id && item.Version == faq.CurrentVersion,
                    cancellationToken);
                return Result.Success(MapFaq(faq, publishedRevision, publishedRevision));
            }
            faq.Publish(request.ExpectedVersion, request.ActorUserId, now);
            CmsFaqRevision revision = await dbContext.CmsFaqRevisions.SingleAsync(
                item => item.FaqId == faq.Id && item.Version == faq.CurrentVersion,
                cancellationToken);
            AddAudit(request.ActorUserId, "cms.faq-published", "CmsFaq", faq.Id, request.AuditReason, now);
            return Result.Success(MapFaq(faq, revision, revision));
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<CmsFaqResponse>(MapDomainFailure(exception));
        }
    }

    public async Task<Result<PortfolioSettingsResponse>> UpdatePortfolioSettingsAsync(
        UpdatePortfolioSettingsCommand request,
        CancellationToken cancellationToken)
    {
        await LockAsync("portfolio-settings", cancellationToken);
        PortfolioSettings? settings = await dbContext.PortfolioSettings
            .SingleOrDefaultAsync(item => item.Id == PortfolioSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            return Result.Failure<PortfolioSettingsResponse>(ResultError.Failure("SETTINGS.NOT_INITIALIZED", "The portfolio settings row has not been initialized."));
        }
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            settings.Update(
                request.FeaturedCourseLimit,
                request.ShowPortfolioNotice,
                request.NoticeAr,
                request.NoticeEn,
                request.ExpectedVersion,
                request.ActorUserId,
                now);
            AddAudit(request.ActorUserId, "settings.portfolio-updated", "PortfolioSettings", settings.Id, request.AuditReason, now);
            return Result.Success(MapSettings(settings));
        }
        catch (Domain.Common.DomainRuleException exception)
        {
            return Result.Failure<PortfolioSettingsResponse>(MapDomainFailure(exception));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<PortfolioSettingsResponse>(ResultError.BusinessRule("SETTINGS.VALUE_INVALID", exception.Message));
        }
    }

    public async Task<Result<AuditLogPageResponse>> GetAuditLogsAsync(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        int limit = Math.Clamp(request.Limit, 1, 100);
        string action = request.Action?.Trim() ?? string.Empty;
        string canonical = $"audit|{action}|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "audit-logs", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return Result.Failure<AuditLogPageResponse>(ResultError.BusinessRule("CURSOR.INVALID", "The audit cursor is invalid or expired."));
        }

        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking();
        if (action.Length > 0) query = query.Where(log => log.Action == action);
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(log => log.OccurredAt < timestamp || log.OccurredAt == timestamp && log.Id.CompareTo(id) < 0);
        }

        AuditLog[] rows = await query.OrderByDescending(log => log.OccurredAt).ThenByDescending(log => log.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        bool hasMore = rows.Length > limit;
        AuditLog[] page = hasMore ? rows[..limit] : rows;
        string? nextCursor = hasMore
            ? cursorCodec.Create("audit-logs", canonical, page[^1].OccurredAt, page[^1].Id)
            : null;
        AddAudit(
            request.ActorUserId,
            "audit.logs-read",
            "AuditLog",
            request.ActorUserId,
            request.AuditReason,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new AuditLogPageResponse(page.Select(MapAudit).ToArray(), nextCursor, hasMore));
    }

    private void AddAudit(Guid actorUserId, string action, string targetType, Guid targetId, string reason, DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", reason, now));

    private Task<int> LockAsync(string identity, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))",
            cancellationToken);

    private static ResultError MapDomainFailure(Domain.Common.DomainRuleException exception) => exception.Code switch
    {
        "CMS.VERSION_CONFLICT" or "SETTINGS.VERSION_CONFLICT" => ResultError.Conflict(exception.Code, exception.Message),
        _ => ResultError.BusinessRule(exception.Code, exception.Message),
    };

    private static CmsPageResponse MapPage(CmsPage page, CmsPageRevision? draft, CmsPageRevision? published) =>
        new(page.Id, page.Slug, page.CurrentVersion, page.PublishedVersion, Map(draft), Map(published), page.UpdatedAt, page.PublishedAt);

    private static CmsPageRevisionResponse? Map(CmsPageRevision? revision) => revision is null
        ? null
        : new(revision.Version, revision.TitleAr, revision.TitleEn, revision.BodyAr, revision.BodyEn,
            revision.CreatedByUserId, revision.CreatedAt);

    private static CmsFaqResponse MapFaq(CmsFaq faq, CmsFaqRevision? draft, CmsFaqRevision? published) =>
        new(faq.Id, faq.DisplayOrder, faq.CurrentVersion, faq.PublishedVersion, Map(draft), Map(published), faq.UpdatedAt, faq.PublishedAt);

    private static CmsFaqRevisionResponse? Map(CmsFaqRevision? revision) => revision is null
        ? null
        : new(revision.Version, revision.QuestionAr, revision.QuestionEn, revision.AnswerAr, revision.AnswerEn,
            revision.CreatedByUserId, revision.CreatedAt);

    private static PortfolioSettingsResponse MapSettings(PortfolioSettings settings) =>
        new(settings.FeaturedCourseLimit, settings.ShowPortfolioNotice, settings.NoticeAr, settings.NoticeEn, settings.Version, settings.UpdatedAt);

    private static AuditLogResponse MapAudit(AuditLog log) =>
        new(log.Id, log.ActorUserId, log.Action, log.TargetType, log.TargetId, log.Result, log.Reason, log.OccurredAt);
}
