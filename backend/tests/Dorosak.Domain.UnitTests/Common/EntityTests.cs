using Dorosak.Domain.Common;

namespace Dorosak.Domain.UnitTests.Common;

public sealed class EntityTests
{
    [Fact]
    public void DomainEvents_PreserveOrderAndCanBeCleared()
    {
        var entity = new TestEntity();
        var first = new TestDomainEvent(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch);
        var second = new TestDomainEvent(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch.AddSeconds(1));

        entity.AddEvent(first);
        entity.AddEvent(second);

        Assert.Equal([first, second], entity.DomainEvents);

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void RaiseDomainEvent_RejectsNull()
    {
        var entity = new TestEntity();

        Assert.Throws<ArgumentNullException>(() => entity.AddEvent(null!));
    }

    private sealed class TestEntity() : Entity<Guid>(Guid.CreateVersion7())
    {
        public void AddEvent(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    private sealed record TestDomainEvent(Guid Id, DateTimeOffset OccurredAt) : IDomainEvent;
}
