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
        AddPublishingHandlers(services);
        AddMediaHandlers(services);
        AddLearningHandlers(services);
        AddCommerceHandlers(services);
        AddEngagementHandlers(services);
        AddEngagementAuthorization(services);
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
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Phase6.StartNewDraftCommand>,
            Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.StartNewDraftCommand>>();
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
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.PutUploadContentCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.PutUploadContentCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.IssueUploadPartCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.IssueUploadPartCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.CompleteUploadCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.CompleteUploadCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.CancelUploadCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.CancelUploadCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.GetMediaStatusQuery>,
            Features.Media.MediaResourceAuthorizer<Features.Media.GetMediaStatusQuery>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.CreateDownloadGrantCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.CreateDownloadGrantCommand>>();
        services.AddScoped<Common.Authorization.IRequestAuthorizer<Features.Media.CreateCaptionUploadCommand>,
            Features.Media.MediaResourceAuthorizer<Features.Media.CreateCaptionUploadCommand>>();
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
        AddCommandHandler<Features.Phase6.StartNewDraftCommand, Features.Phase6.CourseMutationResponse>(services);
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
        AddQueryHandler<Features.Phase6.GetPublicCourseQuery, Features.Phase6.PublicCourseDetailResponse>(services);
        AddQueryHandler<Features.Phase6.SearchCoursesQuery, Features.Phase6.SearchPageResponse>(services);
        AddQueryHandler<Features.Phase6.SuggestCourseSuggestionsQuery,
            IReadOnlyList<Features.Phase6.PublicSearchSuggestionResponse>>(services);
    }

    private static void AddMediaHandlers(IServiceCollection services)
    {
        AddMediaCommandHandler<Features.Media.CreateUploadSessionCommand, Features.Media.UploadSessionResponse>(services);
        AddMediaCommandHandler<Features.Media.CreateCaptionUploadCommand, Features.Media.UploadSessionResponse>(services);
        AddMediaCommandHandler<Features.Media.PutUploadContentCommand, Features.Media.UploadSessionResponse>(services);
        AddMediaCommandHandler<Features.Media.IssueUploadPartCommand, Features.Media.UploadPartResponse>(services);
        AddMediaCommandHandler<Features.Media.CompleteUploadCommand, Features.Media.UploadSessionResponse>(services);
        AddMediaCommandHandler<Features.Media.CancelUploadCommand, Features.Media.UploadSessionResponse>(services);
        AddMediaCommandHandler<Features.Media.CreateDownloadGrantCommand, Features.Media.DownloadGrantResponse>(services);
        AddMediaQueryHandler<Features.Media.GetMediaStatusQuery, Features.Media.MediaStatusResponse>(services);
    }

    private static void AddPublishingHandlers(IServiceCollection services)
    {
        services.AddScoped<Features.Publishing.IPublishingCoordinator, Features.Publishing.PublishingCoordinator>();
        services.AddTransient<MediatR.IRequestHandler<Features.Publishing.PublishCourseCommand,
            Common.Results.Result<Features.Publishing.CourseReleaseResponse>>,
            Features.Publishing.PublishCourseCommandHandler>();
        services.AddTransient<MediatR.IRequestHandler<Features.Publishing.UnpublishCourseCommand,
            Common.Results.Result<Features.Publishing.CourseReleaseResponse>>,
            Features.Publishing.UnpublishCourseCommandHandler>();
        services.AddTransient<MediatR.IRequestHandler<Features.Publishing.ActivateCourseReleaseCommand,
            Common.Results.Result<Features.Publishing.CourseReleaseResponse>>,
            Features.Publishing.ActivateCourseReleaseCommandHandler>();
        services.AddTransient<MediatR.IRequestHandler<Features.Publishing.ResolvePublicCourseQuery,
            Common.Results.Result<Features.Publishing.PublicCourseLookupResponse>>,
            Features.Publishing.ResolvePublicCourseQueryHandler>();
    }

    private static void AddLearningHandlers(IServiceCollection services)
    {
        AddLearningHandler<Features.Learning.EnrollCourseCommand, Features.Learning.EnrollmentResponse>(services);
        AddLearningHandler<Features.Learning.GetEnrollmentsQuery, IReadOnlyList<Features.Learning.EnrollmentResponse>>(services);
        AddLearningHandler<Features.Learning.GetLearningManifestQuery, Features.Learning.LearningManifestResponse>(services);
        AddLearningHandler<Features.Learning.GetLearningLessonQuery, Features.Learning.LearningLessonResponse>(services);
        AddLearningHandler<Features.Learning.GetCourseLearnersQuery, IReadOnlyList<Features.Learning.CourseLearnerResponse>>(services);
        AddLearningHandler<Features.Learning.UpdateLessonProgressCommand, Features.Learning.ProgressResponse>(services);
        AddLearningHandler<Features.Learning.GetLearningNotesQuery, IReadOnlyList<Features.Learning.LearningNoteResponse>>(services);
        AddLearningHandler<Features.Learning.UpsertLearningNoteCommand, Features.Learning.LearningNoteResponse>(services);
        AddLearningHandler<Features.Learning.DeleteLearningNoteCommand, Features.Learning.LearningOperationResponse>(services);
        AddLearningHandler<Features.Learning.AddBookmarkCommand, Features.Learning.BookmarkResponse>(services);
        AddLearningHandler<Features.Learning.DeleteBookmarkCommand, Features.Learning.LearningOperationResponse>(services);
        AddLearningHandler<Features.Learning.MarkRecentlyViewedCommand, Features.Learning.LearningOperationResponse>(services);
        AddLearningHandler<Features.Learning.CreateQuizVersionCommand, Features.Learning.QuizVersionResponse>(services);
        AddLearningHandler<Features.Learning.MarkQuizVersionReadyCommand, Features.Learning.QuizVersionResponse>(services);
        AddLearningHandler<Features.Learning.StartQuizAttemptCommand, Features.Learning.QuizAttemptResponse>(services);
        AddLearningHandler<Features.Learning.GetQuizAttemptQuery, Features.Learning.QuizAttemptResponse>(services);
        AddLearningHandler<Features.Learning.SubmitQuizAttemptCommand, Features.Learning.QuizAttemptResponse>(services);
        AddLearningHandler<Features.Learning.GradeQuizAttemptCommand, Features.Learning.GradeResponse>(services);
        AddLearningHandler<Features.Learning.CreateAssignmentVersionCommand, Features.Learning.AssignmentVersionResponse>(services);
        AddLearningHandler<Features.Learning.MarkAssignmentVersionReadyCommand, Features.Learning.AssignmentVersionResponse>(services);
        AddLearningHandler<Features.Learning.SubmitAssignmentCommand, Features.Learning.AssignmentSubmissionResponse>(services);
        AddLearningHandler<Features.Learning.GetAssignmentSubmissionQuery, Features.Learning.AssignmentSubmissionResponse>(services);
        AddLearningHandler<Features.Learning.GetCurrentAssignmentSubmissionQuery, Features.Learning.AssignmentSubmissionResponse>(services);
        AddLearningHandler<Features.Learning.GradeAssignmentCommand, Features.Learning.GradeResponse>(services);
    }

    private static void AddCommerceHandlers(IServiceCollection services)
    {
        AddCommerceHandler<Features.Commerce.CreateDemoCheckoutCommand, Features.Commerce.DemoCheckoutResponse>(services);
    }

    private static void AddEngagementHandlers(IServiceCollection services)
    {
        AddEngagementHandler<Features.Engagement.GetCourseReviewsQuery, Features.Engagement.CourseReviewPageResponse>(services);
        AddEngagementHandler<Features.Engagement.GetMyCourseReviewQuery, Features.Engagement.CourseReviewResponse>(services);
        AddEngagementHandler<Features.Engagement.CreateCourseReviewCommand, Features.Engagement.CourseReviewResponse>(services);
        AddEngagementHandler<Features.Engagement.UpdateCourseReviewCommand, Features.Engagement.CourseReviewResponse>(services);
        AddEngagementHandler<Features.Engagement.DeleteCourseReviewCommand, Features.Engagement.EngagementOperationResponse>(services);
        AddEngagementHandler<Features.Engagement.GetDiscussionThreadsQuery, Features.Engagement.DiscussionThreadPageResponse>(services);
        AddEngagementHandler<Features.Engagement.GetDiscussionThreadQuery, Features.Engagement.DiscussionThreadResponse>(services);
        AddEngagementHandler<Features.Engagement.CreateDiscussionThreadCommand, Features.Engagement.DiscussionThreadResponse>(services);
        AddEngagementHandler<Features.Engagement.UpdateDiscussionThreadCommand, Features.Engagement.DiscussionThreadResponse>(services);
        AddEngagementHandler<Features.Engagement.DeleteDiscussionThreadCommand, Features.Engagement.EngagementOperationResponse>(services);
        AddEngagementHandler<Features.Engagement.CreateDiscussionCommentCommand, Features.Engagement.DiscussionCommentResponse>(services);
        AddEngagementHandler<Features.Engagement.UpdateDiscussionCommentCommand, Features.Engagement.DiscussionCommentResponse>(services);
        AddEngagementHandler<Features.Engagement.DeleteDiscussionCommentCommand, Features.Engagement.EngagementOperationResponse>(services);
        AddEngagementHandler<Features.Engagement.LikeDiscussionCommentCommand, Features.Engagement.CommentLikeResponse>(services);
        AddEngagementHandler<Features.Engagement.UnlikeDiscussionCommentCommand, Features.Engagement.CommentLikeResponse>(services);
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Engagement.CreateDiscussionThreadCommand,
            Common.Results.Result<Features.Engagement.DiscussionThreadResponse>>,
            Features.Engagement.CreateDiscussionThreadReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Engagement.CreateDiscussionCommentCommand,
            Common.Results.Result<Features.Engagement.DiscussionCommentResponse>>,
            Features.Engagement.CreateDiscussionCommentReplayHandler>();
    }

    private static void AddEngagementAuthorization(IServiceCollection services)
    {
        AddDiscussionAuthorizer<Features.Engagement.GetDiscussionThreadsQuery>(services);
        AddDiscussionAuthorizer<Features.Engagement.GetDiscussionThreadQuery>(services);
        AddDiscussionAuthorizer<Features.Engagement.CreateDiscussionThreadCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.UpdateDiscussionThreadCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.DeleteDiscussionThreadCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.CreateDiscussionCommentCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.UpdateDiscussionCommentCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.DeleteDiscussionCommentCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.LikeDiscussionCommentCommand>(services);
        AddDiscussionAuthorizer<Features.Engagement.UnlikeDiscussionCommentCommand>(services);
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

    private static void AddMediaCommandHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Media.MediaCommandHandler<TRequest, TResponse>>();

    private static void AddMediaQueryHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Media.MediaQueryHandler<TRequest, TResponse>>();

    private static void AddLearningHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Learning.LearningHandler<TRequest, TResponse>>();

    private static void AddCommerceHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Commerce.CommerceHandler<TRequest, TResponse>>();

    private static void AddEngagementHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Engagement.EngagementHandler<TRequest, TResponse>>();

    private static void AddDiscussionAuthorizer<TRequest>(IServiceCollection services)
        where TRequest : Features.Engagement.IDiscussionAuthorizedRequest =>
        services.AddScoped<Common.Authorization.IRequestAuthorizer<TRequest>,
            Features.Engagement.DiscussionResourceAuthorizer<TRequest>>();
}
