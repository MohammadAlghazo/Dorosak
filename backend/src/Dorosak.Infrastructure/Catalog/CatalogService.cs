using System.Diagnostics;
using System.Globalization;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Catalog;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Catalog;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Catalog;

internal sealed class CatalogService(
    DorosakDbContext dbContext,
    CatalogCursorCodec cursorCodec,
    SearchTelemetry searchTelemetry,
    TimeProvider timeProvider) : ICatalogService, IPublicCatalogPort
{
    public async Task<Result<PagedResponse<CategoryResponse>>> GetCategoriesAsync(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 100);
        string canonical = $"categories|{locale}|display-order|{limit}|all:{request.IncludeInactive}";
        if (!cursorCodec.TryRead(request.Cursor, "categories", canonical, out _, out Guid? afterId, out string? afterKey))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<CategoryResponse>>();
        }

        IQueryable<Category> query = dbContext.Categories.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(category => category.IsActive);
        }
        if (afterId is { } id && int.TryParse(afterKey, NumberStyles.None, CultureInfo.InvariantCulture, out int displayOrder))
        {
            query = query.Where(category =>
                category.DisplayOrder > displayOrder ||
                category.DisplayOrder == displayOrder && category.Id.CompareTo(id) > 0);
        }
        else if (request.Cursor is not null)
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<CategoryResponse>>();
        }
        List<Category> categories = await query
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<TaxonomyLocalizationResponse>> localizations = await LoadCategoryLocalizationsAsync(
            categories.Take(limit).Select(category => category.Id).ToArray(),
            cancellationToken);
        bool hasMore = categories.Count > limit;
        List<Category> items = categories.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create(
                "categories",
                canonical,
                null,
                items[^1].Id,
                items[^1].DisplayOrder.ToString(CultureInfo.InvariantCulture))
            : null;
        return Result.Success(new PagedResponse<CategoryResponse>(
            items.Select(category => MapCategory(category, localizations.GetValueOrDefault(category.Id) ?? [])).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<PagedResponse<TagResponse>>> GetTagsAsync(
        GetTagsQuery request,
        CancellationToken cancellationToken)
    {
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 100);
        string canonical = $"tags|{locale}|code|{limit}|all:{request.IncludeInactive}";
        if (!cursorCodec.TryRead(request.Cursor, "tags", canonical, out _, out Guid? afterId, out string? afterKey))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<TagResponse>>();
        }

        IQueryable<Tag> query = dbContext.Tags.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(tag => tag.IsActive);
        }
        if (afterId is { } id && afterKey is not null)
        {
            query = query.Where(tag =>
                EF.Functions.Collate(tag.Code, "C").CompareTo(afterKey) > 0 ||
                tag.Code == afterKey && tag.Id.CompareTo(id) > 0);
        }
        else if (request.Cursor is not null)
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<TagResponse>>();
        }
        List<Tag> tags = await query
            .OrderBy(tag => EF.Functions.Collate(tag.Code, "C"))
            .ThenBy(tag => tag.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<TaxonomyLocalizationResponse>> localizations = await LoadTagLocalizationsAsync(
            tags.Take(limit).Select(tag => tag.Id).ToArray(),
            cancellationToken);
        bool hasMore = tags.Count > limit;
        List<Tag> items = tags.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create("tags", canonical, null, items[^1].Id, items[^1].Code)
            : null;
        return Result.Success(new PagedResponse<TagResponse>(
            items.Select(tag => MapTag(tag, localizations.GetValueOrDefault(tag.Id) ?? [])).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<CategoryResponse>> UpsertCategoryAsync(
        UpsertCategoryCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Category? category = request.CategoryId is { } id
            ? await dbContext.Categories.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (request.CategoryId is not null && category is null)
        {
            return Result.Failure<CategoryResponse>(ResultError.NotFound("CATEGORY.NOT_FOUND", "The category was not found."));
        }
        if (category is not null && !string.Equals(category.Code, request.Code, StringComparison.Ordinal))
        {
            return Result.Failure<CategoryResponse>(ResultError.Conflict(
                "CATEGORY.CODE_IMMUTABLE",
                "A category code cannot be changed."));
        }
        if (request.ParentId == request.CategoryId)
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_INVALID",
                "A category cannot be its own parent."));
        }
        Category? parent = request.ParentId is { } parentId
            ? await dbContext.Categories.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == parentId,
                cancellationToken)
            : null;
        if (request.ParentId is not null && parent is null)
        {
            return Result.Failure<CategoryResponse>(ResultError.NotFound("CATEGORY.PARENT_NOT_FOUND", "The parent category was not found."));
        }
        if (request.IsActive && parent is { IsActive: false })
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_INACTIVE",
                "An active category cannot belong to an inactive parent."));
        }
        if (request.CategoryId is { } existingCategoryId && request.ParentId is { } requestedParentId &&
            await CreatesCategoryCycleAsync(existingCategoryId, requestedParentId, cancellationToken))
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_CYCLE",
                "The category parent would create a cycle."));
        }
        if (request.CategoryId is { } categoryIdToDisable && !request.IsActive &&
            await dbContext.Categories.AnyAsync(
                candidate => candidate.ParentId == categoryIdToDisable && candidate.IsActive,
                cancellationToken))
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.ACTIVE_CHILDREN",
                "Deactivate or move active child categories first."));
        }

        if (category is null)
        {
            if (await dbContext.Categories.AnyAsync(candidate => candidate.Code == request.Code, cancellationToken))
            {
                return Result.Failure<CategoryResponse>(ResultError.Conflict("CATEGORY.CODE_EXISTS", "The category code already exists."));
            }
            category = Category.Create(request.Code, request.ParentId, request.DisplayOrder, now);
            dbContext.Categories.Add(category);
        }
        else
        {
            category.Update(request.ParentId, request.DisplayOrder, request.IsActive, now);
        }
        if (request.CategoryId is null && !request.IsActive)
        {
            category.Update(request.ParentId, request.DisplayOrder, false, now);
        }

        List<CategoryLocalization> existing = await dbContext.CategoryLocalizations
            .Where(localization => localization.CategoryId == category.Id)
            .ToListAsync(cancellationToken);
        foreach (TaxonomyLocalizationInput input in request.Localizations)
        {
            string locale = InfrastructureHelpers.NormalizeLocale(input.Locale);
            CategoryLocalization? localization = existing.SingleOrDefault(candidate => candidate.Locale == locale);
            if (localization is null)
            {
                dbContext.CategoryLocalizations.Add(CategoryLocalization.Create(category.Id, locale, input.Name));
            }
            else
            {
                localization.Rename(input.Name);
            }
        }

        InfrastructureHelpers.AddAudit(dbContext, request.UserId, "catalog.category-upserted", "Category", category.Id, null, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapCategory(category, request.Localizations.Select(input =>
            new TaxonomyLocalizationResponse(InfrastructureHelpers.NormalizeLocale(input.Locale), input.Name.Trim())).ToArray()));
    }

    public async Task<Result<TagResponse>> UpsertTagAsync(
        UpsertTagCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Tag? tag = request.TagId is { } id
            ? await dbContext.Tags.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (request.TagId is not null && tag is null)
        {
            return Result.Failure<TagResponse>(ResultError.NotFound("TAG.NOT_FOUND", "The tag was not found."));
        }
        if (tag is not null && !string.Equals(tag.Code, request.Code, StringComparison.Ordinal))
        {
            return Result.Failure<TagResponse>(ResultError.Conflict("TAG.CODE_IMMUTABLE", "A tag code cannot be changed."));
        }
        if (tag is null)
        {
            if (await dbContext.Tags.AnyAsync(candidate => candidate.Code == request.Code, cancellationToken))
            {
                return Result.Failure<TagResponse>(ResultError.Conflict("TAG.CODE_EXISTS", "The tag code already exists."));
            }
            tag = Tag.Create(request.Code, now);
            dbContext.Tags.Add(tag);
        }
        tag.SetActive(request.IsActive, now);

        List<TagLocalization> existing = await dbContext.TagLocalizations
            .Where(localization => localization.TagId == tag.Id)
            .ToListAsync(cancellationToken);
        foreach (TaxonomyLocalizationInput input in request.Localizations)
        {
            string locale = InfrastructureHelpers.NormalizeLocale(input.Locale);
            TagLocalization? localization = existing.SingleOrDefault(candidate => candidate.Locale == locale);
            if (localization is null)
            {
                dbContext.TagLocalizations.Add(TagLocalization.Create(tag.Id, locale, input.Name));
            }
            else
            {
                localization.Rename(input.Name);
            }
        }

        InfrastructureHelpers.AddAudit(dbContext, request.UserId, "catalog.tag-upserted", "Tag", tag.Id, null, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTag(tag, request.Localizations.Select(input =>
            new TaxonomyLocalizationResponse(InfrastructureHelpers.NormalizeLocale(input.Locale), input.Name.Trim())).ToArray()));
    }

    public async Task<Result<PagedResponse<CatalogCourseResponse>>> GetCatalogAsync(
        GetCatalogCoursesQuery request,
        CancellationToken cancellationToken)
    {
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        string sort = NormalizeCatalogSort(request.Sort);
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 24);
        string canonical = CanonicalPublicQuery(locale, string.Empty, request.Filters, sort, limit);
        if (!cursorCodec.TryRead(request.Cursor, "catalog", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<CatalogCourseResponse>>();
        }

        IQueryable<CatalogDocument> query = ApplyCatalogFilters(ActiveCatalogDocuments(locale), request.Filters);
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(document =>
                document.PublishedAt < timestamp ||
                document.PublishedAt == timestamp && document.ReleaseId.CompareTo(id) < 0);
        }
        List<CatalogDocument> documents = await query
            .OrderByDescending(document => document.PublishedAt)
            .ThenByDescending(document => document.ReleaseId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = documents.Count > limit;
        List<CatalogDocument> items = documents.Take(limit).ToList();
        CatalogReleaseParts parts = await LoadCatalogReleasePartsAsync(
            items.Select(document => document.ReleaseId).ToArray(),
            cancellationToken);
        string? nextCursor = hasMore
            ? cursorCodec.Create("catalog", canonical, items[^1].PublishedAt, items[^1].ReleaseId)
            : null;
        return Result.Success(new PagedResponse<CatalogCourseResponse>(
            items.Select(document => MapCatalog(document, parts)).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<PublicCourseDetailResponse>> GetPublicCourseAsync(
        GetPublicCourseQuery request,
        CancellationToken cancellationToken)
    {
        Result<PublicCourseLookupResponse> lookup = await ResolveAsync(
            new ResolvePublicCourseQuery(request.Locale, request.Slug),
            cancellationToken);
        return lookup.IsSuccess && lookup.Value.Course is { } course
            ? Result.Success(course)
            : Result.Failure<PublicCourseDetailResponse>(ResultError.NotFound(
            "CATALOG.COURSE_NOT_FOUND",
            "The published course was not found."));
    }

    public async Task<Result<SearchPageResponse>> SearchAsync(
        SearchCoursesQuery request,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        string normalizedQuery = SearchTextNormalizer.Normalize(request.Query, locale);
        string sort = NormalizeSearchSort(request.Sort, normalizedQuery.Length == 0);
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 20);
        string canonical = CanonicalPublicQuery(locale, normalizedQuery, request.Filters, sort, limit);
        if (!cursorCodec.TryRead(request.Cursor, "search", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return InfrastructureHelpers.CursorFailure<SearchPageResponse>();
        }

        IQueryable<CatalogDocument> query = ApplyCatalogFilters(ActiveCatalogDocuments(locale), request.Filters);
        if (normalizedQuery.Length > 0)
        {
            query = locale == "ar"
                ? query.Where(document => document.NormalizedArabicText.Contains(normalizedQuery))
                : query.Where(document => document.SearchText.Contains(normalizedQuery));
        }
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(document =>
                document.PublishedAt < timestamp ||
                document.PublishedAt == timestamp && document.ReleaseId.CompareTo(id) < 0);
        }
        List<CatalogDocument> documents = await query
            .OrderByDescending(document => document.PublishedAt)
            .ThenByDescending(document => document.ReleaseId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = documents.Count > limit;
        List<CatalogDocument> items = documents.Take(limit).ToList();
        CatalogReleaseParts parts = await LoadCatalogReleasePartsAsync(
            items.Select(document => document.ReleaseId).ToArray(),
            cancellationToken);
        string? nextCursor = hasMore
            ? cursorCodec.Create("search", canonical, items[^1].PublishedAt, items[^1].ReleaseId)
            : null;
        var response = new SearchPageResponse(
            items.Select(document => MapSearch(document, parts, request.Query)).ToArray(),
            nextCursor,
            hasMore,
            null);
        searchTelemetry.Record(
            locale,
            normalizedQuery,
            response.Items.Count,
            Stopwatch.GetElapsedTime(started),
            sort,
            request.Filters);
        return Result.Success(response);
    }

    public async Task<Result<IReadOnlyList<PublicSearchSuggestionResponse>>> SuggestionsAsync(
        SuggestCourseSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        string normalized = SearchTextNormalizer.Normalize(request.Query, locale);
        if (normalized.Length < 2)
        {
            return Result.Success<IReadOnlyList<PublicSearchSuggestionResponse>>([]);
        }
        IQueryable<CatalogDocument> query = ActiveCatalogDocuments(locale);
        query = locale == "ar"
            ? query.Where(document => document.NormalizedArabicText.Contains(normalized))
            : query.Where(document => document.SearchText.Contains(normalized));
        List<CatalogDocument> candidates = await query
            .OrderByDescending(document => document.PublishedAt)
            .ThenByDescending(document => document.ReleaseId)
            .Take(request.Limit * 4)
            .ToListAsync(cancellationToken);
        PublicSearchSuggestionResponse[] suggestions = candidates
            .DistinctBy(document => document.Title, StringComparer.OrdinalIgnoreCase)
            .Take(request.Limit)
            .Select(document => new PublicSearchSuggestionResponse(
                document.Slug,
                Highlight(document.Title, request.Query)))
            .ToArray();
        return Result.Success<IReadOnlyList<PublicSearchSuggestionResponse>>(suggestions);
    }

    // IPublicCatalogPort

    public async Task<Result<PublicCourseLookupResponse>> ResolveAsync(
        ResolvePublicCourseQuery request,
        CancellationToken cancellationToken)
    {
        string locale = InfrastructureHelpers.NormalizeLocale(request.Locale);
        string slug = request.Slug.Trim().ToLowerInvariant();
        CatalogDocument? active = await ActiveCatalogDocuments(locale)
            .SingleOrDefaultAsync(document => document.Slug == slug, cancellationToken);
        if (active is not null)
        {
            CatalogReleaseParts parts = await LoadCatalogReleasePartsAsync([active.ReleaseId], cancellationToken);
            PublicCourseDetailResponse detail = await MapDetailAsync(active, parts, cancellationToken);
            return Result.Success(new PublicCourseLookupResponse(detail, null));
        }

        Guid? historicalCourseId = await dbContext.CatalogDocuments.AsNoTracking()
            .Where(document => document.Locale == locale && document.Slug == slug)
            .Select(document => (Guid?)document.CourseId)
            .FirstOrDefaultAsync(cancellationToken);
        if (historicalCourseId is not { } courseId)
        {
            return Result.Failure<PublicCourseLookupResponse>(ResultError.NotFound(
                "CATALOG.COURSE_NOT_FOUND",
                "The published course was not found."));
        }
        CatalogDocument? current = await ActiveCatalogDocuments(locale)
            .SingleOrDefaultAsync(document => document.CourseId == courseId, cancellationToken);
        return current is null
            ? Result.Failure<PublicCourseLookupResponse>(ResultError.NotFound(
                "CATALOG.COURSE_NOT_FOUND",
                "The published course was not found."))
            : Result.Success(new PublicCourseLookupResponse(null, current.Slug));
    }

    // Private query helpers

    private IQueryable<CatalogDocument> ActiveCatalogDocuments(string locale) =>
        from document in dbContext.CatalogDocuments.AsNoTracking()
        join course in dbContext.Courses.AsNoTracking() on document.CourseId equals course.Id
        where document.Locale == locale && course.DeletedAt == null && course.ActiveReleaseId == document.ReleaseId
        select document;

    private IQueryable<CatalogDocument> ApplyCatalogFilters(
        IQueryable<CatalogDocument> query,
        CatalogFilterContract filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Price) &&
            !string.Equals(filters.Price.Trim(), "paid", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(_ => false);
        }
        if (!string.IsNullOrWhiteSpace(filters.CategoryCode))
        {
            string code = filters.CategoryCode.Trim().ToLowerInvariant();
            query = query.Where(document => dbContext.CourseReleaseTaxonomies.Any(term =>
                term.ReleaseId == document.ReleaseId && term.IsCategory && term.Code == code));
        }
        if (!string.IsNullOrWhiteSpace(filters.Tag))
        {
            string code = filters.Tag.Trim().ToLowerInvariant();
            query = query.Where(document => dbContext.CourseReleaseTaxonomies.Any(term =>
                term.ReleaseId == document.ReleaseId && !term.IsCategory && term.Code == code));
        }
        if (!string.IsNullOrWhiteSpace(filters.Language))
        {
            string language = filters.Language.Trim().ToLowerInvariant();
            query = query.Where(document => document.Language == language);
        }
        if (!string.IsNullOrWhiteSpace(filters.Level))
        {
            string level = InfrastructureHelpers.NormalizeLevel(filters.Level);
            query = query.Where(document => document.Level == level);
        }
        if (!string.IsNullOrWhiteSpace(filters.Instructor))
        {
            string instructor = filters.Instructor.Trim();
            query = query.Where(document => dbContext.CourseReleaseInstructors.Any(item =>
                item.ReleaseId == document.ReleaseId && item.DisplayName.Contains(instructor)));
        }
        if (!string.IsNullOrWhiteSpace(filters.Duration))
        {
            string duration = filters.Duration.Trim().ToLowerInvariant();
            query = duration switch
            {
                "under-60" or "short" => query.Where(document => document.DurationMinutes < 60),
                "60-120" or "medium" => query.Where(document => document.DurationMinutes >= 60 && document.DurationMinutes <= 120),
                "over-120" or "long" => query.Where(document => document.DurationMinutes > 120),
                _ => query.Where(_ => false),
            };
        }
        return query;
    }

    private async Task<CatalogReleaseParts> LoadCatalogReleasePartsAsync(
        Guid[] releaseIds,
        CancellationToken cancellationToken)
    {
        if (releaseIds.Length == 0)
        {
            return new CatalogReleaseParts(
                new Dictionary<Guid, List<PublicInstructorResponse>>(),
                new Dictionary<Guid, List<ReleaseTaxonomyItem>>());
        }
        List<CourseReleaseInstructor> instructors = await dbContext.CourseReleaseInstructors.AsNoTracking()
            .Where(instructor => releaseIds.Contains(instructor.ReleaseId))
            .OrderBy(instructor => instructor.Position)
            .ToListAsync(cancellationToken);
        List<CourseReleaseTaxonomy> taxonomy = await dbContext.CourseReleaseTaxonomies.AsNoTracking()
            .Where(term => releaseIds.Contains(term.ReleaseId))
            .OrderBy(term => term.IsCategory ? 0 : 1)
            .ThenBy(term => term.Code)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<PublicInstructorResponse>> instructorMap = instructors
            .GroupBy(instructor => instructor.ReleaseId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(instructor => new PublicInstructorResponse(
                    instructor.UserId,
                    instructor.DisplayName)).ToList());
        Dictionary<Guid, List<ReleaseTaxonomyItem>> taxonomyMap = taxonomy
            .GroupBy(term => term.ReleaseId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(term => new ReleaseTaxonomyItem(
                    term.TermId,
                    term.Code,
                    term.Name,
                    term.IsCategory)).ToList());
        return new CatalogReleaseParts(instructorMap, taxonomyMap);
    }

    private async Task<bool> CreatesCategoryCycleAsync(
        Guid categoryId,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        Guid? current = parentId;
        while (current is { } currentId)
        {
            if (currentId == categoryId)
            {
                return true;
            }

            current = await dbContext.Categories.AsNoTracking()
                .Where(category => category.Id == currentId)
                .Select(category => category.ParentId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    private async Task<Dictionary<Guid, List<TaxonomyLocalizationResponse>>> LoadCategoryLocalizationsAsync(
        Guid[] ids,
        CancellationToken cancellationToken) =>
        (await dbContext.CategoryLocalizations.AsNoTracking()
            .Where(localization => ids.Contains(localization.CategoryId))
            .OrderBy(localization => localization.Locale)
            .ToListAsync(cancellationToken))
        .GroupBy(localization => localization.CategoryId)
        .ToDictionary(
            group => group.Key,
            group => group.Select(localization => new TaxonomyLocalizationResponse(localization.Locale, localization.Name)).ToList());

    private async Task<Dictionary<Guid, List<TaxonomyLocalizationResponse>>> LoadTagLocalizationsAsync(
        Guid[] ids,
        CancellationToken cancellationToken) =>
        (await dbContext.TagLocalizations.AsNoTracking()
            .Where(localization => ids.Contains(localization.TagId))
            .OrderBy(localization => localization.Locale)
            .ToListAsync(cancellationToken))
        .GroupBy(localization => localization.TagId)
        .ToDictionary(
            group => group.Key,
            group => group.Select(localization => new TaxonomyLocalizationResponse(localization.Locale, localization.Name)).ToList());

    private static CatalogCourseResponse MapCatalog(CatalogDocument document, CatalogReleaseParts parts)
    {
        List<ReleaseTaxonomyItem> terms = parts.Taxonomy.GetValueOrDefault(document.ReleaseId) ?? [];
        return new CatalogCourseResponse(
            document.CourseId,
            document.ReleaseId,
            document.Slug,
            document.Title,
            document.Summary,
            document.Language,
            InfrastructureHelpers.PublicLevel(document.Level),
            document.DurationMinutes,
            parts.Instructors.GetValueOrDefault(document.ReleaseId) ?? [],
            terms.Where(term => term.IsCategory)
                .Select(term => new PublicTaxonomyTermResponse(term.Id, term.Code, term.Name))
                .ToArray(),
            terms.Where(term => !term.IsCategory)
                .Select(term => new PublicTaxonomyTermResponse(term.Id, term.Code, term.Name))
                .ToArray(),
            new PublicCoursePriceResponse("paid", "100", "DEMO"));
    }

    private async Task<PublicCourseDetailResponse> MapDetailAsync(
        CatalogDocument document,
        CatalogReleaseParts parts,
        CancellationToken cancellationToken)
    {
        CourseRelease release = await dbContext.CourseReleases.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == document.ReleaseId, cancellationToken);
        PublicCourseLocalizationResponse[] localizations = await dbContext.CourseReleaseLocalizations.AsNoTracking()
            .Where(localization => localization.ReleaseId == document.ReleaseId)
            .OrderBy(localization => localization.Locale)
            .Select(localization => new PublicCourseLocalizationResponse(localization.Locale, localization.Slug))
            .ToArrayAsync(cancellationToken);
        CatalogCourseResponse summary = MapCatalog(document, parts);
        return new PublicCourseDetailResponse(
            summary.CourseId,
            summary.ReleaseId,
            summary.Slug,
            summary.Title,
            summary.Summary,
            summary.Language,
            summary.Level,
            summary.DurationMinutes,
            summary.Instructors,
            summary.Categories,
            summary.Tags,
            summary.Price,
            document.Locale,
            release.DefaultLocale,
            document.Description,
            [],
            localizations);
    }

    private static SearchCourseResponse MapSearch(
        CatalogDocument document,
        CatalogReleaseParts parts,
        string query)
    {
        CatalogCourseResponse course = MapCatalog(document, parts);
        return new SearchCourseResponse(
            course.CourseId,
            course.ReleaseId,
            course.Slug,
            course.Title,
            course.Summary,
            course.Language,
            course.Level,
            course.DurationMinutes,
            course.Instructors,
            course.Categories,
            course.Tags,
            course.Price,
            Highlight(course.Title, query),
            Highlight(course.Summary, query));
    }

    private static HighlightSegment[] Highlight(string value, string query)
    {
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return [new HighlightSegment(value, false)];
        }
        int index = value.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? [new HighlightSegment(value, false)]
            : new HighlightSegment[]
            {
                new HighlightSegment(value[..index], false),
                new HighlightSegment(value.Substring(index, normalizedQuery.Length), true),
                new HighlightSegment(value[(index + normalizedQuery.Length)..], false),
            }.Where(segment => segment.Text.Length > 0).ToArray();
    }

    private static CategoryResponse MapCategory(Category category, IReadOnlyList<TaxonomyLocalizationResponse> localizations) => new(
        category.Id,
        category.Code,
        category.ParentId,
        category.DisplayOrder,
        category.IsActive,
        localizations);

    private static TagResponse MapTag(Tag tag, IReadOnlyList<TaxonomyLocalizationResponse> localizations) => new(
        tag.Id,
        tag.Code,
        tag.IsActive,
        localizations);

    private static string NormalizeCatalogSort(string sort) => sort.Trim().ToLowerInvariant() switch
    {
        "" or "newest" => "newest",
        "title" => "title",
        "popular" => "popular",
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    private static string NormalizeSearchSort(string sort, bool blankQuery) => sort.Trim().ToLowerInvariant() switch
    {
        "" when blankQuery => "newest",
        "" => "relevance",
        "relevance" when !blankQuery => "relevance",
        "newest" => "newest",
        "title" => "title",
        "popular" => "popular",
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    private static string CanonicalPublicQuery(
        string locale,
        string query,
        CatalogFilterContract filters,
        string sort,
        int limit) => string.Join('|',
            "v1",
            locale,
            query,
            filters.CategoryCode?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Tag?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Language?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Level?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Price?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Duration?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Instructor?.Trim().ToLowerInvariant() ?? string.Empty,
            sort,
            limit.ToString(CultureInfo.InvariantCulture));

    private sealed record ReleaseTaxonomyItem(Guid Id, string Code, string Name, bool IsCategory);

    private sealed record CatalogReleaseParts(
        IReadOnlyDictionary<Guid, List<PublicInstructorResponse>> Instructors,
        IReadOnlyDictionary<Guid, List<ReleaseTaxonomyItem>> Taxonomy);
}
