using System.Globalization;
using System.Security.Claims;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Api.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class AdminHighRiskRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler(DorosakDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out Guid userId))
        {
            return;
        }

        bool allowed = await dbContext.UserRoles
            .AsNoTracking()
            .Join(
                dbContext.RoleClaims.AsNoTracking(),
                userRole => userRole.RoleId,
                roleClaim => roleClaim.RoleId,
                (userRole, roleClaim) => new { userRole, roleClaim })
            .AnyAsync(
                item => item.userRole.UserId == userId &&
                    item.roleClaim.ClaimType == IdentityConstants.PermissionClaimType &&
                    item.roleClaim.ClaimValue == requirement.Permission,
                context.Resource is HttpContext httpContext
                    ? httpContext.RequestAborted
                    : CancellationToken.None);
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class AdminHighRiskAuthorizationHandler(
    DorosakDbContext dbContext,
    TimeProvider timeProvider) : AuthorizationHandler<AdminHighRiskRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminHighRiskRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out Guid userId) ||
            !int.TryParse(context.User.FindFirstValue("authz_ver"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
        {
            return;
        }

        ApplicationUser? user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.AuthorizationVersion == version,
            CancellationToken.None);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled ||
            !context.User.FindAll("amr").Any(claim => claim.Value is "otp" or "recovery"))
        {
            return;
        }

        string? auditReason = (context.Resource as HttpContext)?.Request.Headers["X-Audit-Reason"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auditReason) || auditReason.Trim().Length < 8)
        {
            return;
        }

        string? authTimeValue = context.User.FindFirstValue("auth_time");
        if (!long.TryParse(authTimeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long authTime) ||
            timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authTime) > TimeSpan.FromMinutes(15))
        {
            return;
        }

        bool allowed = await dbContext.UserRoles
            .AsNoTracking()
            .Join(
                dbContext.RoleClaims.AsNoTracking(),
                userRole => userRole.RoleId,
                roleClaim => roleClaim.RoleId,
                (userRole, roleClaim) => new { userRole, roleClaim })
            .Join(
                dbContext.Roles.AsNoTracking(),
                item => item.roleClaim.RoleId,
                role => role.Id,
                (item, role) => new { item.userRole, item.roleClaim, role })
            .AnyAsync(
                item => item.userRole.UserId == userId &&
                    item.roleClaim.ClaimType == IdentityConstants.PermissionClaimType &&
                    item.roleClaim.ClaimValue == requirement.Permission &&
                    item.role.NormalizedName == "ADMIN",
                CancellationToken.None);
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class PermissionPolicyProvider(
    Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "Permission:";
    public const string HighRiskPrefix = "AdminHighRisk:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string permission = policyName[Prefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build());
        }
        if (policyName.StartsWith(HighRiskPrefix, StringComparison.Ordinal))
        {
            string permission = policyName[HighRiskPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminHighRiskRequirement(permission))
                .Build());
        }

        return base.GetPolicyAsync(policyName);
    }
}
