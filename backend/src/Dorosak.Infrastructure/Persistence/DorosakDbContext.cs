using Dorosak.Application.Common.Persistence;
using Dorosak.Domain.Assessment;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Cms;
using Dorosak.Domain.Commerce;
using Dorosak.Domain.Communications;
using Dorosak.Domain.Credentials;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Persistence;

public sealed class DorosakDbContext(DbContextOptions<DorosakDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options), IUnitOfWork, IDataProtectionKeyContext
{
    public const string DefaultSchema = "app";

    public const string MigrationsSchema = "migrations";

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    internal DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    internal DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();

    internal DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();

    internal DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    internal DbSet<TeacherApplication> TeacherApplications => Set<TeacherApplication>();

    internal DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();

    internal DbSet<Course> Courses => Set<Course>();

    internal DbSet<CourseLocalization> CourseLocalizations => Set<CourseLocalization>();

    internal DbSet<CourseSlug> CourseSlugs => Set<CourseSlug>();

    internal DbSet<CourseInstructor> CourseInstructors => Set<CourseInstructor>();

    internal DbSet<Category> Categories => Set<Category>();

    internal DbSet<CategoryLocalization> CategoryLocalizations => Set<CategoryLocalization>();

    internal DbSet<Tag> Tags => Set<Tag>();

    internal DbSet<TagLocalization> TagLocalizations => Set<TagLocalization>();

    internal DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();

    internal DbSet<CourseTag> CourseTags => Set<CourseTag>();

    internal DbSet<CourseDraft> CourseDrafts => Set<CourseDraft>();

    internal DbSet<CourseSection> CourseSections => Set<CourseSection>();

    internal DbSet<SectionRevision> SectionRevisions => Set<SectionRevision>();

    internal DbSet<CourseLesson> CourseLessons => Set<CourseLesson>();

    internal DbSet<LessonRevision> LessonRevisions => Set<LessonRevision>();

    internal DbSet<PublicationReview> PublicationReviews => Set<PublicationReview>();

    internal DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    internal DbSet<UploadSession> UploadSessions => Set<UploadSession>();

    internal DbSet<UploadPart> UploadParts => Set<UploadPart>();

    internal DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    internal DbSet<MediaVariant> MediaVariants => Set<MediaVariant>();

    internal DbSet<CaptionTrack> CaptionTracks => Set<CaptionTrack>();

    internal DbSet<MediaProcessingJob> MediaProcessingJobs => Set<MediaProcessingJob>();

    internal DbSet<CourseRelease> CourseReleases => Set<CourseRelease>();

    internal DbSet<CourseReleaseSection> CourseReleaseSections => Set<CourseReleaseSection>();

    internal DbSet<CourseReleaseLesson> CourseReleaseLessons => Set<CourseReleaseLesson>();

    internal DbSet<CourseReleaseAssessment> CourseReleaseAssessments => Set<CourseReleaseAssessment>();

    internal DbSet<CourseReleaseMediaVariant> CourseReleaseMediaVariants => Set<CourseReleaseMediaVariant>();

    internal DbSet<CourseReleaseCaption> CourseReleaseCaptions => Set<CourseReleaseCaption>();

    internal DbSet<CourseReleaseLocalization> CourseReleaseLocalizations => Set<CourseReleaseLocalization>();

    internal DbSet<CourseReleaseInstructor> CourseReleaseInstructors => Set<CourseReleaseInstructor>();

    internal DbSet<CourseReleaseTaxonomy> CourseReleaseTaxonomies => Set<CourseReleaseTaxonomy>();

    internal DbSet<CatalogDocument> CatalogDocuments => Set<CatalogDocument>();

    internal DbSet<CatalogProjectionState> CatalogProjectionStates => Set<CatalogProjectionState>();

    internal DbSet<Entitlement> Entitlements => Set<Entitlement>();

    internal DbSet<Enrollment> Enrollments => Set<Enrollment>();

    internal DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();

    internal DbSet<CourseCompletion> CourseCompletions => Set<CourseCompletion>();

    internal DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    internal DbSet<LearningNote> LearningNotes => Set<LearningNote>();

    internal DbSet<RecentlyViewedLesson> RecentlyViewedLessons => Set<RecentlyViewedLesson>();

    internal DbSet<Quiz> Quizzes => Set<Quiz>();

    internal DbSet<QuizVersion> QuizVersions => Set<QuizVersion>();

    internal DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();

    internal DbSet<QuizQuestionOption> QuizQuestionOptions => Set<QuizQuestionOption>();

    internal DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    internal DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();

    internal DbSet<Assignment> Assignments => Set<Assignment>();

    internal DbSet<AssignmentVersion> AssignmentVersions => Set<AssignmentVersion>();

    internal DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();

    internal DbSet<AssignmentSubmissionFile> AssignmentSubmissionFiles => Set<AssignmentSubmissionFile>();

    internal DbSet<QuizAudienceMember> QuizAudienceMembers => Set<QuizAudienceMember>();

    internal DbSet<AssignmentAudienceMember> AssignmentAudienceMembers => Set<AssignmentAudienceMember>();

    internal DbSet<GradeRevision> GradeRevisions => Set<GradeRevision>();

    internal DbSet<QuizGradeRevision> QuizGradeRevisions => Set<QuizGradeRevision>();

    internal DbSet<DemoOrder> DemoOrders => Set<DemoOrder>();

    internal DbSet<DemoPayment> DemoPayments => Set<DemoPayment>();

    internal DbSet<DemoSubscription> DemoSubscriptions => Set<DemoSubscription>();

    internal DbSet<Certificate> Certificates => Set<Certificate>();

    internal DbSet<CmsPage> CmsPages => Set<CmsPage>();

    internal DbSet<CmsPageRevision> CmsPageRevisions => Set<CmsPageRevision>();

    internal DbSet<CmsFaq> CmsFaqs => Set<CmsFaq>();

    internal DbSet<CmsFaqRevision> CmsFaqRevisions => Set<CmsFaqRevision>();

    internal DbSet<PortfolioSettings> PortfolioSettings => Set<PortfolioSettings>();

    internal DbSet<CourseReview> CourseReviews => Set<CourseReview>();

    internal DbSet<DiscussionThread> DiscussionThreads => Set<DiscussionThread>();

    internal DbSet<DiscussionComment> DiscussionComments => Set<DiscussionComment>();

    internal DbSet<CommentLike> CommentLikes => Set<CommentLike>();

    internal DbSet<ContentReport> ContentReports => Set<ContentReport>();

    internal DbSet<ModerationCase> ModerationCases => Set<ModerationCase>();

    internal DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();

    internal DbSet<Conversation> Conversations => Set<Conversation>();

    internal DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    internal DbSet<Message> Messages => Set<Message>();

    internal DbSet<NotificationSequence> NotificationSequences => Set<NotificationSequence>();

    internal DbSet<Notification> Notifications => Set<Notification>();

    internal DbSet<Announcement> Announcements => Set<Announcement>();

    internal DbSet<AnnouncementTarget> AnnouncementTargets => Set<AnnouncementTarget>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.HasDefaultSchema(DefaultSchema);
        builder.HasPostgresExtension("pg_trgm");
        builder.HasPostgresExtension("unaccent");
        builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            TResponse response = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        });
    }
}
