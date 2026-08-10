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

public sealed record GetDiscussionThreadsQuery(
    Guid UserId,
    DiscussionScope Scope,
    int Limit,
    string? Cursor) : IQuery<DiscussionThreadPageResponse>, IDiscussionAuthorizedRequest;

public sealed record GetDiscussionThreadQuery(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    int CommentLimit,
    string? CommentCursor) : IQuery<DiscussionThreadResponse>, IDiscussionAuthorizedRequest;

public sealed record CreateDiscussionThreadCommand(
    Guid UserId,
    DiscussionScope Scope,
    string Title,
    string Body,
    string IdempotencyKey) : IIdempotentCommand<DiscussionThreadResponse>, IDiscussionAuthorizedRequest
{
    public string IdempotencyOperation => "engagement.discussion-thread-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { Scope, Title, Body };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record UpdateDiscussionThreadCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    string Title,
    string Body) : ITransactionalCommand<DiscussionThreadResponse>, IDiscussionAuthorizedRequest;

public sealed record DeleteDiscussionThreadCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId) : ITransactionalCommand<EngagementOperationResponse>, IDiscussionAuthorizedRequest;

public sealed record CreateDiscussionCommentCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    Guid? ParentCommentId,
    string Body,
    string IdempotencyKey) : IIdempotentCommand<DiscussionCommentResponse>, IDiscussionAuthorizedRequest
{
    public string IdempotencyOperation => "engagement.discussion-comment-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { Scope, ThreadId, ParentCommentId, Body };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record UpdateDiscussionCommentCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    Guid CommentId,
    string Body) : ITransactionalCommand<DiscussionCommentResponse>, IDiscussionAuthorizedRequest;

public sealed record DeleteDiscussionCommentCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    Guid CommentId) : ITransactionalCommand<EngagementOperationResponse>, IDiscussionAuthorizedRequest;

public sealed record LikeDiscussionCommentCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    Guid CommentId) : ITransactionalCommand<CommentLikeResponse>, IDiscussionAuthorizedRequest;

public sealed record UnlikeDiscussionCommentCommand(
    Guid UserId,
    DiscussionScope Scope,
    Guid ThreadId,
    Guid CommentId) : ITransactionalCommand<CommentLikeResponse>, IDiscussionAuthorizedRequest;
