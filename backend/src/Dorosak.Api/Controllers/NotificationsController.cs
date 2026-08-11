using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
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
[Route("api/v{version:apiVersion}/me/notifications")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [PermissionPolicy(Permissions.NotificationReadOwn)]
    [ProducesResponseType<ApiResponse<NotificationPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Get(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        [FromQuery] long? afterSequence = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<NotificationPageResponse> result = await sender.Send(
            new GetNotificationsQuery(userId, limit, cursor, afterSequence),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("unread-count")]
    [PermissionPolicy(Permissions.NotificationReadOwn)]
    [ProducesResponseType<ApiResponse<NotificationUnreadCountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<NotificationUnreadCountResponse> result = await sender.Send(
            new GetNotificationUnreadCountQuery(userId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{notificationId:guid}/read")]
    [PermissionPolicy(Permissions.NotificationReadOwn)]
    [ProducesResponseType<ApiResponse<NotificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<NotificationResponse> result = await sender.Send(
            new MarkNotificationReadCommand(userId, notificationId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("read-all")]
    [PermissionPolicy(Permissions.NotificationReadOwn)]
    [ProducesResponseType<ApiResponse<NotificationsReadResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<NotificationsReadResponse> result = await sender.Send(
            new MarkAllNotificationsReadCommand(userId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}
