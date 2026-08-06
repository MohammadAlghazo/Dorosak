using Dorosak.Application.Common.Behaviors;
using FluentValidation;

namespace Dorosak.Application.UnitTests.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_ExecutesScopedValidatorsSequentially()
    {
        var probe = new ConcurrencyProbe();
        IValidator<TestRequest>[] validators = [new TrackingValidator(probe), new TrackingValidator(probe)];
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        string response = await behavior.Handle(
            new TestRequest("value"),
            _ => Task.FromResult("handled"),
            TestContext.Current.CancellationToken);

        Assert.Equal("handled", response);
        Assert.Equal(1, probe.MaximumConcurrency);
    }

    private sealed record TestRequest(string Value);

    private sealed class TrackingValidator : AbstractValidator<TestRequest>
    {
        public TrackingValidator(ConcurrencyProbe probe)
        {
            RuleFor(request => request).CustomAsync(async (_, _, cancellationToken) =>
            {
                probe.Enter();
                try
                {
                    await Task.Delay(25, cancellationToken);
                }
                finally
                {
                    probe.Exit();
                }
            });
        }
    }

    private sealed class ConcurrencyProbe
    {
        private int _active;
        private int _maximumConcurrency;

        public int MaximumConcurrency => _maximumConcurrency;

        public void Enter()
        {
            int active = Interlocked.Increment(ref _active);
            int observedMaximum;
            do
            {
                observedMaximum = _maximumConcurrency;
                if (active <= observedMaximum)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrency, active, observedMaximum) != observedMaximum);
        }

        public void Exit() => Interlocked.Decrement(ref _active);
    }
}
