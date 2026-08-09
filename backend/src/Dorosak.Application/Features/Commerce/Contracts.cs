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
