using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Communications;

internal sealed class CreateConversationReplayHandler(ICommunicationsService service)
    : IIdempotencyReplayHandler<CreateConversationCommand, Result<ConversationResponse>>
{
    public Task<Result<ConversationResponse>> ResolveAsync(
        CreateConversationCommand request,
        Result<ConversationResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetConversationForReplayAsync(
                request.UserId,
                storedResponse.Value.Id,
                cancellationToken)
            : Task.FromResult(storedResponse);
}

internal sealed class CreateAnnouncementReplayHandler(ICommunicationsService service)
    : IIdempotencyReplayHandler<CreateAnnouncementCommand, Result<AnnouncementResponse>>
{
    public Task<Result<AnnouncementResponse>> ResolveAsync(
        CreateAnnouncementCommand request,
        Result<AnnouncementResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetAnnouncementForReplayAsync(
                request.UserId,
                request.CourseId,
                storedResponse.Value.Id,
                cancellationToken)
            : Task.FromResult(storedResponse);
}

internal sealed class UpdateAnnouncementReplayHandler(ICommunicationsService service)
    : IIdempotencyReplayHandler<UpdateAnnouncementCommand, Result<AnnouncementResponse>>
{
    public Task<Result<AnnouncementResponse>> ResolveAsync(
        UpdateAnnouncementCommand request,
        Result<AnnouncementResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetAnnouncementForReplayAsync(
                request.UserId,
                request.CourseId,
                storedResponse.Value.Id,
                cancellationToken)
            : Task.FromResult(storedResponse);
}

internal sealed class CreateMessageReplayHandler(ICommunicationsService service)
    : IIdempotencyReplayHandler<CreateMessageCommand, Result<MessageResponse>>
{
    public Task<Result<MessageResponse>> ResolveAsync(
        CreateMessageCommand request,
        Result<MessageResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetMessageForReplayAsync(
                request.UserId,
                request.ConversationId,
                storedResponse.Value.Id,
                cancellationToken)
            : Task.FromResult(storedResponse);
}
