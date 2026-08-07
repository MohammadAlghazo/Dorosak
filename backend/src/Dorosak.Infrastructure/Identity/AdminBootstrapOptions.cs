namespace Dorosak.Infrastructure.Identity;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public bool Enabled { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string TemporaryPassword { get; init; } = string.Empty;

    public string TotpSecret { get; init; } = string.Empty;
}
