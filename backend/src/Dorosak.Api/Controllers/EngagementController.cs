using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Engagement;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EngagementController(ISender sender) : ControllerBase
{
    [HttpGet("catalog/courses/{courseId:guid}/reviews")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.PublicRateLimitPolicy)]
    public async Task<IActionResult> GetReviews(Guid courseId, [FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        Result<CourseReviewPageResponse> result = await sender.Send(new GetCourseReviewsQuery(courseId, limit), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("courses/{courseId:guid}/reviews")]
    [Authorize]
    [PermissionPolicy(Permissions.ReviewManageOwn)]
    public async Task<IActionResult> CreateReview(
        Guid courseId,
        CourseReviewRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return this.ToActionResult(Result.Failure<CourseReviewResponse>(ResultError.PreconditionRequired(
                "IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
        }
        Result<CourseReviewResponse> result = await sender.Send(
            new CreateCourseReviewCommand(userId, courseId, request.Rating, request.Text, key), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("courses/{courseId:guid}/reviews/mine")]
    [Authorize]
    [PermissionPolicy(Permissions.ReviewManageOwn)]
    public async Task<IActionResult> GetMyReview(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CourseReviewResponse> result = await sender.Send(
            new GetMyCourseReviewQuery(userId, courseId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("courses/{courseId:guid}/reviews/{reviewId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.ReviewManageOwn)]
    public async Task<IActionResult> UpdateReview(Guid courseId, Guid reviewId, CourseReviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CourseReviewResponse> result = await sender.Send(
            new UpdateCourseReviewCommand(userId, courseId, reviewId, request.Rating, request.Text), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("courses/{courseId:guid}/reviews/{reviewId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.ReviewManageOwn)]
    public async Task<IActionResult> DeleteReview(Guid courseId, Guid reviewId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<EngagementOperationResponse> result = await sender.Send(
            new DeleteCourseReviewCommand(userId, courseId, reviewId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/discussions")]
    [HttpGet("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions")]
    [HttpGet("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions")]
    [HttpGet("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> GetDiscussionThreads(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<DiscussionThreadPageResponse> result = await sender.Send(
            new GetDiscussionThreadsQuery(
                userId,
                CreateDiscussionScope(),
                limit,
                cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}")]
    [HttpGet("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [HttpGet("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}")]
    [HttpGet("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> GetDiscussionThread(
        Guid threadId,
        [FromQuery] int commentLimit = 50,
        [FromQuery] string? commentCursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<DiscussionThreadResponse> result = await sender.Send(
            new GetDiscussionThreadQuery(
                userId,
                CreateDiscussionScope(),
                threadId,
                commentLimit,
                commentCursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/discussions")]
    [HttpPost("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions")]
    [HttpPost("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions")]
    [HttpPost("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> CreateDiscussionThread(
        DiscussionThreadRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key)) return MissingIdempotencyKey<DiscussionThreadResponse>();
        Result<DiscussionThreadResponse> result = await sender.Send(
            new CreateDiscussionThreadCommand(
                userId,
                CreateDiscussionScope(),
                request.Title,
                request.Body,
                key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}")]
    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> UpdateDiscussionThread(
        Guid threadId,
        DiscussionThreadRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<DiscussionThreadResponse> result = await sender.Send(
            new UpdateDiscussionThreadCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                request.Title,
                request.Body),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}")]
    [HttpDelete("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> DeleteDiscussionThread(
        Guid threadId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<EngagementOperationResponse> result = await sender.Send(
            new DeleteDiscussionThreadCommand(
                userId,
                CreateDiscussionScope(),
                threadId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}/comments")]
    [HttpPost("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments")]
    [HttpPost("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}/comments")]
    [HttpPost("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> CreateDiscussionComment(
        Guid threadId,
        CreateDiscussionCommentRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key)) return MissingIdempotencyKey<DiscussionCommentResponse>();
        Result<DiscussionCommentResponse> result = await sender.Send(
            new CreateDiscussionCommentCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                request.ParentCommentId,
                request.Body,
                key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.CommentManageOwn)]
    public async Task<IActionResult> UpdateDiscussionComment(
        Guid threadId,
        Guid commentId,
        UpdateDiscussionCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<DiscussionCommentResponse> result = await sender.Send(
            new UpdateDiscussionCommentCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                commentId,
                request.Body),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpDelete("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.CommentManageOwn)]
    public async Task<IActionResult> DeleteDiscussionComment(
        Guid threadId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<EngagementOperationResponse> result = await sender.Send(
            new DeleteDiscussionCommentCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                commentId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpPut("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> LikeDiscussionComment(
        Guid threadId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CommentLikeResponse> result = await sender.Send(
            new LikeDiscussionCommentCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                commentId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("learning/enrollments/{enrollmentId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpDelete("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [HttpDelete("instructor/courses/{courseId:guid}/releases/{releaseId:guid}/lessons/{lessonId:guid}/discussions/{threadId:guid}/comments/{commentId:guid}/like")]
    [Authorize]
    [PermissionPolicy(Permissions.DiscussionParticipate)]
    public async Task<IActionResult> UnlikeDiscussionComment(
        Guid threadId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CommentLikeResponse> result = await sender.Send(
            new UnlikeDiscussionCommentCommand(
                userId,
                CreateDiscussionScope(),
                threadId,
                commentId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private static bool TryGetIdempotencyKey(string? candidate, out string value)
    {
        value = candidate?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }

    private DiscussionScope CreateDiscussionScope()
    {
        Guid? lessonId = TryGetRouteId("lessonId", out Guid concreteLessonId) ? concreteLessonId : null;
        if (TryGetRouteId("enrollmentId", out Guid enrollmentId))
        {
            return DiscussionScope.ForEnrollment(enrollmentId, lessonId);
        }

        _ = TryGetRouteId("courseId", out Guid courseId);
        _ = TryGetRouteId("releaseId", out Guid releaseId);
        return DiscussionScope.ForInstructor(courseId, releaseId, lessonId);
    }

    private bool TryGetRouteId(string key, out Guid value) =>
        Guid.TryParse(Convert.ToString(RouteData.Values[key], System.Globalization.CultureInfo.InvariantCulture), out value);

    private IActionResult MissingIdempotencyKey<T>() => this.ToActionResult(
        Result.Failure<T>(ResultError.PreconditionRequired(
            "IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
}

public sealed record CourseReviewRequest(short Rating, string? Text);

public sealed record DiscussionThreadRequest(string Title, string Body);

public sealed record CreateDiscussionCommentRequest(string Body, Guid? ParentCommentId = null);

public sealed record UpdateDiscussionCommentRequest(string Body);
