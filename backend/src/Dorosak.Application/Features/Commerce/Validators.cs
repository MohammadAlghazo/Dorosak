using FluentValidation;

namespace Dorosak.Application.Features.Commerce;

internal sealed class CreateDemoCheckoutCommandValidator : AbstractValidator<CreateDemoCheckoutCommand>
{
    public CreateDemoCheckoutCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Outcome).Must(value => value is "success" or "failure");
        RuleFor(request => request.Locale).Must(value => value is "ar" or "en");
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GetDemoSubscriptionQueryValidator : AbstractValidator<GetDemoSubscriptionQuery>
{
    public GetDemoSubscriptionQueryValidator() => RuleFor(request => request.UserId).NotEmpty();
}

internal sealed class ActivateDemoSubscriptionCommandValidator : AbstractValidator<ActivateDemoSubscriptionCommand>
{
    public ActivateDemoSubscriptionCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CancelDemoSubscriptionCommandValidator : AbstractValidator<CancelDemoSubscriptionCommand>
{
    public CancelDemoSubscriptionCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.SubscriptionId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
