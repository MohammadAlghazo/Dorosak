using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Publishing;

public sealed record PublishCourseCommand(
    Guid ActorUserId,
    Guid CourseId,
    string IdempotencyKey,
    string AuditReason) : IIdempotentCommand<CourseReleaseResponse>
{
    public string IdempotencyOperation => "catalog.course-publish.v1";

    public string IdempotencyScope => $"admin:{ActorUserId:D}";

    public object IdempotencyPayload => new { CourseId, AuditReason };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(24);
}

public sealed record UnpublishCourseCommand(
    Guid ActorUserId,
    Guid CourseId,
    string IdempotencyKey,
    string AuditReason) : IIdempotentCommand<CourseReleaseResponse>
{
    public string IdempotencyOperation => "catalog.course-unpublish.v1";

    public string IdempotencyScope => $"admin:{ActorUserId:D}";

    public object IdempotencyPayload => new { CourseId, AuditReason };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(24);
}

public sealed record ActivateCourseReleaseCommand(
    Guid ActorUserId,
    Guid CourseId,
    string AuditReason,
    PublicationManifest Manifest) : ITransactionalCommand<CourseReleaseResponse>;

public sealed record ResolvePublicCourseQuery(string Locale, string Slug)
    : IQuery<PublicCourseLookupResponse>;

internal sealed class PublishCourseCommandHandler(IPublishingCoordinator coordinator)
    : IRequestHandler<PublishCourseCommand, Result<CourseReleaseResponse>>
{
    public Task<Result<CourseReleaseResponse>> Handle(
        PublishCourseCommand request,
        CancellationToken cancellationToken) => coordinator.PublishAsync(request, cancellationToken);
}

internal sealed class UnpublishCourseCommandHandler(ICatalogActivationPort activationPort)
    : IRequestHandler<UnpublishCourseCommand, Result<CourseReleaseResponse>>
{
    public Task<Result<CourseReleaseResponse>> Handle(
        UnpublishCourseCommand request,
        CancellationToken cancellationToken) => activationPort.UnpublishAsync(request, cancellationToken);
}

internal sealed class ActivateCourseReleaseCommandHandler(ICatalogActivationPort activationPort)
    : IRequestHandler<ActivateCourseReleaseCommand, Result<CourseReleaseResponse>>
{
    public Task<Result<CourseReleaseResponse>> Handle(
        ActivateCourseReleaseCommand request,
        CancellationToken cancellationToken) => activationPort.ActivateAsync(request, cancellationToken);
}

internal sealed class ResolvePublicCourseQueryHandler(IPublicCatalogPort catalogPort)
    : IRequestHandler<ResolvePublicCourseQuery, Result<PublicCourseLookupResponse>>
{
    public Task<Result<PublicCourseLookupResponse>> Handle(
        ResolvePublicCourseQuery request,
        CancellationToken cancellationToken) => catalogPort.ResolveAsync(request, cancellationToken);
}

public sealed class PublishingCoordinator(
    IAuthoringPublishingPort authoring,
    IMediaPublishingPort media,
    IAssessmentPublishingPort assessment,
    IPublishingAuditPort audit,
    ISender sender) : IPublishingCoordinator
{
    public async Task<Result<CourseReleaseResponse>> PublishAsync(
        PublishCourseCommand request,
        CancellationToken cancellationToken)
    {
        AuthoringPublicationSnapshot authoringSnapshot = await authoring.GetSnapshotAsync(
            request.CourseId,
            cancellationToken);
        if (!authoringSnapshot.Ready)
        {
            return await FailureAsync(request, authoringSnapshot.Failure!, cancellationToken);
        }

        MediaPublicationSnapshot mediaSnapshot = await media.CheckReadinessAsync(
            request.CourseId,
            authoringSnapshot.MediaReferences,
            cancellationToken);
        if (!mediaSnapshot.Ready)
        {
            return await FailureAsync(request, mediaSnapshot.Failure!, cancellationToken);
        }

        AssessmentPublicationSnapshot assessmentSnapshot = await assessment.CheckReadinessAsync(
            request.CourseId,
            authoringSnapshot.QuizVersionIds,
            authoringSnapshot.AssignmentVersionIds,
            cancellationToken);
        if (!assessmentSnapshot.Ready)
        {
            return await FailureAsync(request, assessmentSnapshot.Failure!, cancellationToken);
        }

        var assessments = authoringSnapshot.Sections
            .SelectMany(section => section.Lessons)
            .SelectMany(lesson =>
            {
                var values = new List<ReleaseAssessmentSnapshot>(2);
                if (lesson.QuizVersionId is { } quizVersionId)
                {
                    values.Add(new ReleaseAssessmentSnapshot(
                        lesson.SourceLessonId,
                        ReleaseAssessmentKind.Quiz,
                        quizVersionId,
                        lesson.Position,
                        assessmentSnapshot.Audiences.Single(audience => audience.VersionId == quizVersionId).AudienceType));
                }
                if (lesson.AssignmentVersionId is { } assignmentVersionId)
                {
                    values.Add(new ReleaseAssessmentSnapshot(
                        lesson.SourceLessonId,
                        ReleaseAssessmentKind.Assignment,
                        assignmentVersionId,
                        lesson.Position,
                        assessmentSnapshot.Audiences.Single(audience => audience.VersionId == assignmentVersionId).AudienceType));
                }

                return values;
            })
            .ToArray();
        int durationMinutes = (int)Math.Ceiling(
            mediaSnapshot.Variants
                .Where(variant => variant.DurationSeconds.HasValue)
                .GroupBy(variant => variant.SourceLessonId)
                .Sum(group => group.Max(variant => variant.DurationSeconds!.Value)) / 60m);
        var manifest = new PublicationManifest(
            authoringSnapshot.CourseId,
            authoringSnapshot.DraftId,
            authoringSnapshot.DraftVersion,
            authoringSnapshot.DefaultLocale,
            authoringSnapshot.Level,
            authoringSnapshot.Localizations,
            authoringSnapshot.Sections,
            assessments,
            mediaSnapshot.Variants,
            mediaSnapshot.Captions,
            authoringSnapshot.Instructors,
            authoringSnapshot.Taxonomy,
            durationMinutes);

        return await sender.Send(
            new ActivateCourseReleaseCommand(
                request.ActorUserId,
                request.CourseId,
                request.AuditReason,
                manifest),
            cancellationToken);
    }

    private async Task<Result<CourseReleaseResponse>> FailureAsync(
        PublishCourseCommand request,
        PublishingFailure failure,
        CancellationToken cancellationToken)
    {
        await audit.RecordFailedActivationAsync(request.ActorUserId, request.CourseId, failure, cancellationToken);
        return Result.Failure<CourseReleaseResponse>(ResultError.BusinessRule(failure.Code, failure.Description));
    }
}
