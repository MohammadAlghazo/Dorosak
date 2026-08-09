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
[Route("api/v{version:apiVersion}/commerce")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommerceController(ISender sender) : ControllerBase
{
    [HttpPost("demo-checkout")]
    [PermissionPolicy(Permissions.CheckoutCreateOwn)]
    public async Task<IActionResult> DemoCheckout(DemoCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!Request.Headers.TryGetValue("Idempotency-Key", out Microsoft.Extensions.Primitives.StringValues values) ||
            string.IsNullOrWhiteSpace(values.ToString()))
        {
            return this.ToActionResult(Result.Failure<DemoCheckoutResponse>(ResultError.PreconditionRequired(
                "IDEMPOTENCY.KEY_REQUIRED", "Idempotency-Key is required.")));
        }
        Result<DemoCheckoutResponse> result = await sender.Send(new CreateDemoCheckoutCommand(
            userId, request.CourseId, request.Outcome.Trim().ToLowerInvariant(), GetLocale(), values.ToString()), cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? value = User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }

    private string GetLocale() => string.Equals(
        Request.Headers.AcceptLanguage.ToString().Split(',')[0].Split('-')[0], "en", StringComparison.OrdinalIgnoreCase)
        ? "en" : "ar";
}

public sealed record DemoCheckoutRequest(Guid CourseId, string Outcome);
