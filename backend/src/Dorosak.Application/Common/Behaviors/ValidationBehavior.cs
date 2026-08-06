using Dorosak.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Dorosak.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] requestValidators = [.. validators];
        if (requestValidators.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var validationFailures = new List<ValidationFailure>();
        foreach (IValidator<TRequest> validator in requestValidators)
        {
            ValidationResult result = await validator.ValidateAsync(context, cancellationToken);
            validationFailures.AddRange(result.Errors);
        }

        var errors = validationFailures
            .Where(failure => failure is not null)
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }

        return await next(cancellationToken);
    }
}
