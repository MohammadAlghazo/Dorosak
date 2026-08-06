namespace Dorosak.Application.Common.Exceptions;

public sealed class ForbiddenAccessException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
