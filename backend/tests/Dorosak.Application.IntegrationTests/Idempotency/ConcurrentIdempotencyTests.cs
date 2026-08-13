using Dorosak.Application.Common.Behaviors;
using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Persistence;
using Dorosak.Application.Common.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Idempotency;

[Collection(InfrastructureTestGroup.Name)]
public sealed class ConcurrentIdempotencyTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task ConcurrentRequests_ExecuteHandlerOnceAndReplayResult()
    {
        string key = Guid.CreateVersion7().ToString("N");
        var request = new TestRequest(key, "course-1");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerExecutions = 0;

        async Task<Result<TestResponse>> ExecuteAsync()
        {
            await start.Task;
            return await ExecuteInScopeAsync(request, async cancellationToken =>
            {
                Interlocked.Increment(ref handlerExecutions);
                await Task.Delay(250, cancellationToken);
                return Result.Success(new TestResponse("enrollment-1"));
            });
        }

        Task<Result<TestResponse>> first = ExecuteAsync();
        Task<Result<TestResponse>> second = ExecuteAsync();
        start.SetResult();

        Result<TestResponse>[] results = await Task.WhenAll(first, second);

        Assert.Equal(1, handlerExecutions);
        Assert.All(results, result => Assert.Equal("enrollment-1", result.Value.Id));
    }

    [Fact]
    public async Task ReusedKeyWithChangedRequest_ReturnsConflict()
    {
        string key = Guid.CreateVersion7().ToString("N");
        await ExecuteInScopeAsync(
            new TestRequest(key, "course-1"),
            _ => Task.FromResult(Result.Success(new TestResponse("enrollment-1"))));

        RequestConflictException exception = await Assert.ThrowsAsync<RequestConflictException>(() =>
            ExecuteInScopeAsync(
                new TestRequest(key, "course-2"),
                _ => Task.FromResult(Result.Success(new TestResponse("enrollment-2")))));

        Assert.Equal("IDEMPOTENCY.KEY_REUSED", exception.Code);
    }

    private async Task<Result<TestResponse>> ExecuteInScopeAsync(
        TestRequest request,
        Func<CancellationToken, Task<Result<TestResponse>>> handler)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IIdempotencyStore store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var transactionBehavior = new TransactionBehavior<TestRequest, Result<TestResponse>>(unitOfWork);
        var idempotencyBehavior = new IdempotencyBehavior<TestRequest, Result<TestResponse>>(store);

        return await transactionBehavior.Handle(
            request,
            () => idempotencyBehavior.Handle(request, () => handler(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
    }

    private sealed record TestRequest(string IdempotencyKey, string CourseId) : IIdempotentRequest, ITransactionalRequest
    {
        public string IdempotencyOperation => "enrollment.create.v1";

        public string IdempotencyScope => "user-1";

        public object IdempotencyPayload => new { CourseId };

        public int ResponseSchemaVersion => 1;

        public TimeSpan Retention => TimeSpan.FromHours(1);
    }

    private sealed record TestResponse(string Id);
}

