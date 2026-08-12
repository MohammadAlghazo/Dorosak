using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Credentials;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CertificatesController(ISender sender) : ControllerBase
{
    [HttpGet("me/certificates")]
    [Authorize]
    [PermissionPolicy(Permissions.CertificateReadOwn)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<IReadOnlyList<CertificateResponse>> result = await sender.Send(
            new GetMyCertificatesQuery(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("me/certificates/{certificateId:guid}")]
    [Authorize]
    [PermissionPolicy(Permissions.CertificateReadOwn)]
    public async Task<IActionResult> GetMine(Guid certificateId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CertificateResponse> result = await sender.Send(
            new GetMyCertificateQuery(userId, certificateId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("certificates/verify/{verificationCode}")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.PublicRateLimitPolicy)]
    public async Task<IActionResult> Verify(string verificationCode, CancellationToken cancellationToken)
    {
        Result<PublicCertificateResponse> result = await sender.Send(
            new VerifyCertificateQuery(verificationCode), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("admin/certificates/{certificateId:guid}/revoke")]
    [Authorize]
    [AdminHighRiskPolicy(Permissions.CertificateRevokeAny)]
    public async Task<IActionResult> Revoke(
        Guid certificateId,
        [FromHeader(Name = "X-Audit-Reason"), Required, StringLength(1000, MinimumLength = 8)] string reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<CertificateResponse> result = await sender.Send(
            new RevokeCertificateCommand(userId, certificateId, reason.Trim()), cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue("sub"), out userId) && userId != Guid.Empty;
}
