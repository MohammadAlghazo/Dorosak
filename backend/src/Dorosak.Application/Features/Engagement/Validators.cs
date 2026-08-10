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

internal sealed class GetDiscussionThreadsQueryValidator : AbstractValidator<GetDiscussionThreadsQuery>
{
    public GetDiscussionThreadsQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
        RuleFor(request => request.Cursor).MaximumLength(1000);
    }
}

internal sealed class GetDiscussionThreadQueryValidator : AbstractValidator<GetDiscussionThreadQuery>
{
    public GetDiscussionThreadQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.CommentLimit).InclusiveBetween(1, 100);
        RuleFor(request => request.CommentCursor).MaximumLength(1000);
    }
}

internal sealed class CreateDiscussionThreadCommandValidator : AbstractValidator<CreateDiscussionThreadCommand>
{
    public CreateDiscussionThreadCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(10000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateDiscussionThreadCommandValidator : AbstractValidator<UpdateDiscussionThreadCommand>
{
    public UpdateDiscussionThreadCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(10000);
    }
}

internal sealed class DeleteDiscussionThreadCommandValidator : AbstractValidator<DeleteDiscussionThreadCommand>
{
    public DeleteDiscussionThreadCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
    }
}

internal sealed class CreateDiscussionCommentCommandValidator : AbstractValidator<CreateDiscussionCommentCommand>
{
    public CreateDiscussionCommentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.Body).NotEmpty().MaximumLength(5000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateDiscussionCommentCommandValidator : AbstractValidator<UpdateDiscussionCommentCommand>
{
    public UpdateDiscussionCommentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.CommentId).NotEmpty();
        RuleFor(request => request.Body).NotEmpty().MaximumLength(5000);
    }
}

internal sealed class DeleteDiscussionCommentCommandValidator : AbstractValidator<DeleteDiscussionCommentCommand>
{
    public DeleteDiscussionCommentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.CommentId).NotEmpty();
    }
}

internal sealed class LikeDiscussionCommentCommandValidator : AbstractValidator<LikeDiscussionCommentCommand>
{
    public LikeDiscussionCommentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.CommentId).NotEmpty();
    }
}

internal sealed class UnlikeDiscussionCommentCommandValidator : AbstractValidator<UnlikeDiscussionCommentCommand>
{
    public UnlikeDiscussionCommentCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Scope).NotNull().Must(scope => scope.IsValid);
        RuleFor(request => request.ThreadId).NotEmpty();
        RuleFor(request => request.CommentId).NotEmpty();
    }
}
