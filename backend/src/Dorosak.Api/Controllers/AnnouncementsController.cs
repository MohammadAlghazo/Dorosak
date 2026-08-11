using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Communications;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/instructor/courses/{courseId:guid}/announcements")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnnouncementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [PermissionPolicy(Permissions.AnnouncementManageCourse)]
    [ProducesResponseType<ApiResponse<AnnouncementPageResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        Guid courseId,
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<AnnouncementPageResponse> result = await sender.Send(
            new GetAnnouncementsQuery(userId, courseId, limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{announcementId:guid}")]
    [PermissionPolicy(Permissions.AnnouncementManageCourse)]
    [ProducesResponseType<ApiResponse<AnnouncementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid courseId, Guid announcementId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<AnnouncementResponse> result = await sender.Send(
            new GetAnnouncementQuery(userId, courseId, announcementId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [PermissionPolicy(Permissions.AnnouncementManageCourse)]
    [ProducesResponseType<ApiResponse<AnnouncementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Create(
        Guid courseId,
        AnnouncementRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingIdempotencyKey<AnnouncementResponse>();
        }

        Result<AnnouncementResponse> result = await sender.Send(
            new CreateAnnouncementCommand(userId, courseId, request.Title, request.Body, key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{announcementId:guid}")]
    [PermissionPolicy(Permissions.AnnouncementManageCourse)]
    [ProducesResponseType<ApiResponse<AnnouncementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(
        Guid courseId,
        Guid announcementId,
        AnnouncementRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingIdempotencyKey<AnnouncementResponse>();
        }

        Result<AnnouncementResponse> result = await sender.Send(
            new UpdateAnnouncementCommand(userId, courseId, announcementId, request.Title, request.Body, key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("{announcementId:guid}")]
    [PermissionPolicy(Permissions.AnnouncementManageCourse)]
    [ProducesResponseType<ApiResponse<AnnouncementOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid courseId, Guid announcementId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<AnnouncementOperationResponse> result = await sender.Send(
            new DeleteAnnouncementCommand(userId, courseId, announcementId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private static bool TryGetIdempotencyKey(string? candidate, out string value)
    {
        value = candidate?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }

    private IActionResult MissingIdempotencyKey<T>() => this.ToActionResult(
        Result.Failure<T>(ResultError.PreconditionRequired(
            "IDEMPOTENCY.KEY_REQUIRED",
            "Idempotency-Key is required.")));
}

public sealed record AnnouncementRequest(string Title, string Body);
