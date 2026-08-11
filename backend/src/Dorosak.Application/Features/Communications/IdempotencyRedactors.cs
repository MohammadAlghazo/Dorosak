using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Communications;

internal sealed class CreateConversationResponseRedactor
    : IIdempotencyResponseRedactor<CreateConversationCommand, Result<ConversationResponse>>
{
    public Result<ConversationResponse> Redact(
        CreateConversationCommand request,
        Result<ConversationResponse> response) =>
        response.IsSuccess
            ? Result.Success(response.Value with
            {
                Participants = response.Value.Participants
                    .Select(participant => participant with { DisplayName = string.Empty })
                    .ToArray(),
            })
            : response;
}

internal sealed class CreateMessageResponseRedactor
    : IIdempotencyResponseRedactor<CreateMessageCommand, Result<MessageResponse>>
{
    public Result<MessageResponse> Redact(
        CreateMessageCommand request,
        Result<MessageResponse> response) =>
        response.IsSuccess
            ? Result.Success(response.Value with { SenderName = string.Empty, Body = string.Empty })
            : response;
}

internal sealed class CreateAnnouncementResponseRedactor
    : IIdempotencyResponseRedactor<CreateAnnouncementCommand, Result<AnnouncementResponse>>
{
    public Result<AnnouncementResponse> Redact(
        CreateAnnouncementCommand request,
        Result<AnnouncementResponse> response) => Redact(response);

    private static Result<AnnouncementResponse> Redact(Result<AnnouncementResponse> response) =>
        response.IsSuccess
            ? Result.Success(response.Value with { Title = string.Empty, Body = string.Empty, TargetCount = 0 })
            : response;
}

internal sealed class UpdateAnnouncementResponseRedactor
    : IIdempotencyResponseRedactor<UpdateAnnouncementCommand, Result<AnnouncementResponse>>
{
    public Result<AnnouncementResponse> Redact(
        UpdateAnnouncementCommand request,
        Result<AnnouncementResponse> response) =>
        response.IsSuccess
            ? Result.Success(response.Value with { Title = string.Empty, Body = string.Empty, TargetCount = 0 })
            : response;
}
