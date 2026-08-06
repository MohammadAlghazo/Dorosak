namespace Dorosak.Infrastructure.Identity;

public sealed class PasswordBreachOptions
{
    public const string SectionName = "PasswordBreachCheck";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://api.pwnedpasswords.com/";
}
