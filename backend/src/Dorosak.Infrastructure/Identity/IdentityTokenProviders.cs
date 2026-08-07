using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Identity;

internal sealed class EmailVerificationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(
        dataProtectionProvider,
        Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
        {
            Name = "DorosakEmailVerification",
            TokenLifespan = TimeSpan.FromHours(24),
        }),
        logger);

internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(
        dataProtectionProvider,
        Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
        {
            Name = "DorosakPasswordReset",
            TokenLifespan = TimeSpan.FromHours(1),
        }),
        logger);
