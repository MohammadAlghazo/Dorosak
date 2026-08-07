using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Contracts;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/me/teacher-application")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TeacherApplicationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [PermissionPolicy(Permissions.TeacherApplicationCreateOwn)]
    public async Task<IActionResult> Submit(
        TeacherApplicationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<TeacherApplicationResponse> result = await sender.Send(
            new SubmitTeacherApplicationCommand(
                userId,
                request.Headline,
                request.Biography,
                request.Expertise,
                request.Motivation),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<TeacherApplicationResponse> result = await sender.Send(
            new GetTeacherApplicationQuery(userId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete]
    [PermissionPolicy(Permissions.TeacherApplicationCreateOwn)]
    public async Task<IActionResult> Withdraw(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<TeacherApplicationResponse> result = await sender.Send(
            new WithdrawTeacherApplicationCommand(userId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}
