using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Moderation;

internal sealed class CreateContentReportReplayHandler(IModerationService service)
    : IIdempotencyReplayHandler<CreateContentReportCommand, Result<ContentReportResponse>>
{
    public Task<Result<ContentReportResponse>> ResolveAsync(
        CreateContentReportCommand request,
        Result<ContentReportResponse> storedResponse,
        CancellationToken cancellationToken) => storedResponse.IsSuccess
        ? service.GetMyContentReportAsync(
            new GetMyContentReportQuery(request.UserId, storedResponse.Value.Id),
            cancellationToken)
        : Task.FromResult(storedResponse);
}

internal sealed class ApplyModerationActionReplayHandler(IModerationService service)
    : IIdempotencyReplayHandler<ApplyModerationActionCommand, Result<ModerationCaseResponse>>
{
    public Task<Result<ModerationCaseResponse>> ResolveAsync(
        ApplyModerationActionCommand request,
        Result<ModerationCaseResponse> storedResponse,
        CancellationToken cancellationToken) => storedResponse.IsSuccess
        ? service.GetModerationCaseAsync(
            new GetModerationCaseQuery(request.ActorUserId, request.CaseId),
            cancellationToken)
        : Task.FromResult(storedResponse);
}
