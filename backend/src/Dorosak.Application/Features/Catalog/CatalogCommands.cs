using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Catalog;

public sealed record GetCategoriesQuery(string Locale, int Limit, string? Cursor, bool IncludeInactive = false)
    : IQuery<PagedResponse<CategoryResponse>>;

public sealed record GetTagsQuery(string Locale, int Limit, string? Cursor, bool IncludeInactive = false)
    : IQuery<PagedResponse<TagResponse>>;

public sealed record TaxonomyLocalizationInput(string Locale, string Name);

public sealed record UpsertCategoryCommand(
    Guid UserId,
    Guid? CategoryId,
    string Code,
    Guid? ParentId,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationInput> Localizations) : ITransactionalCommand<CategoryResponse>;

public sealed record UpsertTagCommand(
    Guid UserId,
    Guid? TagId,
    string Code,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationInput> Localizations) : ITransactionalCommand<TagResponse>;

public sealed record GetCatalogCoursesQuery(
    string Locale,
    CatalogFilterContract Filters,
    string Sort,
    int Limit,
    string? Cursor) : IQuery<PagedResponse<CatalogCourseResponse>>;

public sealed record GetPublicCourseQuery(string Locale, string Slug) : IQuery<PublicCourseDetailResponse>;

public sealed record SearchCoursesQuery(
    string Locale,
    string Query,
    CatalogFilterContract Filters,
    string Sort,
    int Limit,
    string? Cursor) : IQuery<SearchPageResponse>;

public sealed record SuggestCourseSuggestionsQuery(string Locale, string Query, int Limit)
    : IQuery<IReadOnlyList<PublicSearchSuggestionResponse>>;

public sealed record CategoryResponse(
    Guid Id,
    string Code,
    Guid? ParentId,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TagResponse(
    Guid Id,
    string Code,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TaxonomyLocalizationResponse(string Locale, string Name);

public sealed record CatalogCourseResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price);

public sealed record PublicInstructorResponse(Guid Id, string DisplayName);
public sealed record PublicTaxonomyTermResponse(Guid Id, string Code, string Name);
public sealed record PublicCoursePriceResponse(string Type, string? Amount, string? Currency);
public sealed record PublicCourseLocalizationResponse(string Locale, string Slug);

public sealed record PublicCourseDetailResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price,
    string Locale,
    string DefaultLocale,
    string Description,
    IReadOnlyList<string> LearningOutcomes,
    IReadOnlyList<PublicCourseLocalizationResponse> Localizations);

public sealed record HighlightSegment(string Text, bool Matched);

public sealed record SearchCourseResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price,
    IReadOnlyList<HighlightSegment> TitleHighlight,
    IReadOnlyList<HighlightSegment> SummaryHighlight);

public sealed record SearchPageResponse(
    IReadOnlyList<SearchCourseResponse> Items,
    string? NextCursor,
    bool HasMore,
    string? Correction);

public sealed record PublicSearchSuggestionResponse(
    string? Slug,
    IReadOnlyList<HighlightSegment> Segments);

public sealed record CatalogFilterContract(
    string? CategoryCode = null,
    string? Tag = null,
    string? Language = null,
    string? Level = null,
    string? Price = null,
    string? Duration = null,
    string? Instructor = null);

public interface ICatalogService
{
    Task<Result<CategoryResponse>> UpsertCategoryAsync(UpsertCategoryCommand request, CancellationToken cancellationToken);
    Task<Result<TagResponse>> UpsertTagAsync(UpsertTagCommand request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<CategoryResponse>>> GetCategoriesAsync(GetCategoriesQuery request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<TagResponse>>> GetTagsAsync(GetTagsQuery request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<CatalogCourseResponse>>> GetCatalogAsync(GetCatalogCoursesQuery request, CancellationToken cancellationToken);
    Task<Result<PublicCourseDetailResponse>> GetPublicCourseAsync(GetPublicCourseQuery request, CancellationToken cancellationToken);
    Task<Result<SearchPageResponse>> SearchAsync(SearchCoursesQuery request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PublicSearchSuggestionResponse>>> SuggestionsAsync(SuggestCourseSuggestionsQuery request, CancellationToken cancellationToken);
}

internal sealed class CatalogCommandHandler(ICatalogService service)
    : IRequestHandler<UpsertCategoryCommand, Result<CategoryResponse>>,
      IRequestHandler<UpsertTagCommand, Result<TagResponse>>
{
    public Task<Result<CategoryResponse>> Handle(UpsertCategoryCommand request, CancellationToken cancellationToken) => service.UpsertCategoryAsync(request, cancellationToken);
    public Task<Result<TagResponse>> Handle(UpsertTagCommand request, CancellationToken cancellationToken) => service.UpsertTagAsync(request, cancellationToken);
}

internal sealed class CatalogQueryHandler(ICatalogService service)
    : IRequestHandler<GetCategoriesQuery, Result<PagedResponse<CategoryResponse>>>,
      IRequestHandler<GetTagsQuery, Result<PagedResponse<TagResponse>>>,
      IRequestHandler<GetCatalogCoursesQuery, Result<PagedResponse<CatalogCourseResponse>>>,
      IRequestHandler<GetPublicCourseQuery, Result<PublicCourseDetailResponse>>,
      IRequestHandler<SearchCoursesQuery, Result<SearchPageResponse>>,
      IRequestHandler<SuggestCourseSuggestionsQuery, Result<IReadOnlyList<PublicSearchSuggestionResponse>>>
{
    public Task<Result<PagedResponse<CategoryResponse>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken) => service.GetCategoriesAsync(request, cancellationToken);
    public Task<Result<PagedResponse<TagResponse>>> Handle(GetTagsQuery request, CancellationToken cancellationToken) => service.GetTagsAsync(request, cancellationToken);
    public Task<Result<PagedResponse<CatalogCourseResponse>>> Handle(GetCatalogCoursesQuery request, CancellationToken cancellationToken) => service.GetCatalogAsync(request, cancellationToken);
    public Task<Result<PublicCourseDetailResponse>> Handle(GetPublicCourseQuery request, CancellationToken cancellationToken) => service.GetPublicCourseAsync(request, cancellationToken);
    public Task<Result<SearchPageResponse>> Handle(SearchCoursesQuery request, CancellationToken cancellationToken) => service.SearchAsync(request, cancellationToken);
    public Task<Result<IReadOnlyList<PublicSearchSuggestionResponse>>> Handle(SuggestCourseSuggestionsQuery request, CancellationToken cancellationToken) => service.SuggestionsAsync(request, cancellationToken);
}


