using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Profiles.TeacherApplications;

public sealed record SubmitTeacherApplicationCommand(
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    string Motivation) : ITransactionalCommand<TeacherApplicationResponse>;

public sealed record GetTeacherApplicationQuery(Guid UserId) : IQuery<TeacherApplicationResponse>;

public sealed record WithdrawTeacherApplicationCommand(Guid UserId) : ITransactionalCommand<TeacherApplicationResponse>;

public sealed record GetTeacherApplicationsQuery(int Limit, string? Cursor) : IQuery<PagedResponse<TeacherApplicationResponse>>;

public sealed record ReviewTeacherApplicationCommand(
    Guid ReviewerUserId,
    Guid ApplicationId,
    string Decision,
    string? Reason) : ITransactionalCommand<TeacherApplicationResponse>;

internal sealed class TeacherApplicationCommandHandler(ITeacherApplicationService service)
    : IRequestHandler<SubmitTeacherApplicationCommand, Result<TeacherApplicationResponse>>,
      IRequestHandler<WithdrawTeacherApplicationCommand, Result<TeacherApplicationResponse>>,
      IRequestHandler<ReviewTeacherApplicationCommand, Result<TeacherApplicationResponse>>
{
    public Task<Result<TeacherApplicationResponse>> Handle(SubmitTeacherApplicationCommand request, CancellationToken cancellationToken)
        => service.SubmitTeacherApplicationAsync(request, cancellationToken);

    public Task<Result<TeacherApplicationResponse>> Handle(WithdrawTeacherApplicationCommand request, CancellationToken cancellationToken)
        => service.WithdrawTeacherApplicationAsync(request, cancellationToken);

    public Task<Result<TeacherApplicationResponse>> Handle(ReviewTeacherApplicationCommand request, CancellationToken cancellationToken)
        => service.ReviewTeacherApplicationAsync(request, cancellationToken);
}

internal sealed class TeacherApplicationQueryHandler(ITeacherApplicationService service)
    : IRequestHandler<GetTeacherApplicationQuery, Result<TeacherApplicationResponse>>,
      IRequestHandler<GetTeacherApplicationsQuery, Result<PagedResponse<TeacherApplicationResponse>>>
{
    public Task<Result<TeacherApplicationResponse>> Handle(GetTeacherApplicationQuery request, CancellationToken cancellationToken)
        => service.GetTeacherApplicationAsync(request, cancellationToken);

    public Task<Result<PagedResponse<TeacherApplicationResponse>>> Handle(GetTeacherApplicationsQuery request, CancellationToken cancellationToken)
        => service.GetTeacherApplicationsAsync(request, cancellationToken);
}
