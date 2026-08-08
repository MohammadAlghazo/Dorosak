namespace Dorosak.Infrastructure.Identity;

public static class Permissions
{
    public const string ProfileReadOwn = "Profile.ReadOwn";
    public const string ProfileUpdateOwn = "Profile.UpdateOwn";
    public const string SecurityManageOwn = "Security.ManageOwn";
    public const string SessionsManageOwn = "Sessions.ManageOwn";
    public const string TeacherApplicationCreateOwn = "TeacherApplication.CreateOwn";
    public const string TeacherApplicationReviewAny = "TeacherApplication.ReviewAny";
    public const string CourseCreate = "Course.Create";
    public const string CourseReadOwn = "Course.ReadOwn";
    public const string CourseUpdateOwn = "Course.UpdateOwn";
    public const string CourseDeleteOwn = "Course.DeleteOwn";
    public const string CourseSubmitOwn = "Course.SubmitOwn";
    public const string CourseReviewAny = "Course.ReviewAny";
    public const string CoursePublishAny = "Course.PublishAny";
    public const string CourseManageAny = "Course.ManageAny";
    public const string MediaUploadOwn = "Media.UploadOwn";
    public const string MediaReadOwn = "Media.ReadOwn";
    public const string MediaManageAny = "Media.ManageAny";
    public const string EnrollmentCreateOwn = "Enrollment.CreateOwn";
    public const string EnrollmentReadOwn = "Enrollment.ReadOwn";
    public const string LearningAccessOwn = "Learning.AccessOwn";
    public const string ProgressUpdateOwn = "Progress.UpdateOwn";
    public const string LearningViewCourseLearners = "Learning.ViewCourseLearners";
    public const string QuizAttemptOwn = "Quiz.AttemptOwn";
    public const string AssignmentSubmitOwn = "Assignment.SubmitOwn";
    public const string SubmissionGradeCourse = "Submission.GradeCourse";
    public const string AssessmentManageCourse = "Assessment.ManageCourse";
    public const string ReviewManageOwn = "Review.ManageOwn";
    public const string DiscussionParticipate = "Discussion.Participate";
    public const string CommentManageOwn = "Comment.ManageOwn";
    public const string ModerationReviewAny = "Moderation.ReviewAny";
    public const string MessageSendAsSelf = "Message.SendAsSelf";
    public const string ConversationReadOwn = "Conversation.ReadOwn";
    public const string NotificationReadOwn = "Notification.ReadOwn";
    public const string AnnouncementManageCourse = "Announcement.ManageCourse";
    public const string CertificateReadOwn = "Certificate.ReadOwn";
    public const string CertificateVerifyPublic = "Certificate.VerifyPublic";
    public const string CertificateRevokeAny = "Certificate.RevokeAny";
    public const string OrderReadOwn = "Order.ReadOwn";
    public const string CheckoutCreateOwn = "Checkout.CreateOwn";
    public const string SubscriptionManageOwn = "Subscription.ManageOwn";
    public const string CommerceManageOffers = "Commerce.ManageOffers";
    public const string CommerceManageOrders = "Commerce.ManageOrders";
    public const string CommerceManageRefunds = "Commerce.ManageRefunds";
    public const string CommerceReadEarningsOwn = "Commerce.ReadEarningsOwn";
    public const string CommerceManagePayoutAccountOwn = "Commerce.ManagePayoutAccountOwn";
    public const string UserReadAny = "User.ReadAny";
    public const string UserManageAny = "User.ManageAny";
    public const string RoleManageAny = "Role.ManageAny";
    public const string CatalogManageTaxonomy = "Catalog.ManageTaxonomy";
    public const string CmsManage = "Cms.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string FeatureFlagManage = "FeatureFlag.Manage";
    public const string AnalyticsRead = "Analytics.Read";
    public const string AuditRead = "Audit.Read";

    public static readonly IReadOnlyList<string> Student =
    [
        MediaUploadOwn,
        MediaReadOwn,
        ProfileReadOwn,
        ProfileUpdateOwn,
        SecurityManageOwn,
        SessionsManageOwn,
        TeacherApplicationCreateOwn,
        EnrollmentCreateOwn,
        EnrollmentReadOwn,
        LearningAccessOwn,
        ProgressUpdateOwn,
        QuizAttemptOwn,
        AssignmentSubmitOwn,
        ReviewManageOwn,
        DiscussionParticipate,
        CommentManageOwn,
        MessageSendAsSelf,
        ConversationReadOwn,
        NotificationReadOwn,
        CertificateReadOwn,
        CertificateVerifyPublic,
        OrderReadOwn,
        CheckoutCreateOwn,
        SubscriptionManageOwn,
    ];

    public static readonly IReadOnlyList<string> Teacher =
    [
        .. Student,
        CourseCreate,
        CourseReadOwn,
        CourseUpdateOwn,
        CourseDeleteOwn,
        CourseSubmitOwn,
        MediaUploadOwn,
        MediaReadOwn,
        LearningViewCourseLearners,
        SubmissionGradeCourse,
        AssessmentManageCourse,
        AnnouncementManageCourse,
        CommerceReadEarningsOwn,
        CommerceManagePayoutAccountOwn,
    ];

    public static readonly IReadOnlyList<string> All =
    [
        .. Teacher,
        TeacherApplicationReviewAny,
        CourseReviewAny,
        CoursePublishAny,
        CourseManageAny,
        MediaManageAny,
        ModerationReviewAny,
        CertificateRevokeAny,
        CommerceManageOffers,
        CommerceManageOrders,
        CommerceManageRefunds,
        UserReadAny,
        UserManageAny,
        RoleManageAny,
        CatalogManageTaxonomy,
        CmsManage,
        SettingsManage,
        FeatureFlagManage,
        AnalyticsRead,
        AuditRead,
    ];
}
