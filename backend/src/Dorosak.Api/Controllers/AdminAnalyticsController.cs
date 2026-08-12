using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Analytics;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin/analytics")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAnalyticsController(ISender sender) : ControllerBase
{
    [HttpGet("overview")]
    [PermissionPolicy(Permissions.AnalyticsRead)]
    [ProducesResponseType(typeof(ApiResponse<AdminAnalyticsOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        Result<AdminAnalyticsOverviewResponse> result = await sender.Send(
            new GetAdminAnalyticsOverviewQuery(),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
