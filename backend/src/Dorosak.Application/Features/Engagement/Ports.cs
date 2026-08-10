using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Engagement;

public interface IEngagementService
{
    Task<Result<CourseReviewPageResponse>> GetCourseReviewsAsync(
        GetCourseReviewsQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> GetMyCourseReviewAsync(
        GetMyCourseReviewQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> CreateCourseReviewAsync(
        CreateCourseReviewCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> UpdateCourseReviewAsync(
        UpdateCourseReviewCommand request,
        CancellationToken cancellationToken);

    Task<Result<EngagementOperationResponse>> DeleteCourseReviewAsync(
        DeleteCourseReviewCommand request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionThreadPageResponse>> GetDiscussionThreadsAsync(
        GetDiscussionThreadsQuery request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionThreadResponse>> GetDiscussionThreadAsync(
        GetDiscussionThreadQuery request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionThreadResponse>> CreateDiscussionThreadAsync(
        CreateDiscussionThreadCommand request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionThreadResponse>> UpdateDiscussionThreadAsync(
        UpdateDiscussionThreadCommand request,
        CancellationToken cancellationToken);

    Task<Result<EngagementOperationResponse>> DeleteDiscussionThreadAsync(
        DeleteDiscussionThreadCommand request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionCommentResponse>> CreateDiscussionCommentAsync(
        CreateDiscussionCommentCommand request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionCommentResponse>> UpdateDiscussionCommentAsync(
        UpdateDiscussionCommentCommand request,
        CancellationToken cancellationToken);

    Task<Result<EngagementOperationResponse>> DeleteDiscussionCommentAsync(
        DeleteDiscussionCommentCommand request,
        CancellationToken cancellationToken);

    Task<Result<CommentLikeResponse>> LikeDiscussionCommentAsync(
        LikeDiscussionCommentCommand request,
        CancellationToken cancellationToken);

    Task<Result<CommentLikeResponse>> UnlikeDiscussionCommentAsync(
        UnlikeDiscussionCommentCommand request,
        CancellationToken cancellationToken);

    Task<Result<DiscussionCommentResponse>> GetDiscussionCommentForReplayAsync(
        Guid userId,
        DiscussionScope scope,
        Guid threadId,
        Guid commentId,
        CancellationToken cancellationToken);
}

public interface IDiscussionAuthorizedRequest : Common.Authorization.IAuthorizedRequest
{
    Guid UserId { get; }

    DiscussionScope Scope { get; }
}

public interface IDiscussionAccessReader
{
    Task<bool> CanAccessAsync(
        Guid userId,
        DiscussionScope scope,
        CancellationToken cancellationToken);
}
