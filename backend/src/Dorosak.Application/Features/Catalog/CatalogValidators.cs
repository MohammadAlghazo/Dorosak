using FluentValidation;

namespace Dorosak.Application.Features.Catalog;

internal static class CatalogValidationHelpers
{
    public static readonly string[] Locales = ["ar", "en"];
    public static bool IsLocale(string locale) => Locales.Contains(locale, StringComparer.OrdinalIgnoreCase);
    public static bool IsPlainText(string value) => !value.Contains('<') && !value.Contains('>');
}

internal sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class GetTagsQueryValidator : AbstractValidator<GetTagsQuery>
{
    public GetTagsQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class UpsertCategoryCommandValidator : AbstractValidator<UpsertCategoryCommand>
{
    public UpsertCategoryCommandValidator()
    {
        RuleFor(request => request.Code).NotEmpty().Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").MaximumLength(80);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Localizations).Must(HaveArabicAndEnglish);
        RuleForEach(request => request.Localizations).SetValidator(new TaxonomyLocalizationInputValidator());
    }

    private static bool HaveArabicAndEnglish(IReadOnlyList<TaxonomyLocalizationInput> values) =>
        values.Select(value => value.Locale.ToLowerInvariant()).Distinct().OrderBy(value => value).SequenceEqual(["ar", "en"]);
}

internal sealed class UpsertTagCommandValidator : AbstractValidator<UpsertTagCommand>
{
    public UpsertTagCommandValidator()
    {
        RuleFor(request => request.Code).NotEmpty().Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").MaximumLength(80);
        RuleFor(request => request.Localizations).Must(values => values.Count == 2 &&
            values.Select(value => value.Locale.ToLowerInvariant()).Distinct().OrderBy(value => value).SequenceEqual(["ar", "en"]));
        RuleForEach(request => request.Localizations).SetValidator(new TaxonomyLocalizationInputValidator());
    }
}

internal sealed class TaxonomyLocalizationInputValidator : AbstractValidator<TaxonomyLocalizationInput>
{
    public TaxonomyLocalizationInputValidator()
    {
        RuleFor(input => input.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(200).Must(CatalogValidationHelpers.IsPlainText);
    }
}

internal sealed class GetCatalogCoursesQueryValidator : AbstractValidator<GetCatalogCoursesQuery>
{
    public GetCatalogCoursesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Sort).Must(sort => sort is "" or "newest" or "title" or "popular");
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class GetPublicCourseQueryValidator : AbstractValidator<GetPublicCourseQuery>
{
    public GetPublicCourseQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(160).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

internal sealed class SearchCoursesQueryValidator : AbstractValidator<SearchCoursesQuery>
{
    public SearchCoursesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Query).MaximumLength(200);
        RuleFor(request => request.Sort).Must(sort => sort is "" or "relevance" or "newest" or "title" or "popular");
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class SuggestCourseSuggestionsQueryValidator : AbstractValidator<SuggestCourseSuggestionsQuery>
{
    public SuggestCourseSuggestionsQueryValidator()
    {
        RuleFor(request => request.Locale).Must(CatalogValidationHelpers.IsLocale);
        RuleFor(request => request.Query).MaximumLength(200);
        RuleFor(request => request.Limit).InclusiveBetween(1, 20);
    }
}
