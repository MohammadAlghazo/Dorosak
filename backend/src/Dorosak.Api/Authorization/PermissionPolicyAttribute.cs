using Microsoft.AspNetCore.Authorization;

namespace Dorosak.Api.Authorization;

public sealed class PermissionPolicyAttribute : AuthorizeAttribute
{
    public PermissionPolicyAttribute(string permission)
    {
        Policy = $"{PermissionPolicyProvider.Prefix}{permission}";
    }
}

public sealed class AdminHighRiskPolicyAttribute : AuthorizeAttribute
{
    public AdminHighRiskPolicyAttribute(string permission)
    {
        Policy = $"{PermissionPolicyProvider.HighRiskPrefix}{permission}";
    }
}
