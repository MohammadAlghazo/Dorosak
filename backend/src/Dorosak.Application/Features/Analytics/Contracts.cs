namespace Dorosak.Application.Features.Analytics;

public sealed record AdminAnalyticsOverviewResponse(
    DateTimeOffset GeneratedAt,
    long TotalUsers,
    long ActiveUsers,
    long TotalCourses,
    long PublishedCourses,
    long TotalEnrollments,
    long CompletedEnrollments,
    long CompletedDemoOrders,
    long ActiveDemoSubscriptions,
    long IssuedCertificates,
    long ActiveCertificates,
    long PendingPublicationReviews,
    long OpenModerationCases,
    long PendingOutboxMessages,
    long RetryingOutboxMessages);
