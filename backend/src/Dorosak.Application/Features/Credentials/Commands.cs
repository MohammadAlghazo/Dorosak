using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Credentials;

public sealed record GetMyCertificatesQuery(Guid UserId) : IQuery<IReadOnlyList<CertificateResponse>>;

public sealed record GetMyCertificateQuery(Guid UserId, Guid CertificateId) : IQuery<CertificateResponse>;

public sealed record VerifyCertificateQuery(string VerificationCode) : IQuery<PublicCertificateResponse>;

public sealed record IssueCertificateFromCompletionCommand(Guid CompletionEnrollmentId)
    : ITransactionalCommand<CertificateResponse>;

public sealed record RevokeCertificateCommand(
    Guid ActorUserId,
    Guid CertificateId,
    string Reason) : ITransactionalCommand<CertificateResponse>;
