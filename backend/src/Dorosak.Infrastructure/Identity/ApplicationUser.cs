using Microsoft.AspNetCore.Identity;

namespace Dorosak.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SecurityVersion { get; set; } = 1;

    public int AuthorizationVersion { get; set; } = 1;

    public string? ProtectedMfaSecret { get; set; }

    public string? ProtectedPendingMfaSecret { get; set; }

    public long? LastMfaTimeStep { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static ApplicationUser Create(string displayName, string email, DateTimeOffset now)
    {
        string normalizedEmail = email.Trim();
        return new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        };
    }
}
