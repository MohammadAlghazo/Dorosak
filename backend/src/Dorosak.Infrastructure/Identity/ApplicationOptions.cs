namespace Dorosak.Infrastructure.Identity;

public sealed class ApplicationOptions
{
    public const string SectionName = "App";

    public string PublicUrl { get; init; } = "http://localhost:4200";
}
