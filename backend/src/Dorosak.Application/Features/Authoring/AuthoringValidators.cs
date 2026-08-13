using FluentValidation;

namespace Dorosak.Application.Features.Authoring;

internal static class ValidationHelpers
{
    public static readonly string[] Locales = ["ar", "en"];
    public static readonly string[] Levels = ["Beginner", "Intermediate", "Advanced", "AllLevels"];
    public static readonly string[] LessonTypes = ["Video", "Article", "Document", "Quiz", "Assignment"];

    public static bool IsLocale(string locale) => Locales.Contains(locale, StringComparer.OrdinalIgnoreCase);
    public static bool IsPlainText(string value) => !value.Contains('<') && !value.Contains('>');
}

internal sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(request => request.DefaultLocale).Must(ValidationHelpers.IsLocale);
        RuleFor(request => request.Level).Must(level => ValidationHelpers.Levels.Contains(level, StringComparer.OrdinalIgnoreCase));
        RuleFor(request => request.Localizations).NotEmpty().Must(HaveOnlySupportedLocales);
        RuleFor(request => request.Localizations).Must(HaveUniqueLocales);
        RuleForEach(request => request.Localizations).SetValidator(new CourseLocalizationInputValidator());
        RuleFor(request => request.CategoryCodes).Must(codes => codes.Count <= 20 && codes.All(IsCode));
        RuleFor(request => request.TagCodes).Must(codes => codes.Count <= 30 && codes.All(IsCode));
    }

    private static bool HaveOnlySupportedLocales(IReadOnlyList<CourseLocalizationInput> values) => values.All(value => ValidationHelpers.IsLocale(value.Locale));
    private static bool HaveUniqueLocales(IReadOnlyList<CourseLocalizationInput> values) => values.Select(value => value.Locale.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static bool IsCode(string value) => value.Length is >= 2 and <= 80 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}

internal sealed class CourseLocalizationInputValidator : AbstractValidator<CourseLocalizationInput>
{
    public CourseLocalizationInputValidator()
    {
        RuleFor(input => input.Locale).Must(ValidationHelpers.IsLocale);
        RuleFor(input => input.Title).NotEmpty().MaximumLength(200).Must(ValidationHelpers.IsPlainText);
        RuleFor(input => input.Subtitle).MaximumLength(300).Must(ValidationHelpers.IsPlainText);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(10000).Must(ValidationHelpers.IsPlainText);
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

internal sealed class UpdateCourseMetadataCommandValidator : AbstractValidator<UpdateCourseMetadataCommand>
{
    public UpdateCourseMetadataCommandValidator()
    {
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(1).When(request => request.ExpectedVersion.HasValue);
        RuleFor(request => request.DefaultLocale).Must(ValidationHelpers.IsLocale);
        RuleFor(request => request.Level).Must(level => ValidationHelpers.Levels.Contains(level, StringComparer.OrdinalIgnoreCase));
        RuleFor(request => request.Localizations).NotEmpty().Must(values => values.All(value => ValidationHelpers.IsLocale(value.Locale)));
        RuleFor(request => request.Localizations).Must(values => values.Select(value => value.Locale.ToLowerInvariant()).Distinct().Count() == values.Count);
        RuleForEach(request => request.Localizations).SetValidator(new CourseLocalizationInputValidator());
    }
}

internal sealed class ArchiveCourseCommandValidator : AbstractValidator<ArchiveCourseCommand>
{
    public ArchiveCourseCommandValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000).Must(ValidationHelpers.IsPlainText);
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
    private static bool HaveUniquePositions(IReadOnlyList<SectionInput> values) => values.Select(value => value.Position).Distinct().Count() == values.Count;
}

internal sealed class SectionInputValidator : AbstractValidator<SectionInput>
{
    public SectionInputValidator()
    {
        RuleFor(section => section.Position).GreaterThanOrEqualTo(0);
        RuleFor(section => section.Title).NotEmpty().MaximumLength(200).Must(ValidationHelpers.IsPlainText);
        RuleFor(section => section.Lessons).NotEmpty().Must(lessons => lessons.Count <= 500);
        RuleFor(section => section.Lessons).Must(lessons => lessons.Select(lesson => lesson.Position).Distinct().Count() == lessons.Count);
        RuleForEach(section => section.Lessons).SetValidator(new LessonInputValidator());
    }
}

internal sealed class LessonInputValidator : AbstractValidator<LessonInput>
{
    public LessonInputValidator()
    {
        RuleFor(lesson => lesson.Position).GreaterThanOrEqualTo(0);
        RuleFor(lesson => lesson.Title).NotEmpty().MaximumLength(200).Must(ValidationHelpers.IsPlainText);
        RuleFor(lesson => lesson.LessonType).Must(type => ValidationHelpers.LessonTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        RuleFor(lesson => lesson.Content).MaximumLength(100000).Must(ValidationHelpers.IsPlainText);
    }
}

internal sealed class AddCollaboratorCommandValidator : AbstractValidator<AddCollaboratorCommand>
{
    public AddCollaboratorCommandValidator() => RuleFor(request => request.Role).Must(role => role is "Editor" or "CoInstructor" or "Reviewer");
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
internal sealed class StartNewDraftCommandValidator : AbstractValidator<StartNewDraftCommand>;
