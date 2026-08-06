using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.System.GetSystemStatus;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(ISender sender) : ControllerBase
{
    [HttpGet("status")]
    [EnableRateLimiting(ApiConstants.PublicRateLimitPolicy)]
    [OutputCache(PolicyName = ApiConstants.PublicOutputCachePolicy)]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        Result<SystemStatusResponse> result = await sender.Send(new GetSystemStatusQuery(), cancellationToken);
        return this.ToActionResult(result);
    }
}
