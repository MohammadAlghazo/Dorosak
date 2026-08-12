using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Commerce;
using Dorosak.Infrastructure.Identity;
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
public sealed class SubscriptionsController(ISender sender) : ControllerBase
{
    [HttpGet("me/subscription")]
    [PermissionPolicy(Permissions.SubscriptionManageOwn)]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<DemoSubscriptionStateResponse> result = await sender.Send(
            new GetDemoSubscriptionQuery(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("subscriptions")]
    [PermissionPolicy(Permissions.SubscriptionManageOwn)]
    public async Task<IActionResult> Activate(
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingKey<DemoSubscriptionResponse>();
        }

        Result<DemoSubscriptionResponse> result = await sender.Send(
            new ActivateDemoSubscriptionCommand(userId, key), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/cancel")]
    [PermissionPolicy(Permissions.SubscriptionManageOwn)]
    public async Task<IActionResult> Cancel(
        Guid subscriptionId,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingKey<DemoSubscriptionResponse>();
        }

        Result<DemoSubscriptionResponse> result = await sender.Send(
            new CancelDemoSubscriptionCommand(userId, subscriptionId, key), cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue("sub"), out userId) && userId != Guid.Empty;

    private static bool TryGetIdempotencyKey(string? candidate, out string value)
    {
        value = candidate?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }

    private IActionResult MissingKey<T>() => this.ToActionResult(Result.Failure<T>(
        ResultError.PreconditionRequired("IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
}
