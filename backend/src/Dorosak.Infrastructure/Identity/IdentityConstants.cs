namespace Dorosak.Infrastructure.Identity;

public static class IdentityConstants
{
    public const string PermissionClaimType = "permission";

    public const string StudentRole = "Student";

    public const string TeacherRole = "Teacher";

    public const string AdminRole = "Admin";

    public static readonly Guid StudentRoleId = Guid.Parse("018f3f0e-4380-7b1b-8f8d-b8ea9c546001");

    public static readonly Guid TeacherRoleId = Guid.Parse("018f3f0e-4380-7b1b-8f8d-b8ea9c546002");

    public static readonly Guid AdminRoleId = Guid.Parse("018f3f0e-4380-7b1b-8f8d-b8ea9c546003");
}
