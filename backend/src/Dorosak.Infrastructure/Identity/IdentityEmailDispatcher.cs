using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Identity;

internal sealed class IdentityEmailDispatcher(
    DorosakDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<ApplicationOptions> applicationOptions,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<IdentityEmailDispatcher> logger) : IIdentityEmailDispatcher
{
    private const string VerificationEmailEvent = "identity.email-verification-requested";
    private const string PasswordResetEmailEvent = "identity.password-reset-requested";
    private const string EmailChangeEvent = "identity.email-change-requested";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, Guid, string, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5200, nameof(DeliveryFailed)),
            "Identity email outbox message {MessageId} failed with {ErrorCode}");

    private readonly ApplicationOptions _applicationOptions = applicationOptions.Value;
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        int processed = 0;
        for (int index = 0; index < 20; index++)
        {
            ClaimedMessage? claimed = await ClaimAsync(cancellationToken);
            if (claimed is null)
            {
                break;
            }

            try
            {
                await DeliverAsync(claimed.Message, cancellationToken);
                await CompleteAsync(claimed, cancellationToken);
                processed++;
            }
            catch (Exception exception) when (exception is HttpRequestException or SmtpException or InvalidOperationException)
            {
                string errorCode = exception.GetType().Name;
                DeliveryFailed(logger, claimed.Message.Id, errorCode, exception);
                await ReleaseAsync(claimed, errorCode, cancellationToken);
            }
        }

        return processed;
    }

    private async Task<ClaimedMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ClaimCoreAsync(cancellationToken));
    }

    private async Task<ClaimedMessage?> ClaimCoreAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        OutboxMessage? message = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT *
                FROM operations.outbox_messages
                WHERE processed_at IS NULL
                  AND available_at <= now()
                  AND (locked_until IS NULL OR locked_until <= now())
                  AND event_type IN ('identity.email-verification-requested', 'identity.password-reset-requested', 'identity.email-change-requested')
                ORDER BY available_at, occurred_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        Guid lockToken = Guid.CreateVersion7();
        if (!message.TryAcquire(timeProvider.GetUtcNow(), TimeSpan.FromMinutes(2), lockToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ClaimedMessage(message, lockToken);
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        IdentityEmailRequested payload = JsonSerializer.Deserialize<IdentityEmailRequested>(
            message.Payload,
            JsonOptions)
            ?? throw new InvalidOperationException("Identity email payload is invalid.");
        ApplicationUser? user = await userManager.FindByIdAsync(payload.UserId.ToString("D"));
        if (user?.Email is null || !user.IsActive)
        {
            return;
        }

        string locale = string.Equals(payload.Locale, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
        string link;
        string subject;
        string body;
        string destinationEmail = user.Email;
        if (message.EventType == VerificationEmailEvent)
        {
            if (user.EmailConfirmed)
            {
                return;
            }

            string token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            link = BuildLink(locale, "verify-email", user.Id, token);
            subject = locale == "ar" ? "تأكيد بريدك في دروسك" : "Verify your Dorosak email";
            body = locale == "ar"
                ? $"لتأكيد بريدك افتح الرابط التالي:\n{link}\n\nتنتهي صلاحية الرابط خلال 24 ساعة."
                : $"Verify your email by opening this link:\n{link}\n\nThe link expires in 24 hours.";
        }
        else if (message.EventType == PasswordResetEmailEvent)
        {
            string token = await userManager.GeneratePasswordResetTokenAsync(user);
            link = BuildLink(locale, "reset-password", user.Id, token);
            subject = locale == "ar" ? "إعادة تعيين كلمة مرور دروسك" : "Reset your Dorosak password";
            body = locale == "ar"
                ? $"لإعادة تعيين كلمة المرور افتح الرابط التالي:\n{link}\n\nتنتهي صلاحية الرابط خلال ساعة."
                : $"Reset your password by opening this link:\n{link}\n\nThe link expires in one hour.";
        }
        else if (message.EventType == EmailChangeEvent)
        {
            if (string.IsNullOrWhiteSpace(user.PendingEmail))
            {
                return;
            }

            destinationEmail = user.PendingEmail;
            string token = await userManager.GenerateChangeEmailTokenAsync(user, user.PendingEmail);
            link = BuildLink(locale, "confirm-email-change", user.Id, token);
            subject = locale == "ar" ? "تأكيد بريدك الجديد في دروسك" : "Confirm your new Dorosak email";
            body = locale == "ar"
                ? $"لتأكيد بريدك الجديد افتح الرابط التالي:\n{link}\n\nتنتهي صلاحية الرابط خلال 24 ساعة."
                : $"Confirm your new email by opening this link:\n{link}\n\nThe link expires in 24 hours.";
        }
        else
        {
            throw new InvalidOperationException("The identity email event type is unsupported.");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromAddress, _emailOptions.FromName, Encoding.UTF8),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };
        mail.To.Add(new MailAddress(destinationEmail));
        using var smtp = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
        {
            EnableSsl = _emailOptions.UseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = true,
        };
        await smtp.SendMailAsync(mail, cancellationToken);
    }

    private string BuildLink(string locale, string page, Guid userId, string token)
    {
        string baseUrl = _applicationOptions.PublicUrl.TrimEnd('/');
        return $"{baseUrl}/{locale}/auth/{page}?userId={userId:D}&token={WebUtility.UrlEncode(token)}";
    }

    private async Task CompleteAsync(ClaimedMessage claimed, CancellationToken cancellationToken)
    {
        OutboxMessage message = await dbContext.OutboxMessages.SingleAsync(
            candidate => candidate.Id == claimed.Message.Id,
            cancellationToken);
        message.MarkProcessed(timeProvider.GetUtcNow(), claimed.LockToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseAsync(ClaimedMessage claimed, string errorCode, CancellationToken cancellationToken)
    {
        OutboxMessage message = await dbContext.OutboxMessages.SingleAsync(
            candidate => candidate.Id == claimed.Message.Id,
            cancellationToken);
        TimeSpan retryDelay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8))));
        message.ReleaseAfterFailure(timeProvider.GetUtcNow(), claimed.LockToken, errorCode, retryDelay);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record IdentityEmailRequested(Guid UserId, string Locale);

    private sealed record ClaimedMessage(OutboxMessage Message, Guid LockToken);
}
