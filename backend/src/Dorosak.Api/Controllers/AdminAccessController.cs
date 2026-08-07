using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/access")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAccessController : ControllerBase
{
    [HttpGet]
    [PermissionPolicy("User.ReadAny")]
    public IActionResult GetAccess() => Ok(new ApiResponse<AdminAccessResponse>(new AdminAccessResponse(true)));
}

public sealed record AdminAccessResponse(bool Granted);
