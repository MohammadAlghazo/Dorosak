using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Communications;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommunicationsController(ISender sender) : ControllerBase
{
    [HttpGet("conversations")]
    [PermissionPolicy(Permissions.ConversationReadOwn)]
    [ProducesResponseType<ApiResponse<ConversationPageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ConversationPageResponse> result = await sender.Send(
            new GetConversationsQuery(userId, limit, cursor),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("conversations")]
    [PermissionPolicy(Permissions.MessageSendAsSelf)]
    [ProducesResponseType<ApiResponse<ConversationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> CreateConversation(
        CreateConversationRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingIdempotencyKey<ConversationResponse>();
        }

        Result<ConversationResponse> result = await sender.Send(
            new CreateConversationCommand(userId, request.ParticipantUserIds, request.CourseId, key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [PermissionPolicy(Permissions.ConversationReadOwn)]
    [ProducesResponseType<ApiResponse<MessagePageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] long? afterSequence = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<MessagePageResponse> result = await sender.Send(
            new GetConversationMessagesQuery(userId, conversationId, limit, cursor, afterSequence),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [PermissionPolicy(Permissions.MessageSendAsSelf)]
    [ProducesResponseType<ApiResponse<MessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> CreateMessage(
        Guid conversationId,
        CreateMessageRequest request,
        [FromHeader(Name = "Idempotency-Key"), StringLength(200, MinimumLength = 1)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        if (!TryGetIdempotencyKey(idempotencyKey, out string key))
        {
            return MissingIdempotencyKey<MessageResponse>();
        }

        Result<MessageResponse> result = await sender.Send(
            new CreateMessageCommand(
                userId,
                conversationId,
                request.ClientMessageId,
                request.Body,
                key),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("conversations/{conversationId:guid}/participants/me")]
    [PermissionPolicy(Permissions.ConversationReadOwn)]
    [ProducesResponseType<ApiResponse<ConversationOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LeaveConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized();
        Result<ConversationOperationResponse> result = await sender.Send(
            new LeaveConversationCommand(userId, conversationId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private static bool TryGetIdempotencyKey(string? candidate, out string value)
    {
        value = candidate?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }

    private IActionResult MissingIdempotencyKey<T>() => this.ToActionResult(
        Result.Failure<T>(ResultError.PreconditionRequired(
            "IDEMPOTENCY.KEY_REQUIRED",
            "Idempotency-Key is required.")));
}

public sealed record CreateConversationRequest
{
    [Required]
    public required IReadOnlyList<Guid> ParticipantUserIds { get; init; }

    [Required]
    public required Guid CourseId { get; init; }
}

public sealed record CreateMessageRequest(Guid ClientMessageId, string Body);
