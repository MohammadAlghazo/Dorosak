using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Profiles.TeacherApplications;

public sealed record TeacherApplicationResponse(
    Guid Id,
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    string Motivation,
    string Status,
    string? ReviewerReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record TeacherProfileResponse(
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    DateTimeOffset ApprovedAt);

public interface ITeacherApplicationService
{
    Task<Result<TeacherApplicationResponse>> SubmitTeacherApplicationAsync(SubmitTeacherApplicationCommand request, CancellationToken cancellationToken);
    Task<Result<TeacherApplicationResponse>> WithdrawTeacherApplicationAsync(WithdrawTeacherApplicationCommand request, CancellationToken cancellationToken);
    Task<Result<TeacherApplicationResponse>> ReviewTeacherApplicationAsync(ReviewTeacherApplicationCommand request, CancellationToken cancellationToken);
    Task<Result<TeacherApplicationResponse>> GetTeacherApplicationAsync(GetTeacherApplicationQuery request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<TeacherApplicationResponse>>> GetTeacherApplicationsAsync(GetTeacherApplicationsQuery request, CancellationToken cancellationToken);
}


