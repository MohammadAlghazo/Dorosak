using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Contracts;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using Dorosak.Application.Features.Publishing;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPhase6Controller(ISender sender, IOutputCacheStore outputCacheStore) : ControllerBase
{
    [HttpPost("courses/{courseId:guid}/publish")]
    [AdminHighRiskPolicy(Permissions.CoursePublishAny)]
    [ProducesResponseType(typeof(ApiResponse<CourseReleaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishCourse(
        Guid courseId,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return MissingIdempotencyKey();
        }
        Result<CourseReleaseResponse> result = await sender.Send(
            new PublishCourseCommand(userId, courseId, idempotencyKey, auditReason ?? string.Empty),
            cancellationToken);
        if (result.IsSuccess)
        {
            await outputCacheStore.EvictByTagAsync(ApiConstants.CatalogCacheTag, cancellationToken);
        }
        return this.ToActionResult(result);
    }

    [HttpPost("courses/{courseId:guid}/unpublish")]
    [AdminHighRiskPolicy(Permissions.CoursePublishAny)]
    [ProducesResponseType(typeof(ApiResponse<CourseReleaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UnpublishCourse(
        Guid courseId,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return MissingIdempotencyKey();
        }
        Result<CourseReleaseResponse> result = await sender.Send(
            new UnpublishCourseCommand(userId, courseId, idempotencyKey, auditReason ?? string.Empty),
            cancellationToken);
        if (result.IsSuccess)
        {
            await outputCacheStore.EvictByTagAsync(ApiConstants.CatalogCacheTag, cancellationToken);
        }
        return this.ToActionResult(result);
    }
    [HttpGet("teacher-applications")]
    [AdminHighRiskPolicy(Permissions.TeacherApplicationReviewAny)]
    public async Task<IActionResult> GetTeacherApplications(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<TeacherApplicationResponse>> result = await sender.Send(
            new GetTeacherApplicationsQuery(limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("teacher-applications/{applicationId:guid}/review")]
    [AdminHighRiskPolicy(Permissions.TeacherApplicationReviewAny)]
    public async Task<IActionResult> ReviewTeacherApplication(
        Guid applicationId,
        TeacherApplicationReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<TeacherApplicationResponse> result = await sender.Send(
            new ReviewTeacherApplicationCommand(userId, applicationId, request.Decision, request.Reason),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("publication-reviews")]
    [PermissionPolicy(Permissions.CourseReviewAny)]
    public async Task<IActionResult> GetPublicationReviews(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<PublicationReviewResponse>> result = await sender.Send(
            new GetPublicationReviewsQuery(limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("publication-reviews/{reviewId:guid}/decision")]
    [PermissionPolicy(Permissions.CourseReviewAny)]
    public async Task<IActionResult> ReviewPublication(
        Guid reviewId,
        PublicationReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<PublicationReviewResponse> result = await sender.Send(
            new ReviewPublicationCommand(userId, reviewId, request.Decision, request.Reason),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("catalog/categories")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public Task<IActionResult> CreateCategory(CategoryUpsertRequest request, CancellationToken cancellationToken) =>
        UpsertCategory(null, request, cancellationToken);

    [HttpPut("catalog/categories/{categoryId:guid}")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public Task<IActionResult> UpdateCategory(
        Guid categoryId,
        CategoryUpsertRequest request,
        CancellationToken cancellationToken) => UpsertCategory(categoryId, request, cancellationToken);

    [HttpGet("catalog/categories")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<CategoryResponse>> result = await sender.Send(
            new GetCategoriesQuery(GetLocale(), limit, cursor, IncludeInactive: true),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("catalog/tags")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public Task<IActionResult> CreateTag(TagUpsertRequest request, CancellationToken cancellationToken) =>
        UpsertTag(null, request, cancellationToken);

    [HttpPut("catalog/tags/{tagId:guid}")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public Task<IActionResult> UpdateTag(
        Guid tagId,
        TagUpsertRequest request,
        CancellationToken cancellationToken) => UpsertTag(tagId, request, cancellationToken);

    [HttpGet("catalog/tags")]
    [PermissionPolicy(Permissions.CatalogManageTaxonomy)]
    public async Task<IActionResult> GetTags(
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResponse<TagResponse>> result = await sender.Send(
            new GetTagsQuery(GetLocale(), limit, cursor, IncludeInactive: true),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<IActionResult> UpsertCategory(
        Guid? categoryId,
        CategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CategoryResponse> result = await sender.Send(
            new UpsertCategoryCommand(
                userId,
                categoryId,
                request.Code,
                request.ParentId,
                request.DisplayOrder,
                request.IsActive,
                request.Localizations),
            cancellationToken);
        if (result.IsSuccess)
        {
            await outputCacheStore.EvictByTagAsync(ApiConstants.TaxonomyCacheTag, cancellationToken);
            await outputCacheStore.EvictByTagAsync(ApiConstants.CatalogCacheTag, cancellationToken);
        }
        return this.ToActionResult(result);
    }

    private async Task<IActionResult> UpsertTag(
        Guid? tagId,
        TagUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<TagResponse> result = await sender.Send(
            new UpsertTagCommand(userId, tagId, request.Code, request.IsActive, request.Localizations),
            cancellationToken);
        if (result.IsSuccess)
        {
            await outputCacheStore.EvictByTagAsync(ApiConstants.TaxonomyCacheTag, cancellationToken);
            await outputCacheStore.EvictByTagAsync(ApiConstants.CatalogCacheTag, cancellationToken);
        }
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private IActionResult MissingIdempotencyKey() => this.ToActionResult(
        Result.Failure<CourseReleaseResponse>(ResultError.Validation(
            new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));

    private string GetLocale()
    {
        string value = Request.GetTypedHeaders().AcceptLanguage?.FirstOrDefault()?.Value.Value ?? "ar";
        return value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    }
}
