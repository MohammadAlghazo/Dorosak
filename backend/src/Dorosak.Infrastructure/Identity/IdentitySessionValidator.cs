using System.Globalization;
using System.Security.Claims;
using Dorosak.Domain.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Identity;

public interface IIdentitySessionValidator
{
    Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

internal sealed class IdentitySessionValidator(
    DorosakDbContext dbContext,
    TimeProvider timeProvider) : IIdentitySessionValidator
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        string? userValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue("sub");
        string? sessionValue = principal.FindFirstValue("sid");
        string? versionValue = principal.FindFirstValue("authz_ver");
        if (!Guid.TryParse(userValue, out Guid userId) ||
            !Guid.TryParse(sessionValue, out Guid sessionId) ||
            !int.TryParse(versionValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int authorizationVersion))
        {
            return false;
        }

        ApplicationUser? user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || user.AuthorizationVersion != authorizationVersion)
        {
            return false;
        }

        RefreshSession? session = await dbContext.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == sessionId && candidate.UserId == userId, cancellationToken);
        return session is not null && session.IsActive(timeProvider.GetUtcNow());
    }
}
