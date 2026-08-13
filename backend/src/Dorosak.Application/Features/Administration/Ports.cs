using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Administration;

public interface IAdministrationService
{
    Task<Result<AdminCmsResponse>> GetAdminCmsAsync(CancellationToken cancellationToken);

    Task<Result<PublicCmsPageResponse>> GetPublicCmsPageAsync(
        GetPublicCmsPageQuery request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PublicCmsFaqResponse>>> GetPublicFaqsAsync(
        GetPublicFaqsQuery request,
        CancellationToken cancellationToken);

    Task<Result<PortfolioSettingsResponse>> GetAdminSettingsAsync(CancellationToken cancellationToken);

    Task<Result<PublicPortfolioSettingsResponse>> GetPublicSettingsAsync(
        GetPublicSettingsQuery request,
        CancellationToken cancellationToken);

    Task<Result<CmsPageResponse>> UpsertCmsPageDraftAsync(
        UpsertCmsPageDraftCommand request,
        CancellationToken cancellationToken);

    Task<Result<CmsPageResponse>> PublishCmsPageAsync(
        PublishCmsPageCommand request,
        CancellationToken cancellationToken);

    Task<Result<CmsFaqResponse>> UpsertCmsFaqDraftAsync(
        UpsertCmsFaqDraftCommand request,
        CancellationToken cancellationToken);

    Task<Result<CmsFaqResponse>> PublishCmsFaqAsync(
        PublishCmsFaqCommand request,
        CancellationToken cancellationToken);

    Task<Result<PortfolioSettingsResponse>> UpdatePortfolioSettingsAsync(
        UpdatePortfolioSettingsCommand request,
        CancellationToken cancellationToken);

    Task<Result<AuditLogPageResponse>> GetAuditLogsAsync(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken);
}
