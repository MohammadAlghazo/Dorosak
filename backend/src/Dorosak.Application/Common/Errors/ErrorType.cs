namespace Dorosak.Application.Common.Errors;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    RateLimited = 6,
    Failure = 7,
    BusinessRule = 8,
    PreconditionFailed = 9,
    ServiceUnavailable = 10,
}
