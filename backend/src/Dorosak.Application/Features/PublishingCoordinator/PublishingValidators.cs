using FluentValidation;

namespace Dorosak.Application.Features.PublishingCoordinator;

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
