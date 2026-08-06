using Microsoft.AspNetCore.Identity;

namespace Dorosak.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}
