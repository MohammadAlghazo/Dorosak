using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Commerce;

public sealed record CreateDemoCheckoutCommand(
    Guid UserId,
    Guid CourseId,
    string Outcome,
    string Locale,
    string IdempotencyKey) : IIdempotentCommand<DemoCheckoutResponse>
{
    public string IdempotencyOperation => "commerce.demo-checkout.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { CourseId, Outcome, Locale };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(1);
}
