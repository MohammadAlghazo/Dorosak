using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Administration;
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
public sealed class AdminAdministrationController(ISender sender, IOutputCacheStore outputCacheStore) : ControllerBase
{
    [HttpGet("cms")]
    [PermissionPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<AdminCmsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCms(CancellationToken cancellationToken)
    {
        Result<AdminCmsResponse> result = await sender.Send(new GetAdminCmsQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("settings")]
    [PermissionPolicy(Permissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<PortfolioSettingsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        Result<PortfolioSettingsResponse> result = await sender.Send(new GetAdminSettingsQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("audit-logs")]
    [AdminHighRiskPolicy(Permissions.AuditRead)]
    [ProducesResponseType<ApiResponse<AuditLogPageResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? action = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<AuditLogPageResponse> result = await sender.Send(
            new GetAuditLogsQuery(userId, action, limit, cursor, auditReason ?? string.Empty),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("cms/pages/{slug}/draft")]
    [AdminHighRiskPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<CmsPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SavePageDraft(
        string slug,
        CmsPageDraftRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CmsPageResponse> result = await sender.Send(
            new UpsertCmsPageDraftCommand(userId, slug, request.ExpectedVersion, request.TitleAr, request.TitleEn,
                request.BodyAr, request.BodyEn, auditReason ?? string.Empty), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("cms/pages/{slug}/publish")]
    [AdminHighRiskPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<CmsPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishPage(
        string slug,
        CmsPublishRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CmsPageResponse> result = await sender.Send(
            new PublishCmsPageCommand(userId, slug, request.ExpectedVersion, auditReason ?? string.Empty), cancellationToken);
        if (result.IsSuccess) await outputCacheStore.EvictByTagAsync(ApiConstants.CmsCacheTag, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("cms/faqs/{faqId:guid}/draft")]
    [AdminHighRiskPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<CmsFaqResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveFaqDraft(
        Guid faqId,
        CmsFaqDraftRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CmsFaqResponse> result = await sender.Send(
            new UpsertCmsFaqDraftCommand(userId, faqId, request.ExpectedVersion, request.DisplayOrder,
                request.QuestionAr, request.QuestionEn, request.AnswerAr, request.AnswerEn, auditReason ?? string.Empty), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("cms/faqs")]
    [AdminHighRiskPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<CmsFaqResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFaqDraft(
        CmsFaqDraftRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CmsFaqResponse> result = await sender.Send(
            new UpsertCmsFaqDraftCommand(userId, null, request.ExpectedVersion, request.DisplayOrder,
                request.QuestionAr, request.QuestionEn, request.AnswerAr, request.AnswerEn, auditReason ?? string.Empty), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("cms/faqs/{faqId:guid}/publish")]
    [AdminHighRiskPolicy(Permissions.CmsManage)]
    [ProducesResponseType<ApiResponse<CmsFaqResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishFaq(
        Guid faqId,
        CmsPublishRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CmsFaqResponse> result = await sender.Send(
            new PublishCmsFaqCommand(userId, faqId, request.ExpectedVersion, auditReason ?? string.Empty), cancellationToken);
        if (result.IsSuccess) await outputCacheStore.EvictByTagAsync(ApiConstants.CmsCacheTag, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("settings")]
    [AdminHighRiskPolicy(Permissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<PortfolioSettingsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSettings(
        PortfolioSettingsRequest request,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string? auditReason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<PortfolioSettingsResponse> result = await sender.Send(
            new UpdatePortfolioSettingsCommand(userId, request.FeaturedCourseLimit, request.ShowPortfolioNotice,
                request.NoticeAr, request.NoticeEn, request.ExpectedVersion, auditReason ?? string.Empty), cancellationToken);
        if (result.IsSuccess) await outputCacheStore.EvictByTagAsync(ApiConstants.CmsCacheTag, cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}

public sealed record CmsPageDraftRequest
{
    [Required]
    public required int ExpectedVersion { get; init; }

    [Required, StringLength(200)]
    public required string TitleAr { get; init; }

    [Required, StringLength(200)]
    public required string TitleEn { get; init; }

    [Required, StringLength(20000)]
    public required string BodyAr { get; init; }

    [Required, StringLength(20000)]
    public required string BodyEn { get; init; }
}

public sealed record CmsFaqDraftRequest
{
    [Required]
    public required int ExpectedVersion { get; init; }

    [Required, Range(0, 10000)]
    public required int DisplayOrder { get; init; }

    [Required, StringLength(300)]
    public required string QuestionAr { get; init; }

    [Required, StringLength(300)]
    public required string QuestionEn { get; init; }

    [Required, StringLength(5000)]
    public required string AnswerAr { get; init; }

    [Required, StringLength(5000)]
    public required string AnswerEn { get; init; }
}

public sealed record CmsPublishRequest
{
    [Required]
    public required int ExpectedVersion { get; init; }
}

public sealed record PortfolioSettingsRequest
{
    [Required, Range(1, 12)]
    public required int FeaturedCourseLimit { get; init; }

    [Required]
    public required bool ShowPortfolioNotice { get; init; }

    [Required, StringLength(240)]
    public required string NoticeAr { get; init; }

    [Required, StringLength(240)]
    public required string NoticeEn { get; init; }

    [Required, Range(1, long.MaxValue)]
    public required long ExpectedVersion { get; init; }
}
