namespace Dorosak.Application.Common.Authorization;

public readonly record struct AuthorizationDecision(bool IsAllowed, string Code, string Description)
{
    public static AuthorizationDecision Allow() => new(true, string.Empty, string.Empty);

    public static AuthorizationDecision Deny(string code, string description) => new(false, code, description);
}
