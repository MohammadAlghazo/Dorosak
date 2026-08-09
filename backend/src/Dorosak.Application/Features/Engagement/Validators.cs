using FluentValidation;

namespace Dorosak.Application.Features.Engagement;

internal sealed class GetCourseReviewsQueryValidator : AbstractValidator<GetCourseReviewsQuery>
{
    public GetCourseReviewsQueryValidator()
    {
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateCourseReviewCommandValidator : AbstractValidator<CreateCourseReviewCommand>
{
    public CreateCourseReviewCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Rating).InclusiveBetween((short)1, (short)5);
        RuleFor(request => request.Text).MaximumLength(4000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GetMyCourseReviewQueryValidator : AbstractValidator<GetMyCourseReviewQuery>
{
    public GetMyCourseReviewQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
    }
}

internal sealed class UpdateCourseReviewCommandValidator : AbstractValidator<UpdateCourseReviewCommand>
{
    public UpdateCourseReviewCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.ReviewId).NotEmpty();
        RuleFor(request => request.Rating).InclusiveBetween((short)1, (short)5);
        RuleFor(request => request.Text).MaximumLength(4000);
    }
}

internal sealed class DeleteCourseReviewCommandValidator : AbstractValidator<DeleteCourseReviewCommand>
{
    public DeleteCourseReviewCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.ReviewId).NotEmpty();
    }
}
