using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;
using Dorosak.Application.Features.Authoring;

namespace Dorosak.Application.Features.PublishingCoordinator;

public sealed record RequestPublicationCommand(Guid UserId, Guid CourseId)
    : ITransactionalCommand<PublicationStatusResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record WithdrawPublicationCommand(Guid UserId, Guid CourseId)
    : ITransactionalCommand<PublicationStatusResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record GetPublicationStatusQuery(Guid UserId, Guid CourseId)
    : IQuery<PublicationStatusResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.View;
}

public sealed record GetPublicationReviewsQuery(int Limit, string? Cursor)
    : IQuery<PagedResponse<PublicationReviewResponse>>;

public sealed record ReviewPublicationCommand(
    Guid ReviewerUserId,
    Guid ReviewId,
    string Decision,
    string? Reason) : ITransactionalCommand<PublicationReviewResponse>;

public sealed record PublicationStatusResponse(
    Guid CourseId,
    string CourseStatus,
    Guid? ReviewId,
    string? ReviewStatus,
    string? ReviewerReason,
    long DraftVersion);

public sealed record PublicationReviewResponse(
    Guid Id,
    Guid CourseId,
    Guid DraftId,
    long DraftVersion,
    Guid RequestedByUserId,
    string Status,
    string? Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? UpdatedAt);
