using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Moderation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpPost("reports")]
    [ProducesResponseType<ApiResponse<ContentReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> CreateReport(
        ContentReportRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<ContentReportResponse>(ResultError.PreconditionRequired(
                "IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
        }

        Result<ContentReportResponse> result = await sender.Send(
            new CreateContentReportCommand(
                userId,
                request.CourseId,
                request.ReviewId,
                request.CommentId,
                request.ReportedUserId,
                request.ContextCommentId,
                request.Reason,
                request.Details,
                idempotencyKey.Trim()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("me/reports/{reportId:guid}")]
    [ProducesResponseType<ApiResponse<ContentReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyReport(Guid reportId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ContentReportResponse> result = await sender.Send(
            new GetMyContentReportQuery(userId, reportId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}

public sealed record ContentReportRequest(
    Guid? CourseId,
    Guid? ReviewId,
    Guid? CommentId,
    Guid? ReportedUserId,
    Guid? ContextCommentId,
    string Reason,
    string? Details);
