using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Moderation;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminModerationController(ISender sender) : ControllerBase
{
    [HttpGet("reports")]
    [PermissionPolicy(Permissions.ModerationReviewAny)]
    [ProducesResponseType<ApiResponse<ContentReportPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? status = null,
        [FromQuery] string? targetKind = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ContentReportPageResponse> result = await sender.Send(
            new GetAdminContentReportsQuery(userId, status, targetKind, limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("moderation-cases")]
    [PermissionPolicy(Permissions.ModerationReviewAny)]
    [ProducesResponseType<ApiResponse<ModerationCasePageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCases(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ModerationCasePageResponse> result = await sender.Send(
            new GetModerationCasesQuery(userId, status, limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("moderation-cases/{caseId:guid}")]
    [PermissionPolicy(Permissions.ModerationReviewAny)]
    [ProducesResponseType<ApiResponse<ModerationCaseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ModerationCaseResponse> result = await sender.Send(
            new GetModerationCaseQuery(userId, caseId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("moderation-cases/{caseId:guid}/actions")]
    [AdminHighRiskPolicy(Permissions.ModerationReviewAny)]
    [ProducesResponseType<ApiResponse<ModerationCaseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> ApplyAction(
        Guid caseId,
        ModerationActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<ModerationCaseResponse>(ResultError.PreconditionRequired(
                "IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
        }

        Result<ModerationCaseResponse> result = await sender.Send(
            new ApplyModerationActionCommand(
                userId,
                caseId,
                request.Action,
                request.Reason,
                request.ExpectedVersion,
                auditReason ?? string.Empty,
                idempotencyKey.Trim()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}

public sealed record ModerationActionRequest(string Action, string Reason, long ExpectedVersion);
