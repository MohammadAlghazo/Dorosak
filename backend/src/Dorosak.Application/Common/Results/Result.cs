using Dorosak.Application.Common.Errors;

namespace Dorosak.Application.Common.Results;

public sealed class Result<T> : IResult
{
    private readonly T? _value;

    internal Result(bool isSuccess, T? value, ResultError failure)
    {
        if (isSuccess == (failure != ResultError.None))
        {
            throw new ArgumentException("Success and failure state are inconsistent.", nameof(failure));
        }

        IsSuccess = isSuccess;
        Failure = failure;
        _value = value;
    }

    public bool IsSuccess { get; }

    public ResultError Failure { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failure result does not contain a value.");

}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(true, value, ResultError.None);

    public static Result<T> Failure<T>(ResultError error) => new(false, default, error);
}
