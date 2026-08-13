using System.Text;
using System.Text.Json;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Assessment;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Catalog;

internal sealed class AuthoringPublishingPort(DorosakDbContext dbContext)
    : IAuthoringPublishingPort
{
    public async Task<AuthoringPublicationSnapshot> GetSnapshotAsync(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        Course? course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == courseId && candidate.DeletedAt == null,
            cancellationToken);
        if (course is null)
        {
            return Empty(courseId, new PublishingFailure("COURSE.NOT_FOUND", "The course was not found."));
        }

        CourseDraft draft = await dbContext.CourseDrafts.AsNoTracking().SingleAsync(
            candidate => candidate.CourseId == courseId,
            cancellationToken);
        PublicationReview? review = await dbContext.PublicationReviews.AsNoTracking()
            .Where(candidate => candidate.CourseId == courseId && candidate.Status == PublicationReviewStatus.Approved)
            .OrderByDescending(candidate => candidate.UpdatedAt)
            .ThenByDescending(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (course.Status is not (CourseStatus.ReadyToPublish or CourseStatus.Published or CourseStatus.Unpublished))
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.NOT_READY",
                "The course must have an approved release review before it can be published."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }
        if (review is null || review.DraftId != draft.Id || review.DraftVersion != draft.Version)
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.STALE_REVIEW",
                "The approved review does not match the current draft."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }

        var localizationRows = await (
            from localization in dbContext.CourseLocalizations.AsNoTracking()
            join slug in dbContext.CourseSlugs.AsNoTracking() on localization.CurrentSlugId equals slug.Id
            where localization.CourseId == courseId
            select new ReleaseLocalizationSnapshot(
                localization.Locale,
                slug.Slug,
                localization.Title,
                localization.Subtitle,
                localization.Description)).ToListAsync(cancellationToken);
        if (!localizationRows.Any(item => item.Locale == course.DefaultLocale &&
                item.Title.Length > 0 && item.Description.Length > 0))
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.METADATA_INCOMPLETE",
                "The default localization is incomplete."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }

        List<CourseSection> sections = await dbContext.CourseSections.AsNoTracking()
            .Where(section => section.DraftId == draft.Id && section.RemovedAt == null)
            .OrderBy(section => section.Position)
            .ThenBy(section => section.Id)
            .ToListAsync(cancellationToken);
        List<CourseLesson> lessons = await dbContext.CourseLessons.AsNoTracking()
            .Where(lesson => lesson.DraftId == draft.Id && lesson.RemovedAt == null)
            .OrderBy(lesson => lesson.Position)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);
        Guid[] sectionRevisionIds = sections
            .Where(section => section.CurrentRevisionId.HasValue)
            .Select(section => section.CurrentRevisionId!.Value)
            .ToArray();
        Guid[] lessonRevisionIds = lessons
            .Where(lesson => lesson.CurrentRevisionId.HasValue)
            .Select(lesson => lesson.CurrentRevisionId!.Value)
            .ToArray();
        Dictionary<Guid, SectionRevision> sectionRevisions = await dbContext.SectionRevisions.AsNoTracking()
            .Where(revision => sectionRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        Dictionary<Guid, LessonRevision> lessonRevisions = await dbContext.LessonRevisions.AsNoTracking()
            .Where(revision => lessonRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        if (sections.Count == 0 || lessons.Count == 0 ||
            sections.Any(section => section.CurrentRevisionId is null || !sectionRevisions.ContainsKey(section.CurrentRevisionId.Value)) ||
            lessons.Any(lesson => lesson.CurrentRevisionId is null || !lessonRevisions.ContainsKey(lesson.CurrentRevisionId.Value)))
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.CURRICULUM_INCOMPLETE",
                "Every published course requires ordered sections and lessons with current revisions."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }

        var sectionSnapshots = sections.Select(section =>
        {
            SectionRevision sectionRevision = sectionRevisions[section.CurrentRevisionId!.Value];
            ReleaseLessonSnapshot[] lessonSnapshots = lessons
                .Where(lesson => lesson.SectionId == section.Id)
                .OrderBy(lesson => lesson.Position)
                .ThenBy(lesson => lesson.Id)
                .Select(lesson =>
                {
                    LessonRevision revision = lessonRevisions[lesson.CurrentRevisionId!.Value];
                    decimal completionRequirement = revision.LessonType == "Video" ? 0.9m : 1m;
                    return new ReleaseLessonSnapshot(
                        lesson.Id,
                        revision.Id,
                        lesson.Position,
                        revision.Title,
                        revision.LessonType,
                        revision.Content,
                        revision.MediaAssetId,
                        revision.QuizVersionId,
                        revision.AssignmentVersionId,
                        completionRequirement);
                })
                .ToArray();
            return new ReleaseSectionSnapshot(
                section.Id,
                sectionRevision.Id,
                section.Position,
                sectionRevision.Title,
                lessonSnapshots);
        }).ToArray();
        if (sectionSnapshots.Any(section => section.Lessons.Count == 0))
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.CURRICULUM_INCOMPLETE",
                "Every published section requires at least one lesson."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }

        ReleaseLessonSnapshot[] allLessons = sectionSnapshots.SelectMany(section => section.Lessons).ToArray();
        if (allLessons.Any(lesson => lesson.LessonType == "Quiz" && lesson.QuizVersionId is null) ||
            allLessons.Any(lesson => lesson.LessonType == "Assignment" && lesson.AssignmentVersionId is null))
        {
            return Empty(courseId, new PublishingFailure(
                "PUBLICATION.ASSESSMENT_REFERENCE_MISSING",
                "Quiz and assignment lessons must reference a version."),
                draft.Id,
                draft.Version,
                course.DefaultLocale,
                draft.Level);
        }

        Guid[] instructorIds = [course.OwnerUserId, .. await dbContext.CourseInstructors.AsNoTracking()
            .Where(instructor => instructor.CourseId == courseId)
            .OrderBy(instructor => instructor.AddedAt)
            .Select(instructor => instructor.UserId)
            .ToArrayAsync(cancellationToken)];
        Dictionary<Guid, string> displayNames = await dbContext.Users.AsNoTracking()
            .Where(user => instructorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        ReleaseInstructorSnapshot[] instructors = instructorIds
            .Distinct()
            .Select((userId, position) => new ReleaseInstructorSnapshot(
                userId,
                displayNames.GetValueOrDefault(userId, "Instructor"),
                position))
            .ToArray();

        ReleaseTaxonomySnapshot[] taxonomy = await LoadTaxonomyAsync(courseId, course.DefaultLocale, cancellationToken);
        return new AuthoringPublicationSnapshot(
            courseId,
            draft.Id,
            draft.Version,
            course.DefaultLocale,
            draft.Level,
            localizationRows.OrderBy(item => item.Locale).ToArray(),
            sectionSnapshots,
            instructors,
            taxonomy,
            null);
    }

    private async Task<ReleaseTaxonomySnapshot[]> LoadTaxonomyAsync(
        Guid courseId,
        string locale,
        CancellationToken cancellationToken)
    {
        var categories = await (
            from link in dbContext.CourseCategories.AsNoTracking()
            join category in dbContext.Categories.AsNoTracking() on link.CategoryId equals category.Id
            join localization in dbContext.CategoryLocalizations.AsNoTracking()
                on new { CategoryId = category.Id, Locale = locale }
                equals new { localization.CategoryId, localization.Locale } into localizations
            from localization in localizations.DefaultIfEmpty()
            where link.CourseId == courseId
            select new ReleaseTaxonomySnapshot(
                category.Id,
                category.Code,
                localization == null ? category.Code : localization.Name,
                true)).ToListAsync(cancellationToken);
        var tags = await (
            from link in dbContext.CourseTags.AsNoTracking()
            join tag in dbContext.Tags.AsNoTracking() on link.TagId equals tag.Id
            join localization in dbContext.TagLocalizations.AsNoTracking()
                on new { TagId = tag.Id, Locale = locale }
                equals new { localization.TagId, localization.Locale } into localizations
            from localization in localizations.DefaultIfEmpty()
            where link.CourseId == courseId
            select new ReleaseTaxonomySnapshot(
                tag.Id,
                tag.Code,
                localization == null ? tag.Code : localization.Name,
                false)).ToListAsync(cancellationToken);
        return [.. categories.OrderBy(item => item.Code), .. tags.OrderBy(item => item.Code)];
    }

    private static AuthoringPublicationSnapshot Empty(
        Guid courseId,
        PublishingFailure failure,
        Guid draftId = default,
        long draftVersion = 0,
        string defaultLocale = "en",
        string level = "AllLevels") => new(
            courseId,
            draftId,
            draftVersion,
            defaultLocale,
            level,
            [],
            [],
            [],
            [],
            failure);
}

internal sealed class MediaPublishingPort(DorosakDbContext dbContext)
    : IMediaPublishingPort
{
    public async Task<MediaPublicationSnapshot> CheckReadinessAsync(
        Guid courseId,
        IReadOnlyList<MediaAssetReference> references,
        CancellationToken cancellationToken)
    {
        if (references.Count == 0)
        {
            return new MediaPublicationSnapshot([], [], null);
        }

        Guid[] assetIds = references.Select(reference => reference.AssetId).Distinct().ToArray();
        MediaAsset[] assets = await dbContext.MediaAssets.AsNoTracking()
            .Where(asset => asset.CourseId == courseId && assetIds.Contains(asset.Id))
            .ToArrayAsync(cancellationToken);
        if (assets.Length != assetIds.Length || assets.Any(asset => asset.State != MediaAssetState.Ready))
        {
            return NotReady("MEDIA.NOT_READY", "Every referenced media asset must be Ready.");
        }

        MediaVariant[] variants = await dbContext.MediaVariants.AsNoTracking()
            .Where(variant => assetIds.Contains(variant.AssetId))
            .ToArrayAsync(cancellationToken);
        if (assetIds.Any(assetId => !variants.Any(variant => variant.AssetId == assetId)))
        {
            return NotReady("MEDIA.VARIANT_NOT_READY", "Every referenced media asset requires a ready playback variant.");
        }

        Dictionary<Guid, Guid[]> lessonsByAsset = references
            .GroupBy(reference => reference.AssetId)
            .ToDictionary(group => group.Key, group => group.Select(reference => reference.SourceLessonId).ToArray());
        var variantSnapshots = variants
            .SelectMany(variant => lessonsByAsset[variant.AssetId].Select(lessonId => new ReleaseMediaVariantSnapshot(
                lessonId,
                variant.AssetId,
                variant.Id,
                variant.Kind,
                variant.ContentType,
                variant.Bytes,
                variant.Width,
                variant.Height,
                variant.DurationSeconds)))
            .ToArray();

        CaptionTrack[] tracks = await dbContext.CaptionTracks.AsNoTracking()
            .Where(track => assetIds.Contains(track.SourceMediaAssetId))
            .ToArrayAsync(cancellationToken);
        if (tracks.Any(track => track.State != CaptionTrackState.Ready))
        {
            return NotReady("MEDIA.CAPTION_NOT_READY", "Every referenced caption must be Ready.");
        }
        var captionSnapshots = tracks
            .SelectMany(track => lessonsByAsset[track.SourceMediaAssetId].Select(lessonId => new ReleaseCaptionSnapshot(
                lessonId,
                track.SourceMediaAssetId,
                track.Id,
                track.Locale,
                track.Label)))
            .ToArray();
        return new MediaPublicationSnapshot(variantSnapshots, captionSnapshots, null);
    }

    private static MediaPublicationSnapshot NotReady(string code, string description) =>
        new([], [], new PublishingFailure(code, description));
}

internal sealed class AssessmentPublishingPort(DorosakDbContext dbContext)
    : IAssessmentPublishingPort
{
    public async Task<AssessmentPublicationSnapshot> CheckReadinessAsync(
        Guid courseId,
        IReadOnlyList<Guid> quizVersionIds,
        IReadOnlyList<Guid> assignmentVersionIds,
        CancellationToken cancellationToken)
    {
        Guid[] quizIds = quizVersionIds.Distinct().ToArray();
        Guid[] assignmentIds = assignmentVersionIds.Distinct().ToArray();
        if (quizIds.Length == 0 && assignmentIds.Length == 0)
        {
            return new AssessmentPublicationSnapshot([], null);
        }
        int readyQuizzes = await dbContext.QuizVersions.AsNoTracking()
            .Where(version => version.Status == AssessmentVersionStatus.Ready && quizIds.Contains(version.Id))
            .CountAsync(cancellationToken);
        int readyAssignments = await dbContext.AssignmentVersions.AsNoTracking()
            .Where(version => version.Status == AssessmentVersionStatus.Ready && assignmentIds.Contains(version.Id))
            .CountAsync(cancellationToken);
        int courseQuizzes = await dbContext.QuizVersions.AsNoTracking()
            .Where(version => quizIds.Contains(version.Id))
            .Join(dbContext.Quizzes.AsNoTracking(), version => version.QuizId, quiz => quiz.Id, (_, quiz) => quiz)
            .CountAsync(quiz => quiz.CourseId == courseId, cancellationToken);
        int courseAssignments = await dbContext.AssignmentVersions.AsNoTracking()
            .Where(version => assignmentIds.Contains(version.Id))
            .Join(dbContext.Assignments.AsNoTracking(), version => version.AssignmentId, assignment => assignment.Id, (_, assignment) => assignment)
            .CountAsync(assignment => assignment.CourseId == courseId, cancellationToken);
        if (readyQuizzes != quizIds.Length || readyAssignments != assignmentIds.Length ||
            courseQuizzes != quizIds.Length || courseAssignments != assignmentIds.Length)
        {
            return new AssessmentPublicationSnapshot([], new PublishingFailure(
                "ASSESSMENT.NOT_READY",
                "Every referenced quiz and assignment version must be Ready for this course."));
        }

        AssessmentVersionAudience[] audiences = [
            .. await dbContext.QuizVersions.AsNoTracking()
                .Where(version => quizIds.Contains(version.Id))
                .Select(version => new AssessmentVersionAudience(version.Id, version.AudienceType.ToString()))
                .ToArrayAsync(cancellationToken),
            .. await dbContext.AssignmentVersions.AsNoTracking()
                .Where(version => assignmentIds.Contains(version.Id))
                .Select(version => new AssessmentVersionAudience(version.Id, version.AudienceType.ToString()))
                .ToArrayAsync(cancellationToken),
        ];
        return new AssessmentPublicationSnapshot(audiences, null);
    }
}

internal sealed class PublishingAuditPort(DorosakDbContext dbContext, TimeProvider timeProvider)
    : IPublishingAuditPort
{
    public Task RecordFailedActivationAsync(
        Guid actorUserId,
        Guid courseId,
        PublishingFailure failure,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(AuditLog.Create(
            actorUserId,
            "course.publish-failed",
            "Course",
            courseId,
            "Failed",
            failure.Code,
            timeProvider.GetUtcNow()));
        return Task.CompletedTask;
    }
}

internal sealed class CatalogProjectionGenerationPort(DorosakDbContext dbContext)
    : ICatalogProjectionGenerationPort
{
    public Task<long> GetAsync(CancellationToken cancellationToken) => dbContext.CatalogProjectionStates
        .AsNoTracking()
        .Where(state => state.Singleton)
        .Select(state => state.Generation)
        .SingleAsync(cancellationToken);

    public async Task<long> AdvanceAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE catalog.projection_state SET generation = generation + 1 WHERE singleton",
            cancellationToken);
        return await GetAsync(cancellationToken);
    }
}

internal sealed class CatalogActivationPort(
    DorosakDbContext dbContext,
    ICatalogProjectionGenerationPort projectionGeneration,
    TimeProvider timeProvider) : ICatalogActivationPort
{
    public async Task<Result<CourseReleaseResponse>> ActivateAsync(
        ActivateCourseReleaseCommand request,
        CancellationToken cancellationToken)
    {
        await LockCourseAsync(request.CourseId, cancellationToken);
        Course? course = await dbContext.Courses.SingleOrDefaultAsync(
            candidate => candidate.Id == request.CourseId && candidate.DeletedAt == null,
            cancellationToken);
        if (course is null)
        {
            return Failure<CourseReleaseResponse>("COURSE.NOT_FOUND", "The course was not found.");
        }

        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(
            candidate => candidate.CourseId == request.CourseId,
            cancellationToken);
        if (draft.Id != request.Manifest.SourceDraftId || draft.Version != request.Manifest.SourceDraftVersion)
        {
            return await FailAsync<CourseReleaseResponse>(
                request,
                "PUBLICATION.STALE_DRAFT",
                "The draft changed while the release was being prepared.",
                cancellationToken);
        }
        PublicationReview? approvedReview = await dbContext.PublicationReviews
            .Where(review => review.CourseId == request.CourseId && review.Status == PublicationReviewStatus.Approved)
            .OrderByDescending(review => review.UpdatedAt)
            .ThenByDescending(review => review.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (approvedReview is null || approvedReview.DraftId != draft.Id || approvedReview.DraftVersion != draft.Version)
        {
            return await FailAsync<CourseReleaseResponse>(
                request,
                "PUBLICATION.STALE_REVIEW",
                "The approved review does not match the current draft.",
                cancellationToken);
        }

        if (course.Status is not (CourseStatus.ReadyToPublish or CourseStatus.Published or CourseStatus.Unpublished))
        {
            return await FailAsync<CourseReleaseResponse>(
                request,
                "PUBLICATION.NOT_READY",
                "The course is not approved for publication.",
                cancellationToken);
        }

        CourseRelease? existingRelease = await dbContext.CourseReleases.SingleOrDefaultAsync(
            release => release.CourseId == request.CourseId &&
                release.SourceDraftId == request.Manifest.SourceDraftId &&
                release.SourceDraftVersion == request.Manifest.SourceDraftVersion,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (existingRelease is not null &&
            !string.Equals(existingRelease.ManifestHash, request.Manifest.ManifestHash, StringComparison.Ordinal))
        {
            return await FailAsync<CourseReleaseResponse>(
                request,
                "PUBLICATION.MANIFEST_CHANGED",
                "The previously released draft no longer produces the same immutable manifest.",
                cancellationToken);
        }

        CourseRelease? currentRelease = course.ActiveReleaseId is { } currentReleaseId
            ? await dbContext.CourseReleases.SingleAsync(release => release.Id == currentReleaseId, cancellationToken)
            : null;
        if (currentRelease is not null && currentRelease.State != CourseReleaseState.Active)
        {
            return await FailAsync<CourseReleaseResponse>(
                request,
                "RELEASE.STATE_INCONSISTENT",
                "The current course release state is inconsistent.",
                cancellationToken);
        }
        if (existingRelease is not null)
        {
            if (course.Status == CourseStatus.Published && course.ActiveReleaseId == existingRelease.Id)
            {
                return Result.Success(Map(course, existingRelease));
            }
            if (currentRelease is not null && currentRelease.Id != existingRelease.Id)
            {
                currentRelease.Supersede();
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            existingRelease.Activate();
            long generation = await projectionGeneration.AdvanceAsync(cancellationToken);
            course.ActivateRelease(existingRelease.Id, generation, now);
            AddAudit(request.ActorUserId, "course.published", request.CourseId, request.AuditReason, "Succeeded");
            AddOutbox("catalog.release-activated.v1", request.CourseId, existingRelease.Id, course.ProjectionGeneration, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(course, existingRelease));
        }

        int releaseNumber = (await dbContext.CourseReleases
            .Where(release => release.CourseId == request.CourseId)
            .Select(release => (int?)release.ReleaseNumber)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        CourseRelease release = CourseRelease.Create(
            request.CourseId,
            request.Manifest.SourceDraftId,
            request.Manifest.SourceDraftVersion,
            releaseNumber,
            request.Manifest.DefaultLocale,
            request.Manifest.ManifestHash,
            request.ActorUserId,
            now);
        if (currentRelease is not null)
        {
            currentRelease.Supersede();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        long nextGeneration = await projectionGeneration.AdvanceAsync(cancellationToken);
        course.ActivateRelease(release.Id, nextGeneration, now);
        dbContext.CourseReleases.Add(release);
        var releaseSections = new Dictionary<Guid, CourseReleaseSection>();
        foreach (ReleaseSectionSnapshot section in request.Manifest.Sections.OrderBy(section => section.Position))
        {
            var releaseSection = CourseReleaseSection.Create(
                release.Id,
                section.SourceSectionId,
                section.SourceRevisionId,
                section.Position,
                section.Title);
            releaseSections.Add(section.SourceSectionId, releaseSection);
            dbContext.CourseReleaseSections.Add(releaseSection);
        }
        var releaseLessons = new Dictionary<Guid, CourseReleaseLesson>();
        foreach (ReleaseSectionSnapshot section in request.Manifest.Sections.OrderBy(section => section.Position))
        {
            foreach (ReleaseLessonSnapshot lesson in section.Lessons.OrderBy(lesson => lesson.Position))
            {
                var releaseLesson = CourseReleaseLesson.Create(
                    release.Id,
                    releaseSections[section.SourceSectionId].Id,
                    lesson.SourceLessonId,
                    lesson.SourceRevisionId,
                    lesson.Position,
                    lesson.Title,
                    lesson.LessonType,
                    lesson.Content,
                    lesson.MediaAssetId,
                    lesson.CompletionRequirement);
                releaseLessons.Add(lesson.SourceLessonId, releaseLesson);
                dbContext.CourseReleaseLessons.Add(releaseLesson);
            }
        }
        foreach (ReleaseAssessmentSnapshot assessment in request.Manifest.Assessments)
        {
            CourseReleaseAssessment releaseAssessment = CourseReleaseAssessment.Create(
                release.Id,
                releaseLessons[assessment.SourceLessonId].Id,
                assessment.Type == ReleaseAssessmentKind.Quiz ? ReleaseAssessmentType.Quiz : ReleaseAssessmentType.Assignment,
                assessment.VersionId,
                assessment.Position);
            if (!Enum.TryParse(assessment.AudienceType, out AssessmentAudienceType audienceType))
            {
                throw new InvalidOperationException("The assessment audience snapshot is invalid.");
            }
            releaseAssessment.SetAudience(audienceType);
            dbContext.CourseReleaseAssessments.Add(releaseAssessment);
        }
        foreach (ReleaseMediaVariantSnapshot variant in request.Manifest.MediaVariants)
        {
            dbContext.CourseReleaseMediaVariants.Add(CourseReleaseMediaVariant.Create(
                release.Id,
                releaseLessons[variant.SourceLessonId].Id,
                variant.AssetId,
                variant.VariantId,
                variant.Kind,
                variant.ContentType,
                variant.Bytes,
                variant.Width,
                variant.Height,
                variant.DurationSeconds));
        }
        foreach (ReleaseCaptionSnapshot caption in request.Manifest.Captions)
        {
            dbContext.CourseReleaseCaptions.Add(CourseReleaseCaption.Create(
                release.Id,
                releaseLessons[caption.SourceLessonId].Id,
                caption.AssetId,
                caption.CaptionId,
                caption.Locale,
                caption.Label));
        }
        dbContext.CourseReleaseLocalizations.AddRange(request.Manifest.Localizations.Select(localization =>
            CourseReleaseLocalization.Create(
                release.Id,
                localization.Locale,
                localization.Slug,
                localization.Title,
                localization.Subtitle,
                localization.Description)));
        dbContext.CourseReleaseInstructors.AddRange(request.Manifest.Instructors.Select(instructor =>
            CourseReleaseInstructor.Create(release.Id, instructor.UserId, instructor.DisplayName, instructor.Position)));
        dbContext.CourseReleaseTaxonomies.AddRange(request.Manifest.Taxonomy.Select(term =>
            CourseReleaseTaxonomy.Create(release.Id, term.TermId, term.Code, term.Name, term.IsCategory)));
        dbContext.CatalogDocuments.AddRange(request.Manifest.Localizations.Select(localization =>
            CatalogDocument.Create(
                release.Id,
                request.CourseId,
                localization.Locale,
                localization.Slug,
                localization.Title,
                localization.Subtitle,
                localization.Description,
                localization.Locale,
                request.Manifest.Level,
                request.Manifest.DurationMinutes,
                SearchTextNormalizer.Normalize(BuildSearchText(localization, request.Manifest), localization.Locale),
                SearchTextNormalizer.Normalize(BuildSearchText(localization, request.Manifest), "ar"),
                now,
                course.ProjectionGeneration)));

        AddAudit(request.ActorUserId, "course.published", request.CourseId, request.AuditReason, "Succeeded");
        AddOutbox("catalog.release-activated.v1", request.CourseId, release.Id, course.ProjectionGeneration, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(course, release));
    }

    public async Task<Result<CourseReleaseResponse>> UnpublishAsync(
        UnpublishCourseCommand request,
        CancellationToken cancellationToken)
    {
        await LockCourseAsync(request.CourseId, cancellationToken);
        Course? course = await dbContext.Courses.SingleOrDefaultAsync(
            candidate => candidate.Id == request.CourseId && candidate.DeletedAt == null,
            cancellationToken);
        if (course is null)
        {
            return Failure<CourseReleaseResponse>("COURSE.NOT_FOUND", "The course was not found.");
        }
        CourseRelease? release = course.ActiveReleaseId is { } activeReleaseId
            ? await dbContext.CourseReleases.SingleOrDefaultAsync(candidate => candidate.Id == activeReleaseId, cancellationToken)
            : await dbContext.CourseReleases
                .Where(candidate => candidate.CourseId == request.CourseId)
                .OrderByDescending(candidate => candidate.ReleaseNumber)
                .FirstOrDefaultAsync(cancellationToken);
        if (release is null)
        {
            return Failure<CourseReleaseResponse>("COURSE.NOT_PUBLISHED", "The course has no published release.");
        }
        if (course.ActiveReleaseId is null && course.Status == CourseStatus.Unpublished)
        {
            return Result.Success(Map(course, release));
        }
        if (release.State != CourseReleaseState.Active)
        {
            return Failure<CourseReleaseResponse>(
                "RELEASE.STATE_INCONSISTENT",
                "The current course release state is inconsistent.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        release.Unpublish();
        long generation = await projectionGeneration.AdvanceAsync(cancellationToken);
        course.Unpublish(generation, now);
        AddAudit(request.ActorUserId, "course.unpublished", request.CourseId, request.AuditReason, "Succeeded");
        AddOutbox("catalog.release-unpublished.v1", request.CourseId, release.Id, course.ProjectionGeneration, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(course, release));
    }

    private async Task LockCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM catalog.courses WHERE id = {courseId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private void AddAudit(Guid actorUserId, string action, Guid courseId, string reason, string result) =>
        dbContext.AuditLogs.Add(AuditLog.Create(
            actorUserId,
            action,
            "Course",
            courseId,
            result,
            reason,
            timeProvider.GetUtcNow()));

    private void AddOutbox(string eventType, Guid courseId, Guid releaseId, long generation, DateTimeOffset now)
    {
        string payload = JsonSerializer.Serialize(new { courseId, releaseId, generation, cacheTag = "catalog-public" });
        dbContext.OutboxMessages.Add(OutboxMessage.Create(eventType, 1, payload, "{}", now));
    }

    private async Task<Result<T>> FailAsync<T>(
        ActivateCourseReleaseCommand request,
        string code,
        string description,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.ActorUserId,
            "course.publish-failed",
            "Course",
            request.CourseId,
            "Failed",
            code,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Failure<T>(code, description);
    }

    private static Result<T> Failure<T>(string code, string description) =>
        Result.Failure<T>(ResultError.BusinessRule(code, description));

    private static CourseReleaseResponse Map(Course course, CourseRelease release) => new(
        course.Id,
        release.Id,
        release.ReleaseNumber,
        release.ManifestHash,
        release.State.ToString(),
        release.PublishedAt,
        course.ProjectionGeneration);

    private static string BuildSearchText(
        ReleaseLocalizationSnapshot localization,
        PublicationManifest manifest)
    {
        string taxonomy = string.Join(' ', manifest.Taxonomy.Select(term => $"{term.Code} {term.Name}"));
        string instructors = string.Join(' ', manifest.Instructors.Select(instructor => instructor.DisplayName));
        string lessons = string.Join(' ', manifest.Sections.SelectMany(section => section.Lessons)
            .Select(lesson => $"{lesson.Title} {lesson.Content}"));
        return string.Join(' ', localization.Title, localization.Subtitle, localization.Description, taxonomy, instructors, lessons)
            .Trim();
    }
}

