using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Administration;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.PublicRateLimitPolicy)]
public sealed class PublicCmsController(ISender sender) : ControllerBase
{
    [HttpGet("pages/{slug}")]
    [OutputCache(PolicyName = ApiConstants.CmsOutputCachePolicy)]
    [ProducesResponseType<ApiResponse<PublicCmsPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPage(string slug, CancellationToken cancellationToken)
    {
        Result<PublicCmsPageResponse> result = await sender.Send(
            new GetPublicCmsPageQuery(slug, GetLocale()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("faqs")]
    [OutputCache(PolicyName = ApiConstants.CmsOutputCachePolicy)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicCmsFaqResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaqs(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PublicCmsFaqResponse>> result = await sender.Send(
            new GetPublicFaqsQuery(GetLocale()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("portfolio-settings")]
    [OutputCache(PolicyName = ApiConstants.CmsOutputCachePolicy)]
    [ProducesResponseType<ApiResponse<PublicPortfolioSettingsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        Result<PublicPortfolioSettingsResponse> result = await sender.Send(
            new GetPublicSettingsQuery(GetLocale()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private string GetLocale() =>
        Request.GetTypedHeaders().AcceptLanguage?.FirstOrDefault()?.Value.Value
            ?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "ar";
}
