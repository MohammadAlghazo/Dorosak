namespace Dorosak.Application.Features.Moderation;

public sealed record ContentReportResponse(
    Guid Id,
    string TargetKind,
    Guid TargetId,
    string Reason,
    string Details,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public sealed record AdminContentReportResponse(
    ContentReportResponse Report,
    Guid ReporterUserId,
    string ReporterName,
    Guid CaseId,
    string CaseStatus,
    MessageReportSnapshotResponse? MessageSnapshot = null);

public sealed record MessageReportSnapshotResponse(
    Guid SenderUserId,
    string SenderName,
    Guid CourseId,
    string CourseTitle,
    Guid ConversationId,
    long Sequence,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record ContentReportPageResponse(
    IReadOnlyList<AdminContentReportResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record ModerationCaseSummaryResponse(
    Guid Id,
    Guid ReportId,
    string Status,
    Guid? AssignedToUserId,
    string? AssignedToName,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    AdminContentReportResponse Report);

public sealed record ModerationCasePageResponse(
    IReadOnlyList<ModerationCaseSummaryResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record ModerationActionResponse(
    Guid Id,
    Guid CaseId,
    Guid ActorUserId,
    string ActorName,
    string ActionType,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record ModerationTargetPreviewResponse(
    string Status,
    string Title,
    string Body,
    string AuthorName);

public sealed record ModerationCaseResponse(
    ModerationCaseSummaryResponse Case,
    IReadOnlyList<ModerationActionResponse> Actions,
    ModerationTargetPreviewResponse TargetPreview);
