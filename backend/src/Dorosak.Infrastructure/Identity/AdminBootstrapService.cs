using Dorosak.Domain.Identity;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Dorosak.Infrastructure.Identity;

internal sealed class AdminBootstrapService(
    DorosakDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<AdminBootstrapOptions> options,
    TimeProvider timeProvider) : IAdminBootstrapService
{
    private readonly AdminBootstrapOptions _options = options.Value;
    private readonly IDataProtector _mfaProtector =
        dataProtectionProvider.CreateProtector("Dorosak.Identity.Mfa.v1");

    public async Task<AdminBootstrapResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new AdminBootstrapResult(false, false);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ExecuteCoreAsync(cancellationToken));
    }

    private async Task<AdminBootstrapResult> ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(IdentityConstants.AdminRole);
        if (administrators.Count > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new AdminBootstrapResult(false, true);
        }

        if (await userManager.FindByEmailAsync(_options.Email) is not null)
        {
            throw new InvalidOperationException("The bootstrap email is already assigned to a non-administrator account.");
        }

        _ = Base32Encoding.ToBytes(_options.TotpSecret);
        DateTimeOffset now = timeProvider.GetUtcNow();
        ApplicationUser user = ApplicationUser.Create(_options.DisplayName, _options.Email, now);
        user.EmailConfirmed = true;
        user.TwoFactorEnabled = true;
        user.ProtectedMfaSecret = _mfaProtector.Protect(_options.TotpSecret);

        IdentityResult created = await userManager.CreateAsync(user, _options.TemporaryPassword);
        EnsureSucceeded(created, "The bootstrap administrator could not be created.");
        EnsureSucceeded(
            await userManager.AddToRoleAsync(user, IdentityConstants.StudentRole),
            "The bootstrap Student role could not be assigned.");
        EnsureSucceeded(
            await userManager.AddToRoleAsync(user, IdentityConstants.AdminRole),
            "The bootstrap Admin role could not be assigned.");

        dbContext.UserProfiles.Add(UserProfile.Create(user.Id, _options.DisplayName, now));
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            null,
            "administrator.bootstrapped",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdminBootstrapResult(true, false);
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(message);
        }
    }
}
