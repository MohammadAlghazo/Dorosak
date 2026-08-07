using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Phase6;

public sealed record SubmitTeacherApplicationCommand(
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    string Motivation) : ITransactionalCommand<TeacherApplicationResponse>;

public sealed record GetTeacherApplicationQuery(Guid UserId) : IQuery<TeacherApplicationResponse>;

public sealed record WithdrawTeacherApplicationCommand(Guid UserId) : ITransactionalCommand<TeacherApplicationResponse>;

public sealed record GetTeacherApplicationsQuery(int Limit, string? Cursor) : IQuery<PagedResponse<TeacherApplicationResponse>>;

public sealed record ReviewTeacherApplicationCommand(
    Guid ReviewerUserId,
    Guid ApplicationId,
    string Decision,
    string? Reason) : ITransactionalCommand<TeacherApplicationResponse>;

public sealed record CourseLocalizationInput(
    string Locale,
    string Title,
    string Subtitle,
    string Description,
    string? Slug = null);

public sealed record CreateCourseCommand(
    Guid UserId,
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> TagCodes) : ITransactionalCommand<CourseMutationResponse>;

public sealed record GetInstructorCoursesQuery(Guid UserId, int Limit, string? Cursor)
    : IQuery<PagedResponse<CourseSummaryResponse>>;

public sealed record GetCourseQuery(Guid UserId, Guid CourseId)
    : IQuery<CourseDetailsResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.View;
}

public sealed record UpdateCourseMetadataCommand(
    Guid UserId,
    Guid CourseId,
    long? ExpectedVersion,
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> TagCodes) : ITransactionalCommand<CourseMutationResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Edit;
}

public sealed record ArchiveCourseCommand(
    Guid UserId,
    Guid CourseId,
    string Reason) : ITransactionalCommand<CourseMutationResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record LessonInput(
    Guid? Id,
    int Position,
    string Title,
    string LessonType,
    string Content);

public sealed record SectionInput(
    Guid? Id,
    int Position,
    string Title,
    IReadOnlyList<LessonInput> Lessons);

public sealed record GetCurriculumQuery(Guid UserId, Guid CourseId)
    : IQuery<CurriculumResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.View;
}

public sealed record UpdateCurriculumCommand(
    Guid UserId,
    Guid CourseId,
    long? ExpectedVersion,
    IReadOnlyList<SectionInput> Sections) : ITransactionalCommand<CourseMutationResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Edit;
}

public sealed record AddCollaboratorCommand(
    Guid UserId,
    Guid CourseId,
    Guid CollaboratorUserId,
    string Role) : ITransactionalCommand<CourseCollaboratorResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record RemoveCollaboratorCommand(
    Guid UserId,
    Guid CourseId,
    Guid CollaboratorUserId) : ITransactionalCommand<OperationCompleted>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record RequestPublicationCommand(Guid UserId, Guid CourseId)
    : ITransactionalCommand<PublicationStatusResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record GetPublicationStatusQuery(Guid UserId, Guid CourseId)
    : IQuery<PublicationStatusResponse>, IPhase6AuthorizedRequest
{
    Guid IPhase6AuthorizedRequest.UserId => UserId;

    Guid IPhase6AuthorizedRequest.CourseId => CourseId;

    CourseAccess IPhase6AuthorizedRequest.Access => CourseAccess.View;
}

public sealed record GetPublicationReviewsQuery(int Limit, string? Cursor)
    : IQuery<PagedResponse<PublicationReviewResponse>>;

public sealed record ReviewPublicationCommand(
    Guid ReviewerUserId,
    Guid ReviewId,
    string Decision,
    string? Reason) : ITransactionalCommand<PublicationReviewResponse>;

public sealed record GetCategoriesQuery(string Locale, int Limit, string? Cursor)
    : IQuery<PagedResponse<CategoryResponse>>;

public sealed record GetTagsQuery(string Locale, int Limit, string? Cursor)
    : IQuery<PagedResponse<TagResponse>>;

public sealed record TaxonomyLocalizationInput(string Locale, string Name);

public sealed record UpsertCategoryCommand(
    Guid? CategoryId,
    string Code,
    Guid? ParentId,
    int DisplayOrder,
    IReadOnlyList<TaxonomyLocalizationInput> Localizations) : ITransactionalCommand<CategoryResponse>;

public sealed record UpsertTagCommand(
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

public sealed record GetPublicCourseQuery(string Locale, string Slug) : IQuery<CatalogCourseResponse>;

public sealed record SearchCoursesQuery(
    string Locale,
    string Query,
    CatalogFilterContract Filters,
    string Sort,
    int Limit,
    string? Cursor) : IQuery<PagedResponse<SearchCourseResponse>>;

public sealed record SuggestCourseSuggestionsQuery(string Locale, string Query) : IQuery<IReadOnlyList<string>>;

internal sealed class Phase6CommandHandler<TRequest, TResponse>(IPhase6Service service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, MediatR.IRequest<Result<TResponse>>
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) =>
        request switch
        {
            SubmitTeacherApplicationCommand command => Cast(service.SubmitTeacherApplicationAsync(command, cancellationToken)),
            WithdrawTeacherApplicationCommand command => Cast(service.WithdrawTeacherApplicationAsync(command, cancellationToken)),
            ReviewTeacherApplicationCommand command => Cast(service.ReviewTeacherApplicationAsync(command, cancellationToken)),
            CreateCourseCommand command => Cast(service.CreateCourseAsync(command, cancellationToken)),
            UpdateCourseMetadataCommand command => Cast(service.UpdateCourseMetadataAsync(command, cancellationToken)),
            ArchiveCourseCommand command => Cast(service.ArchiveCourseAsync(command, cancellationToken)),
            UpdateCurriculumCommand command => Cast(service.UpdateCurriculumAsync(command, cancellationToken)),
            AddCollaboratorCommand command => Cast(service.AddCollaboratorAsync(command, cancellationToken)),
            RemoveCollaboratorCommand command => Cast(service.RemoveCollaboratorAsync(command, cancellationToken)),
            RequestPublicationCommand command => Cast(service.RequestPublicationAsync(command, cancellationToken)),
            ReviewPublicationCommand command => Cast(service.ReviewPublicationAsync(command, cancellationToken)),
            UpsertCategoryCommand command => Cast(service.UpsertCategoryAsync(command, cancellationToken)),
            UpsertTagCommand command => Cast(service.UpsertTagAsync(command, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported Phase 6 command {typeof(TRequest).Name}.")
        };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value)
            : Result.Failure<TResponse>(result.Failure);
    }
}

internal sealed class Phase6QueryHandler<TRequest, TResponse>(IPhase6Service service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, MediatR.IRequest<Result<TResponse>>
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) =>
        request switch
        {
            GetTeacherApplicationQuery query => Cast(service.GetTeacherApplicationAsync(query, cancellationToken)),
            GetTeacherApplicationsQuery query => Cast(service.GetTeacherApplicationsAsync(query, cancellationToken)),
            GetInstructorCoursesQuery query => Cast(service.GetInstructorCoursesAsync(query, cancellationToken)),
            GetCourseQuery query => Cast(service.GetCourseAsync(query, cancellationToken)),
            GetCurriculumQuery query => Cast(service.GetCurriculumAsync(query, cancellationToken)),
            GetPublicationStatusQuery query => Cast(service.GetPublicationStatusAsync(query, cancellationToken)),
            GetPublicationReviewsQuery query => Cast(service.GetPublicationReviewsAsync(query, cancellationToken)),
            GetCategoriesQuery query => Cast(service.GetCategoriesAsync(query, cancellationToken)),
            GetTagsQuery query => Cast(service.GetTagsAsync(query, cancellationToken)),
            GetCatalogCoursesQuery query => Cast(service.GetCatalogAsync(query, cancellationToken)),
            GetPublicCourseQuery query => Cast(service.GetPublicCourseAsync(query, cancellationToken)),
            SearchCoursesQuery query => Cast(service.SearchAsync(query, cancellationToken)),
            SuggestCourseSuggestionsQuery query => Cast(service.SuggestionsAsync(query, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported Phase 6 query {typeof(TRequest).Name}.")
        };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value)
            : Result.Failure<TResponse>(result.Failure);
    }
}
