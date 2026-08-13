using System.Globalization;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Contracts;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/instructor/courses")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstructorCoursesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [PermissionPolicy(Permissions.CourseReadOwn)]
    public async Task<IActionResult> GetCourses(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<PagedResponse<CourseSummaryResponse>> result = await sender.Send(
            new GetInstructorCoursesQuery(userId, limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [PermissionPolicy(Permissions.CourseCreate)]
    public async Task<IActionResult> Create(CourseCreateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new CreateCourseCommand(
                userId,
                request.DefaultLocale,
                request.Level,
                request.Localizations,
                request.CategoryCodes ?? [],
                request.TagCodes ?? []),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpGet("{courseId:guid}")]
    [PermissionPolicy(Permissions.CourseReadOwn)]
    public async Task<IActionResult> GetCourse(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CourseDetailsResponse> result = await sender.Send(new GetCourseQuery(userId, courseId), cancellationToken);
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
        return this.ToActionResult(result);
    }

    [HttpPatch("{courseId:guid}")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> UpdateCourse(
        Guid courseId,
        CourseMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryReadIfMatch(out long? expectedVersion))
        {
            return InvalidEtag();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new UpdateCourseMetadataCommand(
                userId,
                courseId,
                expectedVersion,
                request.DefaultLocale,
                request.Level,
                request.Localizations,
                request.CategoryCodes ?? [],
                request.TagCodes ?? []),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpDelete("{courseId:guid}")]
    [PermissionPolicy(Permissions.CourseDeleteOwn)]
    public async Task<IActionResult> Archive(
        Guid courseId,
        ArchiveCourseRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new ArchiveCourseCommand(userId, courseId, request.Reason),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpGet("{courseId:guid}/curriculum")]
    [PermissionPolicy(Permissions.CourseReadOwn)]
    public async Task<IActionResult> GetCurriculum(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CurriculumResponse> result = await sender.Send(
            new GetCurriculumQuery(userId, courseId),
            cancellationToken);
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
        return this.ToActionResult(result);
    }

    [HttpPut("{courseId:guid}/curriculum")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> UpdateCurriculum(
        Guid courseId,
        CurriculumUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryReadIfMatch(out long? expectedVersion))
        {
            return InvalidEtag();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new UpdateCurriculumCommand(userId, courseId, expectedVersion, request.Sections),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpPost("{courseId:guid}/drafts")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> StartNewDraft(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new StartNewDraftCommand(userId, courseId),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpPost("{courseId:guid}/collaborators")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> AddCollaborator(
        Guid courseId,
        CollaboratorRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<CourseCollaboratorResponse> result = await sender.Send(
            new AddCollaboratorCommand(userId, courseId, request.UserId, request.Role),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("{courseId:guid}/collaborators/{collaboratorUserId:guid}")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> RemoveCollaborator(
        Guid courseId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<OperationCompleted> result = await sender.Send(
            new RemoveCollaboratorCommand(userId, courseId, collaboratorUserId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{courseId:guid}/owner")]
    [PermissionPolicy(Permissions.CourseUpdateOwn)]
    public async Task<IActionResult> TransferOwnership(
        Guid courseId,
        TransferCourseOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryReadIfMatch(out long? expectedVersion))
        {
            return InvalidEtag();
        }
        Result<CourseMutationResponse> result = await sender.Send(
            new TransferCourseOwnershipCommand(userId, courseId, request.NewOwnerUserId, expectedVersion),
            cancellationToken);
        SetEtag(result);
        return this.ToActionResult(result);
    }

    [HttpPost("{courseId:guid}/publication-requests")]
    [PermissionPolicy(Permissions.CourseSubmitOwn)]
    public async Task<IActionResult> RequestPublication(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<PublicationStatusResponse> result = await sender.Send(
            new RequestPublicationCommand(userId, courseId),
            cancellationToken);
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
        return this.ToActionResult(result);
    }

    [HttpDelete("{courseId:guid}/publication-requests/current")]
    [PermissionPolicy(Permissions.CourseSubmitOwn)]
    public async Task<IActionResult> WithdrawPublication(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<PublicationStatusResponse> result = await sender.Send(
            new WithdrawPublicationCommand(userId, courseId),
            cancellationToken);
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
        return this.ToActionResult(result);
    }

    [HttpGet("{courseId:guid}/publication-status")]
    [PermissionPolicy(Permissions.CourseReadOwn)]
    public async Task<IActionResult> GetPublicationStatus(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<PublicationStatusResponse> result = await sender.Send(
            new GetPublicationStatusQuery(userId, courseId),
            cancellationToken);
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private bool TryReadIfMatch(out long? version)
    {
        version = null;
        string? value = Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (value.Length < 4 || value[0] != '"' || value[1] != 'v' || value[^1] != '"')
        {
            return false;
        }
        return long.TryParse(value.AsSpan(2, value.Length - 3), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) &&
            parsed > 0 && (version = parsed) is not null;
    }

    private IActionResult InvalidEtag() => this.ToActionResult(Result.Failure<OperationCompleted>(ResultError.BusinessRule(
        "COURSE.IF_MATCH_INVALID",
        "If-Match must use the exact format \"v{version}\".")));

    private void SetEtag(Result<CourseMutationResponse> result)
    {
        if (result.IsSuccess)
        {
            Response.Headers.ETag = FormatEtag(result.Value.DraftVersion);
        }
    }

    private static string FormatEtag(long version) => $"\"v{version.ToString(CultureInfo.InvariantCulture)}\"";
}

