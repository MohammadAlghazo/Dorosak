using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Media;
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
public sealed class MediaController(ISender sender) : ControllerBase
{
    [HttpPost("uploads")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> CreateUpload(CreateUploadRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        string? idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new CreateUploadSessionCommand(
                userId,
                request.Purpose,
                request.ExpectedBytes,
                request.FileName,
                request.ContentType,
                request.CourseId,
                idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("uploads/{uploadSessionId:guid}/content")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    [Consumes("application/octet-stream")]
    [RequestSizeLimit(33554432)]
    public async Task<IActionResult> PutContent(Guid uploadSessionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!Request.ContentLength.HasValue)
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.BusinessRule(
                "MEDIA.CONTENT_LENGTH_REQUIRED", "Content-Length is required for streamed uploads.")));
        }
        string? sha256 = Request.Headers["X-Content-SHA256"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["X-Content-SHA256"] = ["The SHA-256 header is required."] })));
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new PutUploadContentCommand(userId, uploadSessionId, Request.Body, Request.ContentLength.Value, Request.ContentType, sha256),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("uploads/{uploadSessionId:guid}/parts")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> IssuePart(Guid uploadSessionId, IssuePartRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<UploadPartResponse> result = await sender.Send(
            new IssueUploadPartCommand(userId, uploadSessionId, request.PartNumber, request.ExpectedBytes, request.Sha256),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("uploads/{uploadSessionId:guid}/complete")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> Complete(Guid uploadSessionId, CompleteUploadRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        string? idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new CompleteUploadCommand(userId, uploadSessionId, request.TotalBytes, request.Sha256, request.Parts, idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("uploads/{uploadSessionId:guid}")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> Cancel(Guid uploadSessionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        string? idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new CancelUploadCommand(userId, uploadSessionId, idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("media/{assetId:guid}/status")]
    [PermissionPolicy(Permissions.MediaReadOwn)]
    public async Task<IActionResult> Status(Guid assetId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<MediaStatusResponse> result = await sender.Send(new GetMediaStatusQuery(userId, assetId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("media/{assetId:guid}/download-grant")]
    [PermissionPolicy(Permissions.MediaReadOwn)]
    public async Task<IActionResult> DownloadGrant(Guid assetId, DownloadGrantRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<DownloadGrantResponse> result = await sender.Send(
            new CreateDownloadGrantCommand(userId, assetId, request?.VariantId, request?.FileName),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("media/{assetId:guid}/captions")]
    [PermissionPolicy(Permissions.MediaUploadOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> CreateCaptionUpload(
        Guid assetId,
        CreateCaptionUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        string? idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.ToActionResult(Result.Failure<UploadSessionResponse>(ResultError.Validation(
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new CreateCaptionUploadCommand(
                userId,
                assetId,
                request.Locale,
                request.Label,
                request.ExpectedBytes,
                request.FileName,
                idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}

public sealed record CreateUploadRequest(
    string Purpose,
    long ExpectedBytes,
    string FileName,
    string ContentType,
    Guid? CourseId);

public sealed record IssuePartRequest(int PartNumber, long ExpectedBytes, string Sha256);

public sealed record CompleteUploadRequest(long TotalBytes, string Sha256, IReadOnlyList<UploadPartInput> Parts);

public sealed record DownloadGrantRequest(Guid? VariantId, string? FileName);

public sealed record CreateCaptionUploadRequest(string Locale, string Label, long ExpectedBytes, string FileName);
