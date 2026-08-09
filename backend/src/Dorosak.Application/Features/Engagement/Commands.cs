using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Engagement;

public sealed record GetCourseReviewsQuery(Guid CourseId, int Limit)
    : IQuery<CourseReviewPageResponse>;

public sealed record GetMyCourseReviewQuery(Guid UserId, Guid CourseId)
    : IQuery<CourseReviewResponse>;

public sealed record CreateCourseReviewCommand(
    Guid UserId,
    Guid CourseId,
    short Rating,
    string? Text,
    string IdempotencyKey) : IIdempotentCommand<CourseReviewResponse>
{
    public string IdempotencyOperation => "engagement.course-review-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { CourseId, Rating, Text };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record UpdateCourseReviewCommand(
    Guid UserId,
    Guid CourseId,
    Guid ReviewId,
    short Rating,
    string? Text) : ITransactionalCommand<CourseReviewResponse>;

public sealed record DeleteCourseReviewCommand(
    Guid UserId,
    Guid CourseId,
    Guid ReviewId) : ITransactionalCommand<EngagementOperationResponse>;
