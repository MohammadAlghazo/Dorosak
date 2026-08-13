namespace Dorosak.Api;

public static class ApiConstants
{
    public const string CorsPolicy = "DorosakCors";

    public const string PublicRateLimitPolicy = "PublicApi";

    public const string SensitiveRateLimitPolicy = "SensitiveApi";

    public const string SearchRateLimitPolicy = "PublicSearch";

    public const string UploadRateLimitPolicy = "MediaUploads";

    public const string PublicOutputCachePolicy = "PublicShort";

    public const string CatalogOutputCachePolicy = "CatalogPublic";

    public const string TaxonomyOutputCachePolicy = "CatalogTaxonomy";

    public const string CmsOutputCachePolicy = "CmsPublic";

    public const string TaxonomyCacheTag = "catalog-taxonomy";

    public const string CatalogCacheTag = "catalog-public";

    public const string CmsCacheTag = "cms-public";
}
