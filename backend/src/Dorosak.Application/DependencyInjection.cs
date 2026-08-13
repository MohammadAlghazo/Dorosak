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
        AddCredentialsHandlers(services);
        AddCommunicationsHandlers(services);
        AddEngagementHandlers(services);
        AddModerationHandlers(services);
        AddAnalyticsHandlers(services);
        AddAdministrationHandlers(services);
        AddEngagementAuthorization(services);
        AddCommunicationsAuthorization(services);
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
        AddCommerceHandler<Features.Commerce.GetDemoSubscriptionQuery,
            Features.Commerce.DemoSubscriptionStateResponse>(services);
        AddCommerceHandler<Features.Commerce.ActivateDemoSubscriptionCommand,
            Features.Commerce.DemoSubscriptionResponse>(services);
        AddCommerceHandler<Features.Commerce.CancelDemoSubscriptionCommand,
            Features.Commerce.DemoSubscriptionResponse>(services);
    }

    private static void AddCredentialsHandlers(IServiceCollection services)
    {
        AddCredentialsHandler<Features.Credentials.GetMyCertificatesQuery,
            IReadOnlyList<Features.Credentials.CertificateResponse>>(services);
        AddCredentialsHandler<Features.Credentials.GetMyCertificateQuery,
            Features.Credentials.CertificateResponse>(services);
        AddCredentialsHandler<Features.Credentials.VerifyCertificateQuery,
            Features.Credentials.PublicCertificateResponse>(services);
        AddCredentialsHandler<Features.Credentials.IssueCertificateFromCompletionCommand,
            Features.Credentials.CertificateResponse>(services);
        AddCredentialsHandler<Features.Credentials.RevokeCertificateCommand,
            Features.Credentials.CertificateResponse>(services);
    }

    private static void AddCommunicationsHandlers(IServiceCollection services)
    {
        AddCommunicationsHandler<Features.Communications.GetConversationsQuery,
            Features.Communications.ConversationPageResponse>(services);
        AddCommunicationsHandler<Features.Communications.CreateConversationCommand,
            Features.Communications.ConversationResponse>(services);
        AddCommunicationsHandler<Features.Communications.GetConversationMessagesQuery,
            Features.Communications.MessagePageResponse>(services);
        AddCommunicationsHandler<Features.Communications.CreateMessageCommand,
            Features.Communications.MessageResponse>(services);
        AddCommunicationsHandler<Features.Communications.LeaveConversationCommand,
            Features.Communications.ConversationOperationResponse>(services);
        AddCommunicationsHandler<Features.Communications.GetNotificationsQuery,
            Features.Communications.NotificationPageResponse>(services);
        AddCommunicationsHandler<Features.Communications.GetNotificationUnreadCountQuery,
            Features.Communications.NotificationUnreadCountResponse>(services);
        AddCommunicationsHandler<Features.Communications.MarkNotificationReadCommand,
            Features.Communications.NotificationResponse>(services);
        AddCommunicationsHandler<Features.Communications.MarkAllNotificationsReadCommand,
            Features.Communications.NotificationsReadResponse>(services);
        AddCommunicationsHandler<Features.Communications.GetAnnouncementsQuery,
            Features.Communications.AnnouncementPageResponse>(services);
        AddCommunicationsHandler<Features.Communications.GetAnnouncementQuery,
            Features.Communications.AnnouncementResponse>(services);
        AddCommunicationsHandler<Features.Communications.CreateAnnouncementCommand,
            Features.Communications.AnnouncementResponse>(services);
        AddCommunicationsHandler<Features.Communications.UpdateAnnouncementCommand,
            Features.Communications.AnnouncementResponse>(services);
        AddCommunicationsHandler<Features.Communications.DeleteAnnouncementCommand,
            Features.Communications.AnnouncementOperationResponse>(services);
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Communications.CreateConversationCommand,
            Common.Results.Result<Features.Communications.ConversationResponse>>,
            Features.Communications.CreateConversationReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Communications.CreateMessageCommand,
            Common.Results.Result<Features.Communications.MessageResponse>>,
            Features.Communications.CreateMessageReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Communications.CreateAnnouncementCommand,
            Common.Results.Result<Features.Communications.AnnouncementResponse>>,
            Features.Communications.CreateAnnouncementReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Communications.UpdateAnnouncementCommand,
            Common.Results.Result<Features.Communications.AnnouncementResponse>>,
            Features.Communications.UpdateAnnouncementReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyResponseRedactor<
            Features.Communications.CreateConversationCommand,
            Common.Results.Result<Features.Communications.ConversationResponse>>,
            Features.Communications.CreateConversationResponseRedactor>();
        services.AddScoped<Common.Idempotency.IIdempotencyResponseRedactor<
            Features.Communications.CreateMessageCommand,
            Common.Results.Result<Features.Communications.MessageResponse>>,
            Features.Communications.CreateMessageResponseRedactor>();
        services.AddScoped<Common.Idempotency.IIdempotencyResponseRedactor<
            Features.Communications.CreateAnnouncementCommand,
            Common.Results.Result<Features.Communications.AnnouncementResponse>>,
            Features.Communications.CreateAnnouncementResponseRedactor>();
        services.AddScoped<Common.Idempotency.IIdempotencyResponseRedactor<
            Features.Communications.UpdateAnnouncementCommand,
            Common.Results.Result<Features.Communications.AnnouncementResponse>>,
            Features.Communications.UpdateAnnouncementResponseRedactor>();
    }

    private static void AddCommunicationsAuthorization(IServiceCollection services)
    {
        AddConversationAuthorizer<Features.Communications.GetConversationMessagesQuery>(services);
        AddConversationAuthorizer<Features.Communications.CreateMessageCommand>(services);
        AddConversationAuthorizer<Features.Communications.LeaveConversationCommand>(services);
        AddAnnouncementAuthorizer<Features.Communications.GetAnnouncementsQuery>(services);
        AddAnnouncementAuthorizer<Features.Communications.GetAnnouncementQuery>(services);
        AddAnnouncementAuthorizer<Features.Communications.CreateAnnouncementCommand>(services);
        AddAnnouncementAuthorizer<Features.Communications.UpdateAnnouncementCommand>(services);
        AddAnnouncementAuthorizer<Features.Communications.DeleteAnnouncementCommand>(services);
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

    private static void AddModerationHandlers(IServiceCollection services)
    {
        AddModerationHandler<Features.Moderation.CreateContentReportCommand, Features.Moderation.ContentReportResponse>(services);
        AddModerationHandler<Features.Moderation.GetMyContentReportQuery, Features.Moderation.ContentReportResponse>(services);
        AddModerationHandler<Features.Moderation.GetAdminContentReportsQuery, Features.Moderation.ContentReportPageResponse>(services);
        AddModerationHandler<Features.Moderation.GetModerationCasesQuery, Features.Moderation.ModerationCasePageResponse>(services);
        AddModerationHandler<Features.Moderation.GetModerationCaseQuery, Features.Moderation.ModerationCaseResponse>(services);
        AddModerationHandler<Features.Moderation.ApplyModerationActionCommand, Features.Moderation.ModerationCaseResponse>(services);
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Moderation.CreateContentReportCommand,
            Common.Results.Result<Features.Moderation.ContentReportResponse>>,
            Features.Moderation.CreateContentReportReplayHandler>();
        services.AddScoped<Common.Idempotency.IIdempotencyReplayHandler<
            Features.Moderation.ApplyModerationActionCommand,
            Common.Results.Result<Features.Moderation.ModerationCaseResponse>>,
            Features.Moderation.ApplyModerationActionReplayHandler>();
    }

    private static void AddAnalyticsHandlers(IServiceCollection services) =>
        services.AddTransient<MediatR.IRequestHandler<
            Features.Analytics.GetAdminAnalyticsOverviewQuery,
            Common.Results.Result<Features.Analytics.AdminAnalyticsOverviewResponse>>,
            Features.Analytics.GetAdminAnalyticsOverviewQueryHandler>();

    private static void AddAdministrationHandlers(IServiceCollection services) =>
        services.AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetAdminCmsQuery,
            Common.Results.Result<Features.Administration.AdminCmsResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetAdminCmsQuery,
                Features.Administration.AdminCmsResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetPublicCmsPageQuery,
            Common.Results.Result<Features.Administration.PublicCmsPageResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetPublicCmsPageQuery,
                Features.Administration.PublicCmsPageResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetPublicFaqsQuery,
            Common.Results.Result<IReadOnlyList<Features.Administration.PublicCmsFaqResponse>>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetPublicFaqsQuery,
                IReadOnlyList<Features.Administration.PublicCmsFaqResponse>>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetAdminSettingsQuery,
            Common.Results.Result<Features.Administration.PortfolioSettingsResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetAdminSettingsQuery,
                Features.Administration.PortfolioSettingsResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetPublicSettingsQuery,
            Common.Results.Result<Features.Administration.PublicPortfolioSettingsResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetPublicSettingsQuery,
                Features.Administration.PublicPortfolioSettingsResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.UpsertCmsPageDraftCommand,
            Common.Results.Result<Features.Administration.CmsPageResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.UpsertCmsPageDraftCommand,
                Features.Administration.CmsPageResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.PublishCmsPageCommand,
            Common.Results.Result<Features.Administration.CmsPageResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.PublishCmsPageCommand,
                Features.Administration.CmsPageResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.UpsertCmsFaqDraftCommand,
            Common.Results.Result<Features.Administration.CmsFaqResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.UpsertCmsFaqDraftCommand,
                Features.Administration.CmsFaqResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.PublishCmsFaqCommand,
            Common.Results.Result<Features.Administration.CmsFaqResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.PublishCmsFaqCommand,
                Features.Administration.CmsFaqResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.UpdatePortfolioSettingsCommand,
            Common.Results.Result<Features.Administration.PortfolioSettingsResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.UpdatePortfolioSettingsCommand,
                Features.Administration.PortfolioSettingsResponse>>()
        .AddTransient<MediatR.IRequestHandler<
            Features.Administration.GetAuditLogsQuery,
            Common.Results.Result<Features.Administration.AuditLogPageResponse>>,
            Features.Administration.AdministrationHandler<
                Features.Administration.GetAuditLogsQuery,
                Features.Administration.AuditLogPageResponse>>();

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

    private static void AddCredentialsHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Credentials.CredentialsHandler<TRequest, TResponse>>();

    private static void AddCommunicationsHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Communications.CommunicationsHandler<TRequest, TResponse>>();

    private static void AddEngagementHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Engagement.EngagementHandler<TRequest, TResponse>>();

    private static void AddModerationHandler<TRequest, TResponse>(IServiceCollection services)
        where TRequest : class, MediatR.IRequest<Common.Results.Result<TResponse>>
        where TResponse : notnull =>
        services.AddTransient<MediatR.IRequestHandler<TRequest, Common.Results.Result<TResponse>>,
            Features.Moderation.ModerationHandler<TRequest, TResponse>>();

    private static void AddDiscussionAuthorizer<TRequest>(IServiceCollection services)
        where TRequest : Features.Engagement.IDiscussionAuthorizedRequest =>
        services.AddScoped<Common.Authorization.IRequestAuthorizer<TRequest>,
            Features.Engagement.DiscussionResourceAuthorizer<TRequest>>();

    private static void AddConversationAuthorizer<TRequest>(IServiceCollection services)
        where TRequest : Features.Communications.IConversationAuthorizedRequest =>
        services.AddScoped<Common.Authorization.IRequestAuthorizer<TRequest>,
            Features.Communications.ConversationResourceAuthorizer<TRequest>>();

    private static void AddAnnouncementAuthorizer<TRequest>(IServiceCollection services)
        where TRequest : Features.Communications.IAnnouncementAuthorizedRequest =>
        services.AddScoped<Common.Authorization.IRequestAuthorizer<TRequest>,
            Features.Communications.AnnouncementResourceAuthorizer<TRequest>>();
}
