using Dorosak.Application.Common.Errors;

namespace Dorosak.Application.Common.Results;

public interface IResult
{
    bool IsSuccess { get; }

    ResultError Failure { get; }
}
