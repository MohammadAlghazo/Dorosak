using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Administration;

internal sealed class AdministrationHandler<TRequest, TResponse>(IAdministrationService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        GetAdminCmsQuery => Cast(service.GetAdminCmsAsync(cancellationToken)),
        GetPublicCmsPageQuery query => Cast(service.GetPublicCmsPageAsync(query, cancellationToken)),
        GetPublicFaqsQuery query => Cast(service.GetPublicFaqsAsync(query, cancellationToken)),
        GetAdminSettingsQuery => Cast(service.GetAdminSettingsAsync(cancellationToken)),
        GetPublicSettingsQuery query => Cast(service.GetPublicSettingsAsync(query, cancellationToken)),
        UpsertCmsPageDraftCommand command => Cast(service.UpsertCmsPageDraftAsync(command, cancellationToken)),
        PublishCmsPageCommand command => Cast(service.PublishCmsPageAsync(command, cancellationToken)),
        UpsertCmsFaqDraftCommand command => Cast(service.UpsertCmsFaqDraftAsync(command, cancellationToken)),
        PublishCmsFaqCommand command => Cast(service.PublishCmsFaqAsync(command, cancellationToken)),
        UpdatePortfolioSettingsCommand command => Cast(service.UpdatePortfolioSettingsAsync(command, cancellationToken)),
        GetAuditLogsQuery query => Cast(service.GetAuditLogsAsync(query, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported administration request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
