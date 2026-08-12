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

public sealed record GetDemoSubscriptionQuery(Guid UserId) : IQuery<DemoSubscriptionStateResponse>;

public sealed record ActivateDemoSubscriptionCommand(
    Guid UserId,
    string IdempotencyKey) : IIdempotentCommand<DemoSubscriptionResponse>
{
    public string IdempotencyOperation => "commerce.demo-subscription-activate.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { PlanCode = "portfolio-demo" };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record CancelDemoSubscriptionCommand(
    Guid UserId,
    Guid SubscriptionId,
    string IdempotencyKey) : IIdempotentCommand<DemoSubscriptionResponse>
{
    public string IdempotencyOperation => "commerce.demo-subscription-cancel.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { SubscriptionId };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}
