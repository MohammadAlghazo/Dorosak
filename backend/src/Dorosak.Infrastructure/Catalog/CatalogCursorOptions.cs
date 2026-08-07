namespace Dorosak.Infrastructure.Catalog;

public sealed class CatalogCursorOptions
{
    public const string SectionName = "Catalog:Cursors";

    public string SigningKey { get; init; } = string.Empty;

    public string Environment { get; init; } = "development";

    public string SchemaVersion { get; init; } = "phase6";
}
