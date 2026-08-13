using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Authoring;

internal sealed class CourseAuthoringService(
    DorosakDbContext dbContext,
    CatalogCursorCodec cursorCodec,
    TimeProvider timeProvider) : IAuthoringService, ICurriculumService, ICourseAccessReader
{
    public async Task<Result<CourseMutationResponse>> CreateCourseAsync(
        CreateCourseCommand request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.TeacherProfiles.AnyAsync(profile => profile.UserId == request.UserId, cancellationToken))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.Forbidden(
                "COURSE.TEACHER_APPROVAL_REQUIRED",
                "An approved teacher profile is required."));
        }

        string defaultLocale = InfrastructureHelpers.NormalizeLocale(request.DefaultLocale);
        if (!request.Localizations.Any(localization => InfrastructureHelpers.NormalizeLocale(localization.Locale) == defaultLocale))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.DEFAULT_LOCALIZATION_REQUIRED",
                "The default course localization is required."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Course course = Course.Create(request.UserId, defaultLocale, now);
        CourseDraft draft = CourseDraft.Create(course.Id, InfrastructureHelpers.NormalizeLevel(request.Level), now);
        dbContext.Courses.Add(course);
        dbContext.CourseDrafts.Add(draft);

        ResultError? metadataError = await AddInitialMetadataAsync(course, request.Localizations, cancellationToken);
        if (metadataError is not null)
        {
            return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, metadataError);
        }

        ResultError? taxonomyError = await ReplaceCourseTaxonomyAsync(
            course.Id,
            request.CategoryCodes,
            request.TagCodes,
            cancellationToken);
        if (taxonomyError is not null)
        {
            return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, taxonomyError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    public async Task<Result<PagedResponse<CourseSummaryResponse>>> GetInstructorCoursesAsync(
        GetInstructorCoursesQuery request,
        CancellationToken cancellationToken)
    {
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 20);
        string canonical = $"instructor-courses|{request.UserId:D}|updated-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "instructor-courses", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<CourseSummaryResponse>>();
        }

        IQueryable<Course> query = dbContext.Courses.AsNoTracking().Where(course =>
            course.DeletedAt == null &&
            (course.OwnerUserId == request.UserId || dbContext.CourseInstructors.Any(instructor =>
                instructor.CourseId == course.Id && instructor.UserId == request.UserId)));
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(course => course.UpdatedAt < timestamp || course.UpdatedAt == timestamp && course.Id.CompareTo(id) < 0);
        }

        List<Course> courses = await query
            .OrderByDescending(course => course.UpdatedAt)
            .ThenByDescending(course => course.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        IReadOnlyDictionary<Guid, CourseSummaryParts> parts = await LoadCourseSummaryPartsAsync(
            courses.Take(limit).Select(course => course.Id).ToArray(),
            cancellationToken);
        return Result.Success(InfrastructureHelpers.Page(
            courses,
            limit,
            course => MapSummary(course, parts.GetValueOrDefault(course.Id)),
            course => course.UpdatedAt,
            course => course.Id,
            "instructor-courses",
            canonical,
            cursorCodec));
    }

    public async Task<Result<CourseDetailsResponse>> GetCourseAsync(
        GetCourseQuery request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseDetailsResponse>(InfrastructureHelpers.CourseNotFound());
        }

        CourseDraft draft = await dbContext.CourseDrafts.AsNoTracking().SingleAsync(
            candidate => candidate.CourseId == course.Id,
            cancellationToken);
        List<CourseLocalizationResponse> localizations = await LoadLocalizationsAsync(course.Id, cancellationToken);
        CourseInstructor[] instructorEntities = await dbContext.CourseInstructors
            .AsNoTracking()
            .Where(instructor => instructor.CourseId == course.Id)
            .OrderBy(instructor => instructor.AddedAt)
            .ToArrayAsync(cancellationToken);
        CourseCollaboratorResponse[] collaborators = instructorEntities
            .Select(instructor => new CourseCollaboratorResponse(
                instructor.UserId,
                instructor.Role.ToString(),
                instructor.AddedAt))
            .ToArray();
        string[] categoryCodes = await (
            from link in dbContext.CourseCategories.AsNoTracking()
            join category in dbContext.Categories.AsNoTracking() on link.CategoryId equals category.Id
            where link.CourseId == course.Id
            orderby category.Code
            select category.Code)
            .ToArrayAsync(cancellationToken);
        string[] tagCodes = await (
            from link in dbContext.CourseTags.AsNoTracking()
            join tag in dbContext.Tags.AsNoTracking() on link.TagId equals tag.Id
            where link.CourseId == course.Id
            orderby tag.Code
            select tag.Code)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new CourseDetailsResponse(
            course.Id,
            course.OwnerUserId,
            course.DefaultLocale,
            course.Status.ToString(),
            draft.Version,
            draft.Level,
            categoryCodes,
            tagCodes,
            localizations,
            collaborators,
            course.CreatedAt,
            course.UpdatedAt));
    }

    public async Task<Result<CourseMutationResponse>> UpdateCourseMetadataAsync(
        UpdateCourseMetadataCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion is null)
        {
            return InfrastructureHelpers.PreconditionRequired<CourseMutationResponse>();
        }

        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Edit, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(InfrastructureHelpers.CourseNotFound());
        }
        if (course.Status is not (CourseStatus.Draft or CourseStatus.ChangesRequested))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.DRAFT_LOCKED",
                "Course metadata cannot be changed in the current state."));
        }

        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        if (draft.Version != request.ExpectedVersion.Value)
        {
            return InfrastructureHelpers.VersionConflict<CourseMutationResponse>(draft.Version);
        }

        string defaultLocale = InfrastructureHelpers.NormalizeLocale(request.DefaultLocale);
        if (!request.Localizations.Any(localization => InfrastructureHelpers.NormalizeLocale(localization.Locale) == defaultLocale) &&
            !await dbContext.CourseLocalizations.AnyAsync(
                localization => localization.CourseId == course.Id && localization.Locale == defaultLocale,
                cancellationToken))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.DEFAULT_LOCALIZATION_REQUIRED",
                "The default course localization is required."));
        }

        ResultError? localizationError = await UpdateLocalizationsAsync(course, request.Localizations, cancellationToken);
        if (localizationError is not null)
        {
            return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, localizationError);
        }

        ResultError? taxonomyError = await ReplaceCourseTaxonomyAsync(
            course.Id,
            request.CategoryCodes,
            request.TagCodes,
            cancellationToken);
        if (taxonomyError is not null)
        {
            return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, taxonomyError);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        course.ChangeDefaultLocale(defaultLocale, now);
        draft.UpdateLevel(InfrastructureHelpers.NormalizeLevel(request.Level), request.ExpectedVersion.Value, now);
        ResultError? saveError = await SaveDraftAsync(draft.Id, cancellationToken);
        return saveError is null
            ? Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version))
            : Result.Failure<CourseMutationResponse>(saveError);
    }

    public async Task<Result<CourseMutationResponse>> ArchiveCourseAsync(
        ArchiveCourseCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(InfrastructureHelpers.CourseNotFound());
        }

        try
        {
            course.Archive(request.UserId, request.Reason, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        CourseDraft draft = await dbContext.CourseDrafts.AsNoTracking().SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        InfrastructureHelpers.AddAudit(dbContext, request.UserId, "course.archived", "Course", course.Id, request.Reason, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    public async Task<Result<CourseMutationResponse>> StartNewDraftAsync(
        StartNewDraftCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(InfrastructureHelpers.CourseNotFound());
        }

        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        if (course.Status is CourseStatus.Published or CourseStatus.Unpublished)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            course.StartNewDraft(now);
            draft.Advance(draft.Version, now);
            InfrastructureHelpers.AddAudit(dbContext, request.UserId, "course.draft-started", "Course", course.Id, null, timeProvider);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    public async Task<Result<CourseCollaboratorResponse>> AddCollaboratorAsync(
        AddCollaboratorCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseCollaboratorResponse>(InfrastructureHelpers.CourseNotFound());
        }
        if (request.CollaboratorUserId == course.OwnerUserId)
        {
            return Result.Failure<CourseCollaboratorResponse>(ResultError.BusinessRule(
                "COURSE.OWNER_NOT_COLLABORATOR",
                "The owner cannot also be stored as a collaborator."));
        }
        if (!await dbContext.TeacherProfiles.AnyAsync(
                profile => profile.UserId == request.CollaboratorUserId,
                cancellationToken))
        {
            return Result.Failure<CourseCollaboratorResponse>(ResultError.NotFound(
                "COURSE.COLLABORATOR_NOT_FOUND",
                "The collaborator was not found."));
        }

        CourseCollaboratorRole role = Enum.Parse<CourseCollaboratorRole>(request.Role, false);
        CourseInstructor? collaborator = await dbContext.CourseInstructors.SingleOrDefaultAsync(
            instructor => instructor.CourseId == course.Id && instructor.UserId == request.CollaboratorUserId,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (collaborator is null)
        {
            collaborator = CourseInstructor.Create(course.Id, request.CollaboratorUserId, role, now);
            dbContext.CourseInstructors.Add(collaborator);
        }
        else
        {
            collaborator.ChangeRole(role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CourseCollaboratorResponse(collaborator.UserId, collaborator.Role.ToString(), collaborator.AddedAt));
    }

    public async Task<Result<OperationCompleted>> RemoveCollaboratorAsync(
        RemoveCollaboratorCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<OperationCompleted>(InfrastructureHelpers.CourseNotFound());
        }

        CourseInstructor? collaborator = await dbContext.CourseInstructors.SingleOrDefaultAsync(
            instructor => instructor.CourseId == course.Id && instructor.UserId == request.CollaboratorUserId,
            cancellationToken);
        if (collaborator is null)
        {
            return Result.Failure<OperationCompleted>(ResultError.NotFound(
                "COURSE.COLLABORATOR_NOT_FOUND",
                "The collaborator was not found."));
        }

        dbContext.CourseInstructors.Remove(collaborator);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new OperationCompleted(true));
    }

    public async Task<Result<CourseMutationResponse>> TransferCourseOwnershipAsync(
        TransferCourseOwnershipCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion is null)
        {
            return InfrastructureHelpers.PreconditionRequired<CourseMutationResponse>();
        }
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(InfrastructureHelpers.CourseNotFound());
        }
        if (course.Status is not (CourseStatus.Draft or CourseStatus.ChangesRequested))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.OWNERSHIP_TRANSFER_LOCKED",
                "Course ownership cannot be transferred in the current state."));
        }
        if (!await dbContext.TeacherProfiles.AnyAsync(
                profile => profile.UserId == request.NewOwnerUserId,
                cancellationToken))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.NotFound(
                "COURSE.NEW_OWNER_NOT_FOUND",
                "The new owner was not found."));
        }
        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(
            candidate => candidate.CourseId == course.Id,
            cancellationToken);
        if (draft.Version != request.ExpectedVersion.Value)
        {
            return InfrastructureHelpers.VersionConflict<CourseMutationResponse>(draft.Version);
        }

        Guid oldOwnerUserId = course.OwnerUserId;
        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            course.TransferOwnership(request.NewOwnerUserId, now);
            draft.Advance(request.ExpectedVersion.Value, now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }
        CourseInstructor? newOwnerCollaboration = await dbContext.CourseInstructors.SingleOrDefaultAsync(
            instructor => instructor.CourseId == course.Id && instructor.UserId == request.NewOwnerUserId,
            cancellationToken);
        if (newOwnerCollaboration is not null)
        {
            dbContext.CourseInstructors.Remove(newOwnerCollaboration);
        }

        InfrastructureHelpers.AddAudit(dbContext, oldOwnerUserId, "course.ownership-transferred", "Course", course.Id, null, timeProvider);
        ResultError? saveError = await SaveDraftAsync(draft.Id, cancellationToken);
        return saveError is null
            ? Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version))
            : Result.Failure<CourseMutationResponse>(saveError);
    }

    // ICurriculumService

    public async Task<Result<CurriculumResponse>> GetCurriculumAsync(
        GetCurriculumQuery request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CurriculumResponse>(InfrastructureHelpers.CourseNotFound());
        }

        CourseDraft draft = await dbContext.CourseDrafts.AsNoTracking().SingleAsync(
            candidate => candidate.CourseId == course.Id,
            cancellationToken);
        return Result.Success(await MapCurriculumAsync(draft, cancellationToken));
    }

    public async Task<Result<CourseMutationResponse>> UpdateCurriculumAsync(
        UpdateCurriculumCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion is null)
        {
            return InfrastructureHelpers.PreconditionRequired<CourseMutationResponse>();
        }

        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Edit, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(InfrastructureHelpers.CourseNotFound());
        }
        if (course.Status is not (CourseStatus.Draft or CourseStatus.ChangesRequested))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.DRAFT_LOCKED",
                "The curriculum cannot be changed in the current state."));
        }

        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        if (draft.Version != request.ExpectedVersion.Value)
        {
            return InfrastructureHelpers.VersionConflict<CourseMutationResponse>(draft.Version);
        }

        if (HasDuplicateStableIds(request.Sections))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "CURRICULUM.DUPLICATE_ID",
                "Section and lesson identifiers must be unique within the curriculum."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        draft.Advance(request.ExpectedVersion.Value, now);
        List<CourseSection> existingSections = await dbContext.CourseSections
            .Where(section => section.DraftId == draft.Id)
            .ToListAsync(cancellationToken);
        List<CourseLesson> existingLessons = await dbContext.CourseLessons
            .Where(lesson => lesson.DraftId == draft.Id)
            .ToListAsync(cancellationToken);
        var receivedSectionIds = new HashSet<Guid>();
        var receivedLessonIds = new HashSet<Guid>();
        var pendingSectionPointers = new List<(CourseSection Section, Guid RevisionId, int Position)>();
        var pendingLessonPointers = new List<(CourseLesson Lesson, Guid SectionId, Guid RevisionId, int Position)>();

        foreach (SectionInput input in request.Sections.OrderBy(section => section.Position))
        {
            Guid sectionId = input.Id.GetValueOrDefault();
            CourseSection? section = sectionId == Guid.Empty
                ? null
                : existingSections.SingleOrDefault(candidate => candidate.Id == sectionId);
            bool isNewSection = section is null;
            if (sectionId != Guid.Empty && section is null)
            {
                return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, ResultError.BusinessRule(
                    "CURRICULUM.SECTION_INVALID",
                    "A section identifier does not belong to this course draft."));
            }

            section ??= CourseSection.Create(Guid.Empty, draft.Id, input.Position, now);
            if (section.Id != sectionId && sectionId != Guid.Empty)
            {
                return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, ResultError.BusinessRule(
                    "CURRICULUM.SECTION_INVALID",
                    "A section identifier is invalid."));
            }
            if (!existingSections.Contains(section))
            {
                dbContext.CourseSections.Add(section);
            }

            SectionRevision sectionRevision = SectionRevision.Create(
                section.Id,
                draft.Version,
                input.Title,
                input.Position,
                now);
            dbContext.SectionRevisions.Add(sectionRevision);
            if (isNewSection)
            {
                pendingSectionPointers.Add((section, sectionRevision.Id, input.Position));
            }
            else
            {
                section.ApplyRevision(sectionRevision.Id, input.Position);
            }
            receivedSectionIds.Add(section.Id);

            foreach (LessonInput lessonInput in input.Lessons.OrderBy(lesson => lesson.Position))
            {
                Guid lessonId = lessonInput.Id.GetValueOrDefault();
                CourseLesson? lesson = lessonId == Guid.Empty
                    ? null
                    : existingLessons.SingleOrDefault(candidate => candidate.Id == lessonId);
                bool isNewLesson = lesson is null;
                if (lessonId != Guid.Empty && lesson is null)
                {
                    return InfrastructureHelpers.FailAndClear<CourseMutationResponse>(dbContext, ResultError.BusinessRule(
                        "CURRICULUM.LESSON_INVALID",
                        "A lesson identifier does not belong to this course draft."));
                }

                lesson ??= CourseLesson.Create(Guid.Empty, draft.Id, section.Id, lessonInput.Position, now);
                if (!existingLessons.Contains(lesson))
                {
                    dbContext.CourseLessons.Add(lesson);
                }

                LessonRevision lessonRevision = LessonRevision.Create(
                    lesson.Id,
                    draft.Version,
                    lessonInput.Title,
                    NormalizeLessonType(lessonInput.LessonType),
                    lessonInput.Content,
                    lessonInput.Position,
                    now,
                    lessonInput.MediaAssetId,
                    lessonInput.QuizVersionId,
                    lessonInput.AssignmentVersionId);
                dbContext.LessonRevisions.Add(lessonRevision);
                if (isNewLesson)
                {
                    pendingLessonPointers.Add((lesson, section.Id, lessonRevision.Id, lessonInput.Position));
                }
                else
                {
                    lesson.ApplyRevision(section.Id, lessonRevision.Id, lessonInput.Position);
                }
                receivedLessonIds.Add(lesson.Id);
            }
        }

        foreach (CourseSection section in existingSections.Where(section => !receivedSectionIds.Contains(section.Id)))
        {
            section.Remove(now);
        }
        foreach (CourseLesson lesson in existingLessons.Where(lesson => !receivedLessonIds.Contains(lesson.Id)))
        {
            lesson.Remove(now);
        }

        ResultError? saveError = await SaveDraftAsync(draft.Id, cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<CourseMutationResponse>(saveError);
        }

        foreach ((CourseSection section, Guid revisionId, int position) in pendingSectionPointers)
        {
            section.ApplyRevision(revisionId, position);
        }
        foreach ((CourseLesson lesson, Guid sectionId, Guid revisionId, int position) in pendingLessonPointers)
        {
            lesson.ApplyRevision(sectionId, revisionId, position);
        }
        if (pendingSectionPointers.Count > 0 || pendingLessonPointers.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    // ICourseAccessReader

    public async Task<bool> CanAccessAsync(
        Guid courseId,
        Guid userId,
        CourseAccess access,
        CancellationToken cancellationToken)
    {
        IQueryable<Course> query = dbContext.Courses.AsNoTracking().Where(course =>
            course.Id == courseId && (course.DeletedAt == null || course.OwnerUserId == userId));
        return access switch
        {
            CourseAccess.Owner => await query.AnyAsync(course => course.OwnerUserId == userId, cancellationToken),
            CourseAccess.Edit => await query.AnyAsync(course => course.OwnerUserId == userId ||
                dbContext.CourseInstructors.Any(instructor => instructor.CourseId == course.Id &&
                    instructor.UserId == userId &&
                    (instructor.Role == CourseCollaboratorRole.Editor || instructor.Role == CourseCollaboratorRole.CoInstructor)), cancellationToken),
            _ => await query.AnyAsync(course => course.OwnerUserId == userId ||
                dbContext.CourseInstructors.Any(instructor => instructor.CourseId == course.Id && instructor.UserId == userId), cancellationToken),
        };
    }

    // Private helpers

    private async Task<Course?> FindAccessibleCourseAsync(
        Guid courseId,
        Guid userId,
        CourseAccess access,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(courseId, userId, access, cancellationToken))
        {
            return null;
        }

        return await dbContext.Courses.SingleOrDefaultAsync(course => course.Id == courseId, cancellationToken);
    }

    private async Task<Course?> FindLockedAccessibleCourseAsync(
        Guid courseId,
        Guid userId,
        CourseAccess access,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(courseId, userId, access, cancellationToken))
        {
            return null;
        }

        await LockCourseAsync(courseId, cancellationToken);
        if (!await CanAccessAsync(courseId, userId, access, cancellationToken))
        {
            return null;
        }

        return await dbContext.Courses.SingleOrDefaultAsync(course => course.Id == courseId, cancellationToken);
    }

    private async Task LockCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM catalog.courses WHERE id = {courseId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private async Task LockDraftAsync(Guid courseId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM authoring.course_drafts WHERE course_id = {courseId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private async Task<ResultError?> SaveDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            long currentVersion = await dbContext.CourseDrafts.AsNoTracking()
                .Where(draft => draft.Id == draftId)
                .Select(draft => draft.Version)
                .SingleAsync(cancellationToken);
            return ResultError.PreconditionFailed(
                "COURSE.VERSION_CONFLICT",
                "The course draft was changed by another request.",
                InfrastructureHelpers.ETag(currentVersion));
        }
    }

    private async Task<ResultError?> AddInitialMetadataAsync(
        Course course,
        IReadOnlyList<CourseLocalizationInput> inputs,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (CourseLocalizationInput input in inputs)
        {
            string locale = InfrastructureHelpers.NormalizeLocale(input.Locale);
            string slugValue = input.Slug ?? InfrastructureHelpers.GenerateSlug(input.Title, course.Id);
            if (await dbContext.CourseSlugs.AnyAsync(
                    slug => slug.Locale == locale && slug.Slug == slugValue,
                    cancellationToken))
            {
                return ResultError.Conflict("COURSE.SLUG_EXISTS", "The localized course slug is permanently reserved.");
            }

            CourseSlug slug = CourseSlug.Create(course.Id, locale, slugValue, now);
            dbContext.CourseSlugs.Add(slug);
            dbContext.CourseLocalizations.Add(CourseLocalization.Create(
                course.Id,
                locale,
                input.Title,
                input.Subtitle,
                input.Description,
                slug.Id,
                now));
        }
        return null;
    }

    private async Task<ResultError?> UpdateLocalizationsAsync(
        Course course,
        IReadOnlyList<CourseLocalizationInput> inputs,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<CourseLocalization> existing = await dbContext.CourseLocalizations
            .Where(localization => localization.CourseId == course.Id)
            .ToListAsync(cancellationToken);
        List<CourseSlug> currentSlugs = await dbContext.CourseSlugs
            .Where(slug => slug.CourseId == course.Id && slug.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (CourseLocalizationInput input in inputs)
        {
            string locale = InfrastructureHelpers.NormalizeLocale(input.Locale);
            string desiredSlug = input.Slug ?? InfrastructureHelpers.GenerateSlug(input.Title, course.Id);
            CourseSlug? currentSlug = currentSlugs.SingleOrDefault(slug => slug.Locale == locale);
            CourseLocalization? localization = existing.SingleOrDefault(candidate => candidate.Locale == locale);
            if (currentSlug is null || localization is null)
            {
                if (await dbContext.CourseSlugs.AnyAsync(
                        slug => slug.Locale == locale && slug.Slug == desiredSlug,
                        cancellationToken))
                {
                    return ResultError.Conflict("COURSE.SLUG_EXISTS", "The localized course slug is permanently reserved.");
                }
                CourseSlug createdSlug = CourseSlug.Create(course.Id, locale, desiredSlug, now);
                dbContext.CourseSlugs.Add(createdSlug);
                dbContext.CourseLocalizations.Add(CourseLocalization.Create(
                    course.Id,
                    locale,
                    input.Title,
                    input.Subtitle,
                    input.Description,
                    createdSlug.Id,
                    now));
                continue;
            }

            Guid slugId = currentSlug.Id;
            if (!string.Equals(currentSlug.Slug, desiredSlug, StringComparison.Ordinal))
            {
                if (await dbContext.CourseSlugs.AnyAsync(
                        slug => slug.Locale == locale && slug.Slug == desiredSlug,
                        cancellationToken))
                {
                    return ResultError.Conflict("COURSE.SLUG_EXISTS", "The localized course slug is permanently reserved.");
                }
                currentSlug.Retire(now);
                CourseSlug replacement = CourseSlug.Create(course.Id, locale, desiredSlug, now);
                dbContext.CourseSlugs.Add(replacement);
                slugId = replacement.Id;
            }
            localization.Update(input.Title, input.Subtitle, input.Description, slugId, now);
        }
        return null;
    }

    private async Task<ResultError?> ReplaceCourseTaxonomyAsync(
        Guid courseId,
        IReadOnlyList<string> categoryCodes,
        IReadOnlyList<string> tagCodes,
        CancellationToken cancellationToken)
    {
        string[] categories = categoryCodes.Select(code => code.Trim().ToLowerInvariant()).Distinct().ToArray();
        string[] tags = tagCodes.Select(code => code.Trim().ToLowerInvariant()).Distinct().ToArray();
        Guid[] categoryIds = await dbContext.Categories
            .Where(category => categories.Contains(category.Code) && category.IsActive)
            .Select(category => category.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] tagIds = await dbContext.Tags
            .Where(tag => tags.Contains(tag.Code) && tag.IsActive)
            .Select(tag => tag.Id)
            .ToArrayAsync(cancellationToken);
        if (categoryIds.Length != categories.Length)
        {
            return ResultError.BusinessRule("COURSE.CATEGORY_INVALID", "One or more category codes are unavailable.");
        }
        if (tagIds.Length != tags.Length)
        {
            return ResultError.BusinessRule("COURSE.TAG_INVALID", "One or more tag codes are unavailable.");
        }

        List<CourseCategory> oldCategories = await dbContext.CourseCategories
            .Where(item => item.CourseId == courseId)
            .ToListAsync(cancellationToken);
        List<CourseTag> oldTags = await dbContext.CourseTags
            .Where(item => item.CourseId == courseId)
            .ToListAsync(cancellationToken);
        dbContext.CourseCategories.RemoveRange(oldCategories);
        dbContext.CourseTags.RemoveRange(oldTags);
        dbContext.CourseCategories.AddRange(categoryIds.Select(id => new CourseCategory(courseId, id)));
        dbContext.CourseTags.AddRange(tagIds.Select(id => new CourseTag(courseId, id)));
        return null;
    }

    private async Task<CurriculumResponse> MapCurriculumAsync(CourseDraft draft, CancellationToken cancellationToken)
    {
        List<CourseSection> sections = await dbContext.CourseSections.AsNoTracking()
            .Where(section => section.DraftId == draft.Id && section.RemovedAt == null)
            .OrderBy(section => section.Position)
            .ToListAsync(cancellationToken);
        Guid[] sectionRevisionIds = sections.Where(section => section.CurrentRevisionId.HasValue)
            .Select(section => section.CurrentRevisionId!.Value)
            .ToArray();
        Dictionary<Guid, SectionRevision> sectionRevisions = await dbContext.SectionRevisions.AsNoTracking()
            .Where(revision => sectionRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        List<CourseLesson> lessons = await dbContext.CourseLessons.AsNoTracking()
            .Where(lesson => lesson.DraftId == draft.Id && lesson.RemovedAt == null)
            .OrderBy(lesson => lesson.Position)
            .ToListAsync(cancellationToken);
        Guid[] lessonRevisionIds = lessons.Where(lesson => lesson.CurrentRevisionId.HasValue)
            .Select(lesson => lesson.CurrentRevisionId!.Value)
            .ToArray();
        Dictionary<Guid, LessonRevision> lessonRevisions = await dbContext.LessonRevisions.AsNoTracking()
            .Where(revision => lessonRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        SectionResponse[] response = sections.Select(section =>
        {
            SectionRevision revision = sectionRevisions[section.CurrentRevisionId!.Value];
            LessonResponse[] lessonResponses = lessons.Where(lesson => lesson.SectionId == section.Id).Select(lesson =>
            {
                LessonRevision lessonRevision = lessonRevisions[lesson.CurrentRevisionId!.Value];
                return new LessonResponse(
                    lesson.Id,
                    lesson.Position,
                    lessonRevision.Title,
                    lessonRevision.LessonType,
                    lessonRevision.Content,
                    lessonRevision.MediaAssetId,
                    lessonRevision.QuizVersionId,
                    lessonRevision.AssignmentVersionId);
            }).ToArray();
            return new SectionResponse(section.Id, section.Position, revision.Title, lessonResponses);
        }).ToArray();
        return new CurriculumResponse(draft.Version, response);
    }

    private async Task<IReadOnlyDictionary<Guid, CourseSummaryParts>> LoadCourseSummaryPartsAsync(
        Guid[] courseIds,
        CancellationToken cancellationToken)
    {
        if (courseIds.Length == 0)
        {
            return new Dictionary<Guid, CourseSummaryParts>();
        }
        var draftVersions = await dbContext.CourseDrafts.AsNoTracking()
            .Where(draft => courseIds.Contains(draft.CourseId))
            .ToDictionaryAsync(draft => draft.CourseId, draft => draft.Version, cancellationToken);
        var values = await dbContext.CourseLocalizations.AsNoTracking()
            .Where(localization => courseIds.Contains(localization.CourseId))
            .Join(
                dbContext.CourseSlugs.AsNoTracking(),
                localization => localization.CurrentSlugId,
                slug => slug.Id,
                (localization, slug) => new { localization, slug })
            .ToListAsync(cancellationToken);
        return values.GroupBy(item => item.localization.CourseId).ToDictionary(
            group => group.Key,
            group =>
            {
                var value = group.First();
                return new CourseSummaryParts(
                    draftVersions[group.Key],
                    value.localization.Title,
                    value.slug.Slug);
            });
    }

    private async Task<List<CourseLocalizationResponse>> LoadLocalizationsAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var values = await dbContext.CourseLocalizations.AsNoTracking()
            .Where(localization => localization.CourseId == courseId)
            .Join(
                dbContext.CourseSlugs.AsNoTracking(),
                localization => localization.CurrentSlugId,
                slug => slug.Id,
                (localization, slug) => new
                {
                    localization.Locale,
                    localization.Title,
                    localization.Subtitle,
                    localization.Description,
                    Slug = slug.Slug,
                })
            .OrderBy(localization => localization.Locale)
            .ToListAsync(cancellationToken);
        return values.Select(localization => new CourseLocalizationResponse(
            localization.Locale,
            localization.Title,
            localization.Subtitle,
            localization.Description,
            localization.Slug)).ToList();
    }

    private static CourseSummaryResponse MapSummary(Course course, CourseSummaryParts? parts) => new(
        course.Id,
        course.DefaultLocale,
        course.Status.ToString(),
        parts?.DraftVersion ?? 1,
        course.CreatedAt,
        course.UpdatedAt,
        parts?.Title,
        parts?.Slug);

    private static string NormalizeLessonType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "video" => "Video",
        "article" => "Article",
        "document" => "Document",
        "quiz" => "Quiz",
        "assignment" => "Assignment",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static bool HasDuplicateStableIds(IReadOnlyList<SectionInput> sections)
    {
        Guid[] sectionIds = sections.Where(section => section.Id.HasValue).Select(section => section.Id!.Value).ToArray();
        Guid[] lessonIds = sections.SelectMany(section => section.Lessons)
            .Where(lesson => lesson.Id.HasValue)
            .Select(lesson => lesson.Id!.Value)
            .ToArray();
        return sectionIds.Distinct().Count() != sectionIds.Length || lessonIds.Distinct().Count() != lessonIds.Length;
    }

    private sealed record CourseSummaryParts(long DraftVersion, string Title, string Slug);
}
