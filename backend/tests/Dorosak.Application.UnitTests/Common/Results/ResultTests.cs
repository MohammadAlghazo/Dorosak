using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.UnitTests.Common.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_ContainsValueAndNoFailure()
    {
        Result<string> result = Result.Success("value");

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
        Assert.Equal(ResultError.None, result.Failure);
    }

    [Fact]
    public void Failure_ContainsErrorAndRejectsValueAccess()
    {
        ResultError error = ResultError.NotFound("COURSE.NOT_FOUND", "The course was not found.");
        Result<string> result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Failure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_RejectsNoError()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure<string>(ResultError.None));
    }

    [Fact]
    public void RateLimited_RequiresPositiveRetryDelay()
    {
        ResultError error = ResultError.RateLimited(
            "RATE_LIMIT.EXCEEDED",
            "Try again later.",
            TimeSpan.FromSeconds(30));

        Assert.Equal(ErrorType.RateLimited, error.Type);
        Assert.Equal(TimeSpan.FromSeconds(30), error.RetryAfter);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResultError.RateLimited("RATE_LIMIT.EXCEEDED", "Try again later.", TimeSpan.Zero));
    }
}
