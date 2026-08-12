using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Analytics;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Analytics;

internal sealed class AnalyticsService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider) : IAnalyticsService
{
    public async Task<Result<AdminAnalyticsOverviewResponse>> GetAdminOverviewAsync(
        CancellationToken cancellationToken)
    {
        AnalyticsOverviewRow row = await dbContext.Database
            .SqlQueryRaw<AnalyticsOverviewRow>(
                """
                SELECT
                    (SELECT COUNT(*) FROM identity.users) AS total_users,
                    (SELECT COUNT(*) FROM identity.users WHERE is_active) AS active_users,
                    (SELECT COUNT(*) FROM catalog.courses WHERE deleted_at IS NULL) AS total_courses,
                    (SELECT COUNT(*) FROM catalog.courses WHERE status = 'Published' AND deleted_at IS NULL) AS published_courses,
                    (SELECT COUNT(*) FROM learning.enrollments) AS total_enrollments,
                    (SELECT COUNT(*) FROM learning.enrollments WHERE status = 'Completed') AS completed_enrollments,
                    (SELECT COUNT(*) FROM commerce.demo_orders WHERE status = 'Completed') AS completed_demo_orders,
                    (SELECT COUNT(*) FROM commerce.demo_subscriptions WHERE status = 'Active') AS active_demo_subscriptions,
                    (SELECT COUNT(*) FROM credentials.certificates) AS issued_certificates,
                    (SELECT COUNT(*) FROM credentials.certificates WHERE status = 'Active') AS active_certificates,
                    (SELECT COUNT(*) FROM authoring.publication_reviews WHERE status = 'Pending') AS pending_publication_reviews,
                    (SELECT COUNT(*) FROM engagement.moderation_cases WHERE status IN ('Open', 'InReview')) AS open_moderation_cases,
                    (SELECT COUNT(*) FROM operations.outbox_messages WHERE processed_at IS NULL) AS pending_outbox_messages,
                    (SELECT COUNT(*) FROM operations.outbox_messages WHERE processed_at IS NULL AND last_error_code IS NOT NULL) AS retrying_outbox_messages
                """)
            .SingleAsync(cancellationToken);

        return Result.Success(new AdminAnalyticsOverviewResponse(
            timeProvider.GetUtcNow(),
            row.TotalUsers,
            row.ActiveUsers,
            row.TotalCourses,
            row.PublishedCourses,
            row.TotalEnrollments,
            row.CompletedEnrollments,
            row.CompletedDemoOrders,
            row.ActiveDemoSubscriptions,
            row.IssuedCertificates,
            row.ActiveCertificates,
            row.PendingPublicationReviews,
            row.OpenModerationCases,
            row.PendingOutboxMessages,
            row.RetryingOutboxMessages));
    }

    private sealed class AnalyticsOverviewRow
    {
        public long TotalUsers { get; init; }
        public long ActiveUsers { get; init; }
        public long TotalCourses { get; init; }
        public long PublishedCourses { get; init; }
        public long TotalEnrollments { get; init; }
        public long CompletedEnrollments { get; init; }
        public long CompletedDemoOrders { get; init; }
        public long ActiveDemoSubscriptions { get; init; }
        public long IssuedCertificates { get; init; }
        public long ActiveCertificates { get; init; }
        public long PendingPublicationReviews { get; init; }
        public long OpenModerationCases { get; init; }
        public long PendingOutboxMessages { get; init; }
        public long RetryingOutboxMessages { get; init; }
    }
}
