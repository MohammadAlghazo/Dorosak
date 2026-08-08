using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Publishing;

public interface IAuthoringPublishingPort
{
    Task<AuthoringPublicationSnapshot> GetSnapshotAsync(Guid courseId, CancellationToken cancellationToken);
}

public interface IMediaPublishingPort
{
    Task<MediaPublicationSnapshot> CheckReadinessAsync(
        Guid courseId,
        IReadOnlyList<MediaAssetReference> references,
        CancellationToken cancellationToken);
}

public interface IAssessmentPublishingPort
{
    Task<AssessmentPublicationSnapshot> CheckReadinessAsync(
        Guid courseId,
        IReadOnlyList<Guid> quizVersionIds,
        IReadOnlyList<Guid> assignmentVersionIds,
        CancellationToken cancellationToken);
}

public interface IPublishingAuditPort
{
    Task RecordFailedActivationAsync(
        Guid actorUserId,
        Guid courseId,
        PublishingFailure failure,
        CancellationToken cancellationToken);
}

public interface ICatalogActivationPort
{
    Task<Result<CourseReleaseResponse>> ActivateAsync(
        ActivateCourseReleaseCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseReleaseResponse>> UnpublishAsync(
        UnpublishCourseCommand request,
        CancellationToken cancellationToken);
}

public interface IPublicCatalogPort
{
    Task<Result<PublicCourseLookupResponse>> ResolveAsync(
        ResolvePublicCourseQuery request,
        CancellationToken cancellationToken);
}

public interface IPublishingCoordinator
{
    Task<Result<CourseReleaseResponse>> PublishAsync(
        PublishCourseCommand request,
        CancellationToken cancellationToken);
}

public interface ICatalogProjectionGenerationPort
{
    Task<long> AdvanceAsync(CancellationToken cancellationToken);

    Task<long> GetAsync(CancellationToken cancellationToken);
}
