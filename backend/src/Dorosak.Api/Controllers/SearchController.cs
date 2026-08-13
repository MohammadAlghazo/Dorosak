using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/search")]
[EnableRateLimiting(ApiConstants.SearchRateLimitPolicy)]
public sealed class SearchController(ISender sender) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")] string query = "",
        [FromQuery] string? categoryCode = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? language = null,
        [FromQuery] string? level = null,
        [FromQuery] string? price = null,
        [FromQuery] string? duration = null,
        [FromQuery] string? instructor = null,
        [FromQuery] string sort = "",
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers["X-Robots-Tag"] = "noindex,follow";
        Result<SearchPageResponse> result = await sender.Send(
            new SearchCoursesQuery(
                GetLocale(),
                query,
                new CatalogFilterContract(categoryCode, tag, language, level, price, duration, instructor),
                sort,
                limit,
                cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("suggestions")]
    [OutputCache(PolicyName = ApiConstants.CatalogOutputCachePolicy)]
    public async Task<IActionResult> Suggestions(
        [FromQuery(Name = "q")] string query = "",
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<PublicSearchSuggestionResponse>> result = await sender.Send(
            new SuggestCourseSuggestionsQuery(GetLocale(), query, limit),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private string GetLocale()
    {
        string value = Request.GetTypedHeaders().AcceptLanguage?.FirstOrDefault()?.Value.Value ?? "ar";
        return value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    }
}

