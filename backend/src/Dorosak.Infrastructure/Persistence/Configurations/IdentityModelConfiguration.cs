using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users", "identity");
        builder.HasKey(user => user.Id).HasName("pk_identity_users");
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.PendingEmail).HasMaxLength(320);
        builder.Property(user => user.UserName).HasMaxLength(320);
        builder.Property(user => user.NormalizedUserName).HasMaxLength(320);
        builder.Property(user => user.Email).HasMaxLength(320);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(320);
        builder.Property(user => user.PasswordHash).HasMaxLength(1000);
        builder.Property(user => user.SecurityStamp).HasMaxLength(1000);
        builder.Property(user => user.ConcurrencyStamp).HasMaxLength(1000);
        builder.Property(user => user.PhoneNumber).HasMaxLength(100);
        builder.Property(user => user.ProtectedMfaSecret).HasMaxLength(4000);
        builder.Property(user => user.ProtectedPendingMfaSecret).HasMaxLength(4000);
        builder.Property(user => user.SecurityVersion).HasDefaultValue(1);
        builder.Property(user => user.AuthorizationVersion).HasDefaultValue(1);
        builder.Property(user => user.IsActive).HasDefaultValue(true);

        builder
            .HasIndex(user => user.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("uq_identity_users_normalized_user_name")
            .HasFilter("normalized_user_name IS NOT NULL");
        builder
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("uq_identity_users_normalized_email")
            .HasFilter("normalized_email IS NOT NULL");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    private static readonly DateTimeOffset SeededAt =
        new(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles", "identity");
        builder.HasKey(role => role.Id).HasName("pk_identity_roles");
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(100);
        builder.Property(role => role.NormalizedName).HasMaxLength(100);
        builder.Property(role => role.ConcurrencyStamp).HasMaxLength(1000);
        builder
            .HasIndex(role => role.NormalizedName)
            .IsUnique()
            .HasDatabaseName("uq_identity_roles_normalized_name")
            .HasFilter("normalized_name IS NOT NULL");

        builder.HasData(
            CreateRole(DorosakIdentityConstants.StudentRoleId, DorosakIdentityConstants.StudentRole),
            CreateRole(DorosakIdentityConstants.TeacherRoleId, DorosakIdentityConstants.TeacherRole),
            CreateRole(DorosakIdentityConstants.AdminRoleId, DorosakIdentityConstants.AdminRole));
    }

    private static ApplicationRole CreateRole(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString("N"),
        CreatedAt = SeededAt,
    };
}

internal sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims", "identity");
        builder.HasKey(claim => claim.Id).HasName("pk_identity_user_claims");
        builder.Property(claim => claim.ClaimType).HasMaxLength(200);
        builder.Property(claim => claim.ClaimValue).HasMaxLength(1000);
        builder.HasIndex(claim => claim.UserId).HasDatabaseName("ix_identity_user_claims_user_id");
    }
}

internal sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("role_claims", "identity");
        builder.HasKey(claim => claim.Id).HasName("pk_identity_role_claims");
        builder.Property(claim => claim.ClaimType).HasMaxLength(200);
        builder.Property(claim => claim.ClaimValue).HasMaxLength(1000);
        builder.HasIndex(claim => claim.RoleId).HasDatabaseName("ix_identity_role_claims_role_id");
        builder
            .HasIndex(claim => new { claim.RoleId, claim.ClaimType, claim.ClaimValue })
            .IsUnique()
            .HasDatabaseName("uq_identity_role_claims_role_type_value");
        builder.HasData(CreateSeedClaims());
    }

    private static IdentityRoleClaim<Guid>[] CreateSeedClaims()
    {
        var claims = new List<IdentityRoleClaim<Guid>>();
        int id = 1;
        AddRoleClaims(DorosakIdentityConstants.StudentRoleId, Permissions.Student, claims, ref id);
        AddRoleClaims(DorosakIdentityConstants.TeacherRoleId, Permissions.Teacher, claims, ref id);
        AddRoleClaims(DorosakIdentityConstants.AdminRoleId, Permissions.All, claims, ref id);
        return [.. claims];
    }

    private static void AddRoleClaims(
        Guid roleId,
        IEnumerable<string> permissions,
        List<IdentityRoleClaim<Guid>> claims,
        ref int id)
    {
        foreach (string permission in permissions.Distinct(StringComparer.Ordinal))
        {
            claims.Add(new IdentityRoleClaim<Guid>
            {
                Id = id++,
                RoleId = roleId,
                ClaimType = DorosakIdentityConstants.PermissionClaimType,
                ClaimValue = permission,
            });
        }
    }
}

internal sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("user_roles", "identity");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId }).HasName("pk_identity_user_roles");
        builder.HasIndex(userRole => userRole.RoleId).HasDatabaseName("ix_identity_user_roles_role_id");
    }
}

internal sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins", "identity");
        builder
            .HasKey(login => new { login.LoginProvider, login.ProviderKey })
            .HasName("pk_identity_user_logins");
        builder.Property(login => login.LoginProvider).HasMaxLength(200);
        builder.Property(login => login.ProviderKey).HasMaxLength(500);
        builder.Property(login => login.ProviderDisplayName).HasMaxLength(300);
        builder.HasIndex(login => login.UserId).HasDatabaseName("ix_identity_user_logins_user_id");
    }
}

internal sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens", "identity");
        builder
            .HasKey(token => new { token.UserId, token.LoginProvider, token.Name })
            .HasName("pk_identity_user_tokens");
        builder.Property(token => token.LoginProvider).HasMaxLength(200);
        builder.Property(token => token.Name).HasMaxLength(200);
        builder.Property(token => token.Value).HasMaxLength(4000);
    }
}
