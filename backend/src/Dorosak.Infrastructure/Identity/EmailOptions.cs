namespace Dorosak.Infrastructure.Identity;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; init; } = "127.0.0.1";

    public int SmtpPort { get; init; } = 1026;

    public bool UseTls { get; init; }

    public string FromAddress { get; init; } = "noreply@dorosak.test";

    public string FromName { get; init; } = "Dorosak";
}
