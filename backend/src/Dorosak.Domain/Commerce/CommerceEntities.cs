using Dorosak.Domain.Common;

namespace Dorosak.Domain.Commerce;

public enum DemoOrderStatus
{
    Pending,
    Completed,
    Failed,
}

public enum DemoPaymentStatus
{
    Succeeded,
    Failed,
}

public enum DemoSubscriptionStatus
{
    Active,
    Cancelled,
}

public sealed class DemoOrder
{
    private DemoOrder()
    {
    }

    private DemoOrder(
        Guid id,
        Guid userId,
        Guid courseId,
        decimal totalCredits,
        DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        CourseId = courseId;
        Currency = "DEMO";
        TotalCredits = totalCredits;
        Status = DemoOrderStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CourseId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal TotalCredits { get; private set; }

    public DemoOrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static DemoOrder Create(
        Guid userId,
        Guid courseId,
        decimal totalCredits,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty || courseId == Guid.Empty)
        {
            throw new ArgumentException("Demo order identifiers are required.");
        }
        if (totalCredits <= 0)
        {
            throw new DomainRuleException("COMMERCE.DEMO_AMOUNT_INVALID", "The demo amount must be positive.");
        }

        return new DemoOrder(Guid.CreateVersion7(), userId, courseId, totalCredits, now);
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == DemoOrderStatus.Completed)
        {
            return;
        }
        if (Status == DemoOrderStatus.Failed)
        {
            throw new DomainRuleException("COMMERCE.ORDER_TERMINAL", "A failed demo order cannot be completed.");
        }

        Status = DemoOrderStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Fail(DateTimeOffset now)
    {
        if (Status == DemoOrderStatus.Completed)
        {
            throw new DomainRuleException("COMMERCE.ORDER_TERMINAL", "A completed demo order cannot fail.");
        }
        if (Status == DemoOrderStatus.Failed)
        {
            return;
        }

        Status = DemoOrderStatus.Failed;
        UpdatedAt = now;
    }
}

public sealed class DemoPayment
{
    private DemoPayment()
    {
    }

    private DemoPayment(
        Guid id,
        Guid orderId,
        decimal amountCredits,
        DemoPaymentStatus status,
        DateTimeOffset now)
    {
        Id = id;
        OrderId = orderId;
        Provider = "DemoProvider";
        ProviderReference = $"demo_{Guid.CreateVersion7():N}";
        AmountCredits = amountCredits;
        Currency = "DEMO";
        Status = status;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderReference { get; private set; } = string.Empty;

    public decimal AmountCredits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DemoPaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DemoPayment Create(
        Guid orderId,
        decimal amountCredits,
        DemoPaymentStatus status,
        DateTimeOffset now)
    {
        if (orderId == Guid.Empty || amountCredits <= 0)
        {
            throw new ArgumentException("Demo payment values are invalid.");
        }

        return new DemoPayment(Guid.CreateVersion7(), orderId, amountCredits, status, now);
    }
}

public sealed class DemoSubscription
{
    public const string DemoPlanCode = "portfolio-demo";

    private DemoSubscription()
    {
    }

    private DemoSubscription(Guid id, Guid userId, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        PlanCode = DemoPlanCode;
        Status = DemoSubscriptionStatus.Active;
        ActivatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public DemoSubscriptionStatus Status { get; private set; }

    public DateTimeOffset ActivatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public static DemoSubscription Create(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A demo subscription user is required.", nameof(userId));
        }

        return new DemoSubscription(Guid.CreateVersion7(), userId, now);
    }

    public void Activate(DateTimeOffset now)
    {
        Status = DemoSubscriptionStatus.Active;
        ActivatedAt = now;
        CancelledAt = null;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == DemoSubscriptionStatus.Cancelled)
        {
            return;
        }

        Status = DemoSubscriptionStatus.Cancelled;
        CancelledAt = now;
        UpdatedAt = now;
    }
}
