using Dorosak.Application.Common.Behaviors;
using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.UnitTests.Common.Behaviors;

public sealed class IdempotencyBehaviorTests
{
    [Fact]
    public async Task Handle_ReplaysCompletedResponse()
    {
        Result<string> completed = Result.Success("completed");
        var store = new StubIdempotencyStore
        {
            LookupStatus = IdempotencyLookupStatus.Completed,
            LookupResponse = completed,
        };
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(store);
        int handlerCalls = 0;

        Result<string> result = await behavior.Handle(
            new IdempotentRequest("key", "payload"),
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(Result.Success("new"));
            },
            TestContext.Current.CancellationToken);

        Assert.Same(completed, result);
        Assert.Equal(0, handlerCalls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task Handle_RejectsReusedKeyForDifferentRequest()
    {
        var store = new StubIdempotencyStore { LookupStatus = IdempotencyLookupStatus.Conflict };
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(store);

        RequestConflictException exception = await Assert.ThrowsAsync<RequestConflictException>(() => behavior.Handle(
            new IdempotentRequest("key", "changed"),
            _ => Task.FromResult(Result.Success("new")),
            TestContext.Current.CancellationToken));

        Assert.Equal("IDEMPOTENCY.KEY_REUSED", exception.Code);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task Handle_RejectsIncompatibleStoredResponse()
    {
        var store = new StubIdempotencyStore
        {
            LookupStatus = IdempotencyLookupStatus.ResponseSchemaMismatch,
        };
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(store);

        RequestConflictException exception = await Assert.ThrowsAsync<RequestConflictException>(() => behavior.Handle(
            new IdempotentRequest("key", "payload"),
            _ => Task.FromResult(Result.Success("new")),
            TestContext.Current.CancellationToken));

        Assert.Equal("IDEMPOTENCY.RESPONSE_SCHEMA_MISMATCH", exception.Code);
    }

    [Fact]
    public async Task Handle_StoresNewResponse()
    {
        var store = new StubIdempotencyStore { LookupStatus = IdempotencyLookupStatus.NotFound };
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(store);

        Result<string> result = await behavior.Handle(
            new IdempotentRequest("key", "payload"),
            _ => Task.FromResult(Result.Success("new")),
            TestContext.Current.CancellationToken);

        Assert.Equal("new", result.Value);
        Assert.Equal(1, store.StoreCalls);
    }

    [Fact]
    public async Task Handle_StoresRedactedResponseAndReturnsOriginalResponse()
    {
        var store = new StubIdempotencyStore { LookupStatus = IdempotencyLookupStatus.NotFound };
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(
            store,
            responseRedactors: [new StubResponseRedactor()]);

        Result<string> result = await behavior.Handle(
            new IdempotentRequest("key", "payload"),
            _ => Task.FromResult(Result.Success("private")),
            TestContext.Current.CancellationToken);

        Assert.Equal("private", result.Value);
        Assert.Equal("redacted", Assert.IsType<Result<string>>(store.StoredResponse).Value);
    }

    [Fact]
    public async Task Handle_RefreshesCompletedResponseWhenReplayHandlerIsRegistered()
    {
        Result<string> completed = Result.Success("stored");
        var store = new StubIdempotencyStore
        {
            LookupStatus = IdempotencyLookupStatus.Completed,
            LookupResponse = completed,
        };
        var replayHandler = new StubReplayHandler();
        var behavior = new IdempotencyBehavior<IdempotentRequest, Result<string>>(
            store,
            [replayHandler]);

        Result<string> result = await behavior.Handle(
            new IdempotentRequest("key", "payload"),
            _ => Task.FromResult(Result.Success("new")),
            TestContext.Current.CancellationToken);

        Assert.Equal("current", result.Value);
        Assert.Equal(1, replayHandler.Calls);
        Assert.Equal(0, store.StoreCalls);
    }

    private sealed record IdempotentRequest(string IdempotencyKey, string Payload) : IIdempotentRequest
    {
        public string IdempotencyOperation => "test.operation.v1";

        public string IdempotencyScope => "user-1";

        public object IdempotencyPayload => new { Payload };

        public int ResponseSchemaVersion => 1;

        public TimeSpan Retention => TimeSpan.FromHours(24);
    }

    private sealed class StubIdempotencyStore : IIdempotencyStore
    {
        public IdempotencyLookupStatus LookupStatus { get; init; }

        public object? LookupResponse { get; init; }

        public int StoreCalls { get; private set; }

        public object? StoredResponse { get; private set; }

        public Task<IdempotencyLookup<TResponse>> FindAsync<TResponse>(
            string scope,
            string operation,
            string key,
            object requestPayload,
            int responseSchemaVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IdempotencyLookup<TResponse>(LookupStatus, (TResponse?)LookupResponse));

        public Task StoreAsync<TResponse>(
            string scope,
            string operation,
            string key,
            object requestPayload,
            TResponse response,
            int responseSchemaVersion,
            TimeSpan retention,
            CancellationToken cancellationToken)
        {
            StoreCalls++;
            StoredResponse = response;
            return Task.CompletedTask;
        }
    }

    private sealed class StubReplayHandler
        : IIdempotencyReplayHandler<IdempotentRequest, Result<string>>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> ResolveAsync(
            IdempotentRequest request,
            Result<string> storedResponse,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result.Success("current"));
        }
    }

    private sealed class StubResponseRedactor
        : IIdempotencyResponseRedactor<IdempotentRequest, Result<string>>
    {
        public Result<string> Redact(IdempotentRequest request, Result<string> response) =>
            Result.Success("redacted");
    }
}
