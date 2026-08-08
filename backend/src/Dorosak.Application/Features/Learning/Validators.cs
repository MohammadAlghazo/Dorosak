using FluentValidation;

namespace Dorosak.Application.Features.Learning;

internal sealed class EnrollCourseCommandValidator : AbstractValidator<EnrollCourseCommand>
{
    public EnrollCourseCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Locale).Must(IsLocale);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }

    private static bool IsLocale(string locale) => locale is "ar" or "en";
}

internal sealed class GetEnrollmentsQueryValidator : AbstractValidator<GetEnrollmentsQuery>
{
    public GetEnrollmentsQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
    }
}

internal sealed class GetLearningManifestQueryValidator : AbstractValidator<GetLearningManifestQuery>
{
    public GetLearningManifestQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
    }
}

internal sealed class GetLearningLessonQueryValidator : AbstractValidator<GetLearningLessonQuery>
{
    public GetLearningLessonQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.LessonId).NotEmpty();
    }
}

internal sealed class UpdateLessonProgressCommandValidator : AbstractValidator<UpdateLessonProgressCommand>
{
    public UpdateLessonProgressCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.LessonId).NotEmpty();
        RuleFor(request => request.ClientCommandId).NotEmpty();
        RuleFor(request => request.Sequence).GreaterThan(0);
        RuleFor(request => request.PositionSeconds).GreaterThanOrEqualTo(0);
        RuleFor(request => request.WatchedIntervals)
            .NotNull()
            .Must(intervals => intervals is not null && intervals.Count <= 500);
        RuleForEach(request => request.WatchedIntervals).ChildRules(interval =>
        {
            interval.RuleFor(item => item.StartSeconds).GreaterThanOrEqualTo(0);
            interval.RuleFor(item => item.EndSeconds).GreaterThan(item => item.StartSeconds);
        });
    }
}

internal sealed class UpsertLearningNoteCommandValidator : AbstractValidator<UpsertLearningNoteCommand>
{
    public UpsertLearningNoteCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.LessonId).NotEmpty();
        RuleFor(request => request.Text).NotEmpty().MaximumLength(5000);
    }
}

internal sealed class CreateQuizVersionCommandValidator : AbstractValidator<CreateQuizVersionCommand>
{
    public CreateQuizVersionCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.LessonId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AttemptLimit).InclusiveBetween(1, 100);
        RuleFor(request => request.DurationMinutes).InclusiveBetween(1, 1440).When(request => request.DurationMinutes.HasValue);
        RuleFor(request => request.PassScore).InclusiveBetween(0, 100);
        RuleFor(request => request.Questions)
            .NotNull()
            .NotEmpty()
            .Must(questions => questions is not null && questions.Select(question => question.Position).Distinct().Count() == questions.Count);
        RuleForEach(request => request.Questions).ChildRules(question =>
        {
            question.RuleFor(item => item.Position).GreaterThanOrEqualTo(0);
            question.RuleFor(item => item.Type).Must(type => Enum.TryParse<Domain.Assessment.QuizQuestionType>(type, true, out _));
            question.RuleFor(item => item.Prompt).NotEmpty().MaximumLength(10000);
            question.RuleFor(item => item.Points).GreaterThan(0);
            question.RuleFor(item => item.AcceptedAnswer).MaximumLength(2000);
            question.RuleFor(item => item.Options)
                .NotNull()
                .Must(options => options is not null && options.Select(option => option.Position).Distinct().Count() == options.Count);
            question.RuleForEach(item => item.Options).ChildRules(option =>
            {
                option.RuleFor(item => item.Position).GreaterThanOrEqualTo(0);
                option.RuleFor(item => item.Text).NotEmpty().MaximumLength(2000);
            });
        });
    }
}

internal sealed class StartQuizAttemptCommandValidator : AbstractValidator<StartQuizAttemptCommand>
{
    public StartQuizAttemptCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.QuizVersionId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class SubmitQuizAttemptCommandValidator : AbstractValidator<SubmitQuizAttemptCommand>
{
    public SubmitQuizAttemptCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.QuizVersionId).NotEmpty();
        RuleFor(request => request.AttemptId).NotEmpty();
        RuleFor(request => request.Answers).NotNull().Must(answers => answers is not null && answers.Count <= 500);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleForEach(request => request.Answers).ChildRules(answer =>
        {
            answer.RuleFor(item => item.QuestionId).NotEmpty();
            answer.RuleFor(item => item.TextAnswer).MaximumLength(10000);
            answer.RuleFor(item => item.SelectedOptionIds)
                .NotNull()
                .Must(ids => ids is not null && ids.Distinct().Count() == ids.Count);
        });
    }
}

internal sealed class CreateAssignmentVersionCommandValidator : AbstractValidator<CreateAssignmentVersionCommand>
{
    public CreateAssignmentVersionCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.LessonId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Instructions).NotEmpty().MaximumLength(100000);
    }
}

internal sealed class SubmitAssignmentCommandValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.EnrollmentId).NotEmpty();
        RuleFor(request => request.AssignmentVersionId).NotEmpty();
        RuleFor(request => request.Text).NotEmpty().MaximumLength(100000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GradeAssignmentCommandValidator : AbstractValidator<GradeAssignmentCommand>
{
    public GradeAssignmentCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.SubmissionId).NotEmpty();
        RuleFor(request => request.Score).InclusiveBetween(0, 100);
        RuleFor(request => request.Feedback).MaximumLength(10000);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class GradeQuizAttemptCommandValidator : AbstractValidator<GradeQuizAttemptCommand>
{
    public GradeQuizAttemptCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.AttemptId).NotEmpty();
        RuleFor(request => request.Score).InclusiveBetween(0, 100);
        RuleFor(request => request.Feedback).NotNull().MaximumLength(10000);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}
