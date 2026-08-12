using Dorosak.Domain.Commerce;

namespace Dorosak.Domain.UnitTests.Commerce;

public sealed class DemoCommerceTests
{
    [Fact]
    public void SuccessfulOrderCanBeCompletedOnlyOnce()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DemoOrder order = DemoOrder.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 100, now);

        order.Complete(now.AddMinutes(1));
        order.Complete(now.AddMinutes(2));

        Assert.Equal(DemoOrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void FailedOrderCannotBeCompleted()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DemoOrder order = DemoOrder.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 100, now);

        order.Fail(now.AddMinutes(1));

        Assert.Throws<Domain.Common.DomainRuleException>(() => order.Complete(now.AddMinutes(2)));
    }

    [Fact]
    public void DemoSubscriptionCanBeCancelledAndReactivated()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DemoSubscription subscription = DemoSubscription.Create(Guid.CreateVersion7(), now);

        subscription.Cancel(now.AddMinutes(1));
        subscription.Cancel(now.AddMinutes(2));
        Assert.Equal(DemoSubscriptionStatus.Cancelled, subscription.Status);
        Assert.NotNull(subscription.CancelledAt);

        subscription.Activate(now.AddMinutes(3));
        Assert.Equal(DemoSubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.CancelledAt);
    }
}
