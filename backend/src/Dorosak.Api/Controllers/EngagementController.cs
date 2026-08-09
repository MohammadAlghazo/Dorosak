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
    public async Task<IActionResult> CreateReview(Guid courseId, CourseReviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(out string key))
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

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private bool TryGetIdempotencyKey(out string value)
    {
        value = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }
}

public sealed record CourseReviewRequest(short Rating, string? Text);
