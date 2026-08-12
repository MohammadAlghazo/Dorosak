namespace Dorosak.Application.Features.Commerce;

public sealed record DemoCheckoutResponse(
    Guid OrderId,
    Guid PaymentId,
    Guid CourseId,
    Guid? EnrollmentId,
    string OrderStatus,
    string PaymentStatus,
    decimal AmountCredits,
    string Currency);

public sealed record DemoSubscriptionResponse(
    Guid Id,
    string PlanCode,
    string Status,
    DateTimeOffset ActivatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt);

public sealed record DemoSubscriptionStateResponse(DemoSubscriptionResponse? Subscription);
