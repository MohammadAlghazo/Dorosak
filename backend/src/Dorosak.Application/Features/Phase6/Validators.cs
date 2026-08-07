using FluentValidation;

namespace Dorosak.Application.Features.Phase6;

internal static class Phase6Validation
{
    public static readonly string[] Locales = ["ar", "en"];
    public static readonly string[] Levels = ["Beginner", "Intermediate", "Advanced", "AllLevels"];
    public static readonly string[] LessonTypes = ["Video", "Article", "Document", "Quiz", "Assignment"];

    public static bool IsLocale(string locale) => Locales.Contains(locale, StringComparer.OrdinalIgnoreCase);

    public static bool IsPlainText(string value) => !value.Contains('<') && !value.Contains('>');
}

internal sealed class SubmitTeacherApplicationCommandValidator : AbstractValidator<SubmitTeacherApplicationCommand>
{
    public SubmitTeacherApplicationCommandValidator()
    {
        RuleFor(request => request.Headline).NotEmpty().MinimumLength(2).MaximumLength(160).Must(Phase6Validation.IsPlainText);
        RuleFor(request => request.Biography).NotEmpty().MaximumLength(4000).Must(Phase6Validation.IsPlainText);
        RuleFor(request => request.Expertise).NotEmpty().MaximumLength(1000).Must(Phase6Validation.IsPlainText);
        RuleFor(request => request.Motivation).NotEmpty().MaximumLength(4000).Must(Phase6Validation.IsPlainText);
    }
}

internal sealed class ReviewTeacherApplicationCommandValidator : AbstractValidator<ReviewTeacherApplicationCommand>
{
    public ReviewTeacherApplicationCommandValidator()
    {
        RuleFor(request => request.ApplicationId).NotEmpty();
        RuleFor(request => request.Decision).Must(value => value is "start" or "approve" or "reject");
        RuleFor(request => request.Reason)
            .MaximumLength(2000)
            .Must((request, reason) => request.Decision != "reject" || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A rejection reason is required.");
    }
}

internal sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(request => request.DefaultLocale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Level).Must(level => Phase6Validation.Levels.Contains(level, StringComparer.OrdinalIgnoreCase));
        RuleFor(request => request.Localizations).NotEmpty().Must(HaveOnlySupportedLocales);
        RuleFor(request => request.Localizations).Must(HaveUniqueLocales);
        RuleForEach(request => request.Localizations).SetValidator(new CourseLocalizationInputValidator());
        RuleFor(request => request.CategoryCodes).Must(codes => codes.Count <= 20 && codes.All(IsCode));
        RuleFor(request => request.TagCodes).Must(codes => codes.Count <= 30 && codes.All(IsCode));
    }

    private static bool HaveOnlySupportedLocales(IReadOnlyList<CourseLocalizationInput> values) =>
        values.All(value => Phase6Validation.IsLocale(value.Locale));

    private static bool HaveUniqueLocales(IReadOnlyList<CourseLocalizationInput> values) =>
        values.Select(value => value.Locale.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Count() == values.Count;

    private static bool IsCode(string value) =>
        value.Length is >= 2 and <= 80 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}

internal sealed class CourseLocalizationInputValidator : AbstractValidator<CourseLocalizationInput>
{
    public CourseLocalizationInputValidator()
    {
        RuleFor(input => input.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(input => input.Title).NotEmpty().MaximumLength(200).Must(Phase6Validation.IsPlainText);
        RuleFor(input => input.Subtitle).MaximumLength(300).Must(Phase6Validation.IsPlainText);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(10000).Must(Phase6Validation.IsPlainText);
        RuleFor(input => input.Slug).MaximumLength(160).Must(slug => slug is null || IsSlug(slug));
    }

    private static bool IsSlug(string value) => string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') &&
        !value.StartsWith('-') && !value.EndsWith('-') && !value.Contains("--", StringComparison.Ordinal);
}

internal sealed class GetInstructorCoursesQueryValidator : AbstractValidator<GetInstructorCoursesQuery>
{
    public GetInstructorCoursesQueryValidator() => RuleFor(request => request.Limit).InclusiveBetween(1, 100);
}

internal sealed class GetTeacherApplicationsQueryValidator : AbstractValidator<GetTeacherApplicationsQuery>
{
    public GetTeacherApplicationsQueryValidator() => RuleFor(request => request.Limit).InclusiveBetween(1, 100);
}

internal sealed class UpdateCourseMetadataCommandValidator : AbstractValidator<UpdateCourseMetadataCommand>
{
    public UpdateCourseMetadataCommandValidator()
    {
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(1).When(request => request.ExpectedVersion.HasValue);
        RuleFor(request => request.DefaultLocale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Level).Must(level => Phase6Validation.Levels.Contains(level, StringComparer.OrdinalIgnoreCase));
        RuleFor(request => request.Localizations).NotEmpty().Must(values => values.All(value => Phase6Validation.IsLocale(value.Locale)));
        RuleFor(request => request.Localizations).Must(values => values.Select(value => value.Locale.ToLowerInvariant()).Distinct().Count() == values.Count);
        RuleForEach(request => request.Localizations).SetValidator(new CourseLocalizationInputValidator());
    }
}

internal sealed class ArchiveCourseCommandValidator : AbstractValidator<ArchiveCourseCommand>
{
    public ArchiveCourseCommandValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000).Must(Phase6Validation.IsPlainText);
}

internal sealed class UpdateCurriculumCommandValidator : AbstractValidator<UpdateCurriculumCommand>
{
    public UpdateCurriculumCommandValidator()
    {
        RuleFor(request => request.ExpectedVersion).NotNull().GreaterThanOrEqualTo(1);
        RuleFor(request => request.Sections).NotEmpty().Must(sections => sections.Count <= 100);
        RuleFor(request => request.Sections).Must(HaveUniquePositions);
        RuleForEach(request => request.Sections).SetValidator(new SectionInputValidator());
    }

    private static bool HaveUniquePositions(IReadOnlyList<SectionInput> values) =>
        values.Select(value => value.Position).Distinct().Count() == values.Count;
}

internal sealed class SectionInputValidator : AbstractValidator<SectionInput>
{
    public SectionInputValidator()
    {
        RuleFor(section => section.Position).GreaterThanOrEqualTo(0);
        RuleFor(section => section.Title).NotEmpty().MaximumLength(200).Must(Phase6Validation.IsPlainText);
        RuleFor(section => section.Lessons).NotEmpty().Must(lessons => lessons.Count <= 500);
        RuleFor(section => section.Lessons)
            .Must(lessons => lessons.Select(lesson => lesson.Position).Distinct().Count() == lessons.Count);
        RuleForEach(section => section.Lessons).SetValidator(new LessonInputValidator());
    }
}

internal sealed class LessonInputValidator : AbstractValidator<LessonInput>
{
    public LessonInputValidator()
    {
        RuleFor(lesson => lesson.Position).GreaterThanOrEqualTo(0);
        RuleFor(lesson => lesson.Title).NotEmpty().MaximumLength(200).Must(Phase6Validation.IsPlainText);
        RuleFor(lesson => lesson.LessonType).Must(type => Phase6Validation.LessonTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        RuleFor(lesson => lesson.Content).MaximumLength(100000).Must(Phase6Validation.IsPlainText);
    }
}

internal sealed class AddCollaboratorCommandValidator : AbstractValidator<AddCollaboratorCommand>
{
    public AddCollaboratorCommandValidator() =>
        RuleFor(request => request.Role).Must(role => role is "Editor" or "CoInstructor" or "Reviewer");
}

internal sealed class RemoveCollaboratorCommandValidator : AbstractValidator<RemoveCollaboratorCommand>;

internal sealed class TransferCourseOwnershipCommandValidator : AbstractValidator<TransferCourseOwnershipCommand>
{
    public TransferCourseOwnershipCommandValidator()
    {
        RuleFor(request => request.NewOwnerUserId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).NotNull().GreaterThanOrEqualTo(1);
    }
}

internal sealed class RequestPublicationCommandValidator : AbstractValidator<RequestPublicationCommand>;

internal sealed class WithdrawPublicationCommandValidator : AbstractValidator<WithdrawPublicationCommand>;

internal sealed class ReviewPublicationCommandValidator : AbstractValidator<ReviewPublicationCommand>
{
    public ReviewPublicationCommandValidator()
    {
        RuleFor(request => request.ReviewId).NotEmpty();
        RuleFor(request => request.Decision).Must(value => value is "changesRequested" or "approve");
        RuleFor(request => request.Reason)
            .MaximumLength(2000)
            .Must((request, reason) => request.Decision != "changesRequested" || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required when requesting changes.");
    }
}

internal sealed class GetPublicationReviewsQueryValidator : AbstractValidator<GetPublicationReviewsQuery>
{
    public GetPublicationReviewsQueryValidator() => RuleFor(request => request.Limit).InclusiveBetween(1, 100);
}

internal sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class GetTagsQueryValidator : AbstractValidator<GetTagsQuery>
{
    public GetTagsQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
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
        RuleFor(input => input.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(200).Must(Phase6Validation.IsPlainText);
    }
}

internal sealed class GetCatalogCoursesQueryValidator : AbstractValidator<GetCatalogCoursesQuery>
{
    public GetCatalogCoursesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Sort).Must(sort => sort is "" or "newest" or "title" or "popular");
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class GetPublicCourseQueryValidator : AbstractValidator<GetPublicCourseQuery>
{
    public GetPublicCourseQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(160).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

internal sealed class SearchCoursesQueryValidator : AbstractValidator<SearchCoursesQuery>
{
    public SearchCoursesQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Query).MaximumLength(200);
        RuleFor(request => request.Sort).Must(sort => sort is "" or "relevance" or "newest" or "title" or "popular");
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class SuggestCourseSuggestionsQueryValidator : AbstractValidator<SuggestCourseSuggestionsQuery>
{
    public SuggestCourseSuggestionsQueryValidator()
    {
        RuleFor(request => request.Locale).Must(Phase6Validation.IsLocale);
        RuleFor(request => request.Query).MaximumLength(200);
    }
}
