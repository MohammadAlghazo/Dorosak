using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Credentials;

public interface ICredentialsService
{
    Task<Result<IReadOnlyList<CertificateResponse>>> GetMyCertificatesAsync(
        GetMyCertificatesQuery request,
        CancellationToken cancellationToken);

    Task<Result<CertificateResponse>> GetMyCertificateAsync(
        GetMyCertificateQuery request,
        CancellationToken cancellationToken);

    Task<Result<PublicCertificateResponse>> VerifyCertificateAsync(
        VerifyCertificateQuery request,
        CancellationToken cancellationToken);

    Task<Result<CertificateResponse>> IssueCertificateFromCompletionAsync(
        IssueCertificateFromCompletionCommand request,
        CancellationToken cancellationToken);

    Task<Result<CertificateResponse>> RevokeCertificateAsync(
        RevokeCertificateCommand request,
        CancellationToken cancellationToken);
}

public interface ICertificateIssuanceDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
