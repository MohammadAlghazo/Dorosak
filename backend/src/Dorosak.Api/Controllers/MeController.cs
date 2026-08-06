using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MeController(ISender sender, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return Unauthorized();
        }

        Result<IdentitySnapshotResponse> result = await sender.Send(
            new GetCurrentProfileQuery(userId, sessionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return Unauthorized();
        }

        Result<SessionsResponse> result = await sender.Send(
            new GetSessionsQuery(userId, sessionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid currentSessionId))
        {
            return Unauthorized();
        }

        Result<OperationCompletedResponse> result = await sender.Send(
            new RevokeSessionCommand(userId, currentSessionId, sessionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!Guid.TryParse(User.FindFirstValue("sub"), out Guid userId))
        {
            return Unauthorized();
        }

        Result<OperationCompletedResponse> result = await sender.Send(
            new SignOutAllSessionsCommand(userId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetCurrentIdentity(out Guid userId, out Guid sessionId)
    {
        bool hasUser = Guid.TryParse(User.FindFirstValue("sub"), out userId);
        bool hasSession = Guid.TryParse(User.FindFirstValue("sid"), out sessionId);
        return hasUser && hasSession;
    }
}
