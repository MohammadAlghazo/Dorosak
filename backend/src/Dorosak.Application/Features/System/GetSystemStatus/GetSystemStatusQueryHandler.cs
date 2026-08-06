using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.System.GetSystemStatus;

internal sealed class GetSystemStatusQueryHandler(TimeProvider timeProvider)
    : IRequestHandler<GetSystemStatusQuery, Result<SystemStatusResponse>>
{
    public Task<Result<SystemStatusResponse>> Handle(
        GetSystemStatusQuery request,
        CancellationToken cancellationToken)
    {
        string version = typeof(GetSystemStatusQueryHandler).Assembly.GetName().Version?.ToString() ?? "unknown";
        var response = new SystemStatusResponse("Dorosak.Api", version, timeProvider.GetUtcNow());
        return Task.FromResult(Result.Success(response));
    }
}
