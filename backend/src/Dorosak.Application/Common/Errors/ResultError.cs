namespace Dorosak.Application.Common.Errors;

public sealed record ResultError(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    TimeSpan? RetryAfter = null)
{
    public static readonly ResultError None = new(string.Empty, string.Empty, ErrorType.None);

    public static ResultError Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static ResultError NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static ResultError Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static ResultError Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static ResultError Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static ResultError BusinessRule(string code, string description) => new(code, description, ErrorType.BusinessRule);

    public static ResultError PreconditionFailed(string code, string description) =>
        new(code, description, ErrorType.PreconditionFailed);

    public static ResultError PreconditionFailed(string code, string description, string etag) =>
        new(code, description, ErrorType.PreconditionFailed, RetryAfter: null) with { ETag = etag };

    public static ResultError PreconditionRequired(string code, string description) =>
        new(code, description, ErrorType.PreconditionRequired);

    public string? ETag { get; init; }

    public static ResultError ServiceUnavailable(string code, string description, TimeSpan? retryAfter = null) =>
        new(code, description, ErrorType.ServiceUnavailable, RetryAfter: retryAfter);

    public static ResultError RateLimited(string code, string description, TimeSpan retryAfter)
    {
        if (retryAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter), "Retry delay must be positive.");
        }

        return new ResultError(code, description, ErrorType.RateLimited, RetryAfter: retryAfter);
    }

    public static ResultError Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("VALIDATION.FAILED", "One or more validation errors occurred.", ErrorType.Validation, errors);
}
