using AutoMapper;
using Dorosak.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        string? mediatRLicenseKey,
        string? autoMapperLicenseKey)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            configuration.LicenseKey = mediatRLicenseKey;
            configuration.AddOpenBehavior(typeof(TelemetryBehavior<,>));
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            configuration.AddOpenBehavior(typeof(QueryCacheBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
            configuration.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        AddPhase6Handlers(services);
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.GetCourseQuery>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetCourseQuery>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.UpdateCourseMetadataCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.UpdateCourseMetadataCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.ArchiveCourseCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.ArchiveCourseCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.GetCurriculumQuery>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetCurriculumQuery>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.UpdateCurriculumCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.UpdateCurriculumCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.AddCollaboratorCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.AddCollaboratorCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.RemoveCollaboratorCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.RemoveCollaboratorCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.TransferCourseOwnershipCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.TransferCourseOwnershipCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.RequestPublicationCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.RequestPublicationCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.WithdrawPublicationCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.WithdrawPublicationCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.GetPublicationStatusQuery>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetPublicationStatusQuery>>();
        services.AddAutoMapper(
            configuration => configuration.LicenseKey = autoMapperLicenseKey,
            AssemblyReference.Assembly);

        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static void AddPhase6Handlers(IServiceCollection services)
    {
        AddCommandHandler<Features.Phase6.SubmitTeacherApplicationCommand, Features.Phase6.TeacherApplicationResponse>(services);
        AddCommandHandler<Features.Phase6.WithdrawTeacherApplicationCommand, Features.Phase6.TeacherApplicationResponse>(services);
        AddCommandHandler<Features.Phase6.ReviewTeacherApplicationCommand, Features.Phase6.TeacherApplicationResponse>(services);
        AddCommandHandler<Features.Phase6.CreateCourseCommand, Features.Phase6.CourseMutationResponse>(services);
        AddCommandHandler<Features.Phase6.UpdateCourseMetadataCommand, Features.Phase6.CourseMutationResponse>(services);
        AddCommandHandler<Features.Phase6.ArchiveCourseCommand, Features.Phase6.CourseMutationResponse>(services);
        AddCommandHandler<Features.Phase6.UpdateCurriculumCommand, Features.Phase6.CourseMutationResponse>(services);
        AddCommandHandler<Features.Phase6.AddCollaboratorCommand, Features.Phase6.CourseCollaboratorResponse>(services);
        AddCommandHandler<Features.Phase6.RemoveCollaboratorCommand, Features.Phase6.OperationCompleted>(services);
        AddCommandHandler<Features.Phase6.TransferCourseOwnershipCommand, Features.Phase6.CourseMutationResponse>(services);
        AddCommandHandler<Features.Phase6.RequestPublicationCommand, Features.Phase6.PublicationStatusResponse>(services);
        AddCommandHandler<Features.Phase6.WithdrawPublicationCommand, Features.Phase6.PublicationStatusResponse>(services);
        AddCommandHandler<Features.Phase6.ReviewPublicationCommand, Features.Phase6.PublicationReviewResponse>(services);
        AddCommandHandler<Features.Phase6.UpsertCategoryCommand, Features.Phase6.CategoryResponse>(services);
        AddCommandHandler<Features.Phase6.UpsertTagCommand, Features.Phase6.TagResponse>(services);

        AddQueryHandler<Features.Phase6.GetTeacherApplicationQuery, Features.Phase6.TeacherApplicationResponse>(services);
        AddQueryHandler<Features.Phase6.GetTeacherApplicationsQuery, Features.Phase6.PagedResponse<Features.Phase6.TeacherApplicationResponse>>(services);
        AddQueryHandler<Features.Phase6.GetInstructorCoursesQuery, Features.Phase6.PagedResponse<Features.Phase6.CourseSummaryResponse>>(services);
        AddQueryHandler<Features.Phase6.GetCourseQuery, Features.Phase6.CourseDetailsResponse>(services);
        AddQueryHandler<Features.Phase6.GetCurriculumQuery, Features.Phase6.CurriculumResponse>(services);
        AddQueryHandler<Features.Phase6.GetPublicationStatusQuery, Features.Phase6.PublicationStatusResponse>(services);
        AddQueryHandler<Features.Phase6.GetPublicationReviewsQuery, Features.Phase6.PagedResponse<Features.Phase6.PublicationReviewResponse>>(services);
        AddQueryHandler<Features.Phase6.GetCategoriesQuery, Features.Phase6.PagedResponse<Features.Phase6.CategoryResponse>>(services);
        AddQueryHandler<Features.Phase6.GetTagsQuery, Features.Phase6.PagedResponse<Features.Phase6.TagResponse>>(services);
        AddQueryHandler<Features.Phase6.GetCatalogCoursesQuery, Features.Phase6.PagedResponse<Features.Phase6.CatalogCourseResponse>>(services);
        AddQueryHandler<Features.Phase6.GetPublicCourseQuery, Features.Phase6.CatalogCourseResponse>(services);
        AddQueryHandler<Features.Phase6.SearchCoursesQuery, Features.Phase6.PagedResponse<Features.Phase6.SearchCourseResponse>>(services);
        AddQueryHandler<Features.Phase6.SuggestCourseSuggestionsQuery, IReadOnlyList<string>>(services);
    }

    private static void AddCommandHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Phase6.Phase6CommandHandler<TRequest, TResponse>>();

    private static void AddQueryHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Phase6.Phase6QueryHandler<TRequest, TResponse>>();
}
