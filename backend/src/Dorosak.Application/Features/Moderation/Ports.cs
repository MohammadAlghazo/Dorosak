using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Moderation;

public interface IModerationService
{
    Task<Result<ContentReportResponse>> CreateContentReportAsync(
        CreateContentReportCommand request,
        CancellationToken cancellationToken);

    Task<Result<ContentReportResponse>> GetMyContentReportAsync(
        GetMyContentReportQuery request,
        CancellationToken cancellationToken);

    Task<Result<ContentReportPageResponse>> GetAdminContentReportsAsync(
        GetAdminContentReportsQuery request,
        CancellationToken cancellationToken);

    Task<Result<ModerationCasePageResponse>> GetModerationCasesAsync(
        GetModerationCasesQuery request,
        CancellationToken cancellationToken);

    Task<Result<ModerationCaseResponse>> GetModerationCaseAsync(
        GetModerationCaseQuery request,
        CancellationToken cancellationToken);

    Task<Result<ModerationCaseResponse>> ApplyModerationActionAsync(
        ApplyModerationActionCommand request,
        CancellationToken cancellationToken);
}
