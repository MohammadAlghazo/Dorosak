using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Moderation;

public sealed record CreateContentReportCommand(
    Guid UserId,
    Guid? CourseId,
    Guid? ReviewId,
    Guid? CommentId,
    Guid? ReportedUserId,
    Guid? ContextCommentId,
    string Reason,
    string? Details,
    string IdempotencyKey) : IIdempotentCommand<ContentReportResponse>
{
    public string IdempotencyOperation => "moderation.report-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new
    {
        CourseId,
        ReviewId,
        CommentId,
        ReportedUserId,
        ContextCommentId,
        Reason,
        Details,
    };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record GetMyContentReportQuery(Guid UserId, Guid ReportId) : IQuery<ContentReportResponse>;

public sealed record GetAdminContentReportsQuery(
    Guid ActorUserId,
    string? Status,
    string? TargetKind,
    int Limit,
    string? Cursor) : IQuery<ContentReportPageResponse>;

public sealed record GetModerationCasesQuery(
    Guid ActorUserId,
    string? Status,
    int Limit,
    string? Cursor) : IQuery<ModerationCasePageResponse>;

public sealed record GetModerationCaseQuery(
    Guid ActorUserId,
    Guid CaseId) : IQuery<ModerationCaseResponse>;

public sealed record ApplyModerationActionCommand(
    Guid ActorUserId,
    Guid CaseId,
    string Action,
    string Reason,
    long ExpectedVersion,
    string AuditReason,
    string IdempotencyKey) : IIdempotentCommand<ModerationCaseResponse>
{
    public string IdempotencyOperation => "moderation.case-action.v1";

    public string IdempotencyScope => $"user:{ActorUserId:D}:case:{CaseId:D}";

    public object IdempotencyPayload => new { CaseId, Action, Reason, ExpectedVersion, AuditReason };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(365);
}
