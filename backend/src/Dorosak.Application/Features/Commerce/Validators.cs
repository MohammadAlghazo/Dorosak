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
