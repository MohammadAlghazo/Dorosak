using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/catalog")]
[EnableRateLimiting(ApiConstants.PublicRateLimitPolicy)]
public sealed class PublicCatalogController(ISender sender) : ControllerBase
{
    [HttpGet("courses")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? categoryCode,
        [FromQuery] string? language,
        [FromQuery] string? level,
        [FromQuery] string sort = "newest",
        [FromQuery] int limit = 24,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<CatalogCourseResponse>> result = await sender.Send(
            new GetCatalogCoursesQuery(
                GetLocale(),
                new CatalogFilterContract(categoryCode, language, level),
                sort,
                limit,
                cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("courses/{slug}")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public async Task<IActionResult> GetCourse(string slug, CancellationToken cancellationToken)
    {
        Result<CatalogCourseResponse> result = await sender.Send(
            new GetPublicCourseQuery(GetLocale(), slug),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("categories")]
    [OutputCache(PolicyName = ApiConstants.TaxonomyOutputCachePolicy)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<CategoryResponse>> result = await sender.Send(
            new GetCategoriesQuery(GetLocale(), limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("tags")]
    [OutputCache(PolicyName = ApiConstants.TaxonomyOutputCachePolicy)]
    public async Task<IActionResult> GetTags(
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<TagResponse>> result = await sender.Send(
            new GetTagsQuery(GetLocale(), limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("featured")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public Task<IActionResult> GetFeatured([FromQuery] int limit = 24, CancellationToken cancellationToken = default) =>
        GetDiscovery("newest", limit, cancellationToken);

    [HttpGet("popular")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public Task<IActionResult> GetPopular([FromQuery] int limit = 24, CancellationToken cancellationToken = default) =>
        GetDiscovery("popular", limit, cancellationToken);

    [HttpGet("recommendations")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public Task<IActionResult> GetRecommendations([FromQuery] int limit = 24, CancellationToken cancellationToken = default) =>
        GetDiscovery("newest", limit, cancellationToken);

    private async Task<IActionResult> GetDiscovery(string sort, int limit, CancellationToken cancellationToken)
    {
        Result<PagedResponse<CatalogCourseResponse>> result = await sender.Send(
            new GetCatalogCoursesQuery(GetLocale(), new CatalogFilterContract(), sort, limit, null),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private string GetLocale()
    {
        string value = Request.GetTypedHeaders().AcceptLanguage?.FirstOrDefault()?.Value.Value ?? "ar";
        return value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    }
}
