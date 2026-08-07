using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Operations;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Catalog;

internal sealed class Phase6Service(
    DorosakDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    CatalogCursorCodec cursorCodec,
    SearchTelemetry searchTelemetry,
    TimeProvider timeProvider) : IPhase6Service, ICourseAccessReader
{
    private const string InvalidCursorCode = "CURSOR.INVALID";

    public async Task<Result<TeacherApplicationResponse>> SubmitTeacherApplicationAsync(
        SubmitTeacherApplicationCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UserId && candidate.IsActive,
            cancellationToken);
        if (user is null || !user.EmailConfirmed)
        {
            return Result.Failure<TeacherApplicationResponse>(ResultError.Forbidden(
                "TEACHER_APPLICATION.EMAIL_VERIFICATION_REQUIRED",
                "A confirmed email address is required before applying."));
        }

        if (await dbContext.TeacherProfiles.AnyAsync(profile => profile.UserId == request.UserId, cancellationToken))
        {
            return Result.Failure<TeacherApplicationResponse>(ResultError.Conflict(
                "TEACHER_APPLICATION.ALREADY_TEACHER",
                "This account already has an approved teacher profile."));
        }

        if (await dbContext.TeacherApplications.AnyAsync(
                application => application.UserId == request.UserId &&
                    (application.Status == TeacherApplicationStatus.Pending ||
                     application.Status == TeacherApplicationStatus.InReview),
                cancellationToken))
        {
            return Result.Failure<TeacherApplicationResponse>(ResultError.Conflict(
                "TEACHER_APPLICATION.ACTIVE_EXISTS",
                "An active teacher application already exists."));
        }

        TeacherApplication application = TeacherApplication.Create(
            request.UserId,
            request.Headline,
            request.Biography,
            request.Expertise,
            request.Motivation,
            timeProvider.GetUtcNow());
        dbContext.TeacherApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(application));
    }

    public async Task<Result<TeacherApplicationResponse>> GetTeacherApplicationAsync(
        GetTeacherApplicationQuery request,
        CancellationToken cancellationToken)
    {
        TeacherApplication? application = await dbContext.TeacherApplications
            .AsNoTracking()
            .Where(candidate => candidate.UserId == request.UserId)
            .OrderByDescending(candidate => candidate.SubmittedAt)
            .ThenByDescending(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return application is null
            ? Result.Failure<TeacherApplicationResponse>(NotFound("TEACHER_APPLICATION.NOT_FOUND", "No teacher application was found."))
            : Result.Success(Map(application));
    }

    public async Task<Result<TeacherApplicationResponse>> WithdrawTeacherApplicationAsync(
        WithdrawTeacherApplicationCommand request,
        CancellationToken cancellationToken)
    {
        await LockTeacherApplicationsAsync(request.UserId, cancellationToken);
        TeacherApplication? application = await dbContext.TeacherApplications
            .Where(candidate => candidate.UserId == request.UserId &&
                (candidate.Status == TeacherApplicationStatus.Pending || candidate.Status == TeacherApplicationStatus.InReview))
            .OrderByDescending(candidate => candidate.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (application is null)
        {
            return Result.Failure<TeacherApplicationResponse>(NotFound(
                "TEACHER_APPLICATION.NOT_FOUND",
                "No active teacher application was found."));
        }

        application.Withdraw(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(application));
    }

    public async Task<Result<PagedResponse<TeacherApplicationResponse>>> GetTeacherApplicationsAsync(
        GetTeacherApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        int limit = NormalizeLimit(request.Limit, 20);
        string canonicalQuery = $"teacher-applications|submitted-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "teacher-applications", canonicalQuery, out DateTimeOffset? after, out Guid? afterId))
        {
            return CursorFailure<PagedResponse<TeacherApplicationResponse>>();
        }

        IQueryable<TeacherApplication> query = dbContext.TeacherApplications.AsNoTracking();
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(application =>
                application.SubmittedAt < timestamp ||
                application.SubmittedAt == timestamp && application.Id.CompareTo(id) < 0);
        }

        List<TeacherApplication> applications = await query
            .OrderByDescending(application => application.SubmittedAt)
            .ThenByDescending(application => application.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        return Result.Success(Page(
            applications,
            limit,
            application => Map(application),
            application => application.SubmittedAt,
            application => application.Id,
            "teacher-applications",
            canonicalQuery));
    }

    public async Task<Result<TeacherApplicationResponse>> ReviewTeacherApplicationAsync(
        ReviewTeacherApplicationCommand request,
        CancellationToken cancellationToken)
    {
        await LockTeacherApplicationAsync(request.ApplicationId, cancellationToken);
        TeacherApplication? application = await dbContext.TeacherApplications.SingleOrDefaultAsync(
            candidate => candidate.Id == request.ApplicationId,
            cancellationToken);
        if (application is null)
        {
            return Result.Failure<TeacherApplicationResponse>(NotFound(
                "TEACHER_APPLICATION.NOT_FOUND",
                "The teacher application was not found."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (request.Decision == "start")
            {
                application.StartReview(request.ReviewerUserId, now);
            }
            else if (request.Decision == "reject")
            {
                application.Reject(request.ReviewerUserId, request.Reason!, now);
            }
            else
            {
                ApplicationUser? user = await dbContext.Users.SingleOrDefaultAsync(
                    candidate => candidate.Id == application.UserId && candidate.IsActive,
                    cancellationToken);
                if (user is null)
                {
                    return Result.Failure<TeacherApplicationResponse>(ResultError.BusinessRule(
                        "TEACHER_APPLICATION.ACCOUNT_UNAVAILABLE",
                        "The applicant account is no longer available."));
                }

                application.Approve(request.ReviewerUserId, now);
                dbContext.TeacherProfiles.Add(TeacherProfile.Create(application, request.ReviewerUserId, now));
                if (!await userManager.IsInRoleAsync(user, Dorosak.Infrastructure.Identity.IdentityConstants.TeacherRole))
                {
                    IdentityResult roleResult = await userManager.AddToRoleAsync(
                        user,
                        Dorosak.Infrastructure.Identity.IdentityConstants.TeacherRole);
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException("The Teacher role could not be assigned.");
                    }
                }

                user.AuthorizationVersion++;
                user.SecurityVersion++;
                user.UpdatedAt = now;
                List<RefreshSession> sessions = await dbContext.RefreshSessions
                    .Where(session => session.UserId == user.Id && session.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (RefreshSession session in sessions)
                {
                    session.Revoke(now, "teacher-role-approved");
                }

                dbContext.SecurityEvents.Add(SecurityEvent.Create(
                    user.Id,
                    null,
                    "teacher.application-approved",
                    now));
            }
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<TeacherApplicationResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        AddAudit(request.ReviewerUserId, $"teacher-application.{request.Decision}", "TeacherApplication", application.Id, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(application));
    }

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

        string defaultLocale = NormalizeLocale(request.DefaultLocale);
        if (!request.Localizations.Any(localization => NormalizeLocale(localization.Locale) == defaultLocale))
        {
            return Result.Failure<CourseMutationResponse>(ResultError.BusinessRule(
                "COURSE.DEFAULT_LOCALIZATION_REQUIRED",
                "The default course localization is required."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Course course = Course.Create(request.UserId, defaultLocale, now);
        CourseDraft draft = CourseDraft.Create(course.Id, NormalizeLevel(request.Level), now);
        dbContext.Courses.Add(course);
        dbContext.CourseDrafts.Add(draft);

        ResultError? metadataError = await AddInitialMetadataAsync(course, request.Localizations, cancellationToken);
        if (metadataError is not null)
        {
            return FailAndClear<CourseMutationResponse>(metadataError);
        }

        ResultError? taxonomyError = await ReplaceCourseTaxonomyAsync(
            course.Id,
            request.CategoryCodes,
            request.TagCodes,
            cancellationToken);
        if (taxonomyError is not null)
        {
            return FailAndClear<CourseMutationResponse>(taxonomyError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    public async Task<Result<PagedResponse<CourseSummaryResponse>>> GetInstructorCoursesAsync(
        GetInstructorCoursesQuery request,
        CancellationToken cancellationToken)
    {
        int limit = NormalizeLimit(request.Limit, 20);
        string canonical = $"instructor-courses|{request.UserId:D}|updated-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "instructor-courses", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return CursorFailure<PagedResponse<CourseSummaryResponse>>();
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
        return Result.Success(Page(
            courses,
            limit,
            course => MapSummary(course, parts.GetValueOrDefault(course.Id)),
            course => course.UpdatedAt,
            course => course.Id,
            "instructor-courses",
            canonical));
    }

    public async Task<Result<CourseDetailsResponse>> GetCourseAsync(
        GetCourseQuery request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseDetailsResponse>(CourseNotFound());
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
            return PreconditionRequired<CourseMutationResponse>();
        }

        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Edit, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(CourseNotFound());
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
            return VersionConflict<CourseMutationResponse>(draft.Version);
        }

        string defaultLocale = NormalizeLocale(request.DefaultLocale);
        if (!request.Localizations.Any(localization => NormalizeLocale(localization.Locale) == defaultLocale) &&
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
            return FailAndClear<CourseMutationResponse>(localizationError);
        }

        ResultError? taxonomyError = await ReplaceCourseTaxonomyAsync(
            course.Id,
            request.CategoryCodes,
            request.TagCodes,
            cancellationToken);
        if (taxonomyError is not null)
        {
            return FailAndClear<CourseMutationResponse>(taxonomyError);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        course.ChangeDefaultLocale(defaultLocale, now);
        draft.UpdateLevel(NormalizeLevel(request.Level), request.ExpectedVersion.Value, now);
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
            return Result.Failure<CourseMutationResponse>(CourseNotFound());
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
        AddAudit(request.UserId, "course.archived", "Course", course.Id, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version));
    }

    public async Task<Result<CurriculumResponse>> GetCurriculumAsync(
        GetCurriculumQuery request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CurriculumResponse>(CourseNotFound());
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
            return PreconditionRequired<CourseMutationResponse>();
        }

        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Edit, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(CourseNotFound());
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
            return VersionConflict<CourseMutationResponse>(draft.Version);
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
                return FailAndClear<CourseMutationResponse>(ResultError.BusinessRule(
                    "CURRICULUM.SECTION_INVALID",
                    "A section identifier does not belong to this course draft."));
            }

            section ??= CourseSection.Create(Guid.Empty, draft.Id, input.Position, now);
            if (section.Id != sectionId && sectionId != Guid.Empty)
            {
                return FailAndClear<CourseMutationResponse>(ResultError.BusinessRule(
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
                    return FailAndClear<CourseMutationResponse>(ResultError.BusinessRule(
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
                    now);
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

    public async Task<Result<CourseCollaboratorResponse>> AddCollaboratorAsync(
        AddCollaboratorCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseCollaboratorResponse>(CourseNotFound());
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
            return Result.Failure<CourseCollaboratorResponse>(NotFound(
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
            return Result.Failure<OperationCompleted>(CourseNotFound());
        }

        CourseInstructor? collaborator = await dbContext.CourseInstructors.SingleOrDefaultAsync(
            instructor => instructor.CourseId == course.Id && instructor.UserId == request.CollaboratorUserId,
            cancellationToken);
        if (collaborator is null)
        {
            return Result.Failure<OperationCompleted>(NotFound(
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
            return PreconditionRequired<CourseMutationResponse>();
        }
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<CourseMutationResponse>(CourseNotFound());
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
            return Result.Failure<CourseMutationResponse>(NotFound(
                "COURSE.NEW_OWNER_NOT_FOUND",
                "The new owner was not found."));
        }
        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(
            candidate => candidate.CourseId == course.Id,
            cancellationToken);
        if (draft.Version != request.ExpectedVersion.Value)
        {
            return VersionConflict<CourseMutationResponse>(draft.Version);
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

        AddAudit(oldOwnerUserId, "course.ownership-transferred", "Course", course.Id, null);
        ResultError? saveError = await SaveDraftAsync(draft.Id, cancellationToken);
        return saveError is null
            ? Result.Success(new CourseMutationResponse(course.Id, course.Status.ToString(), draft.Version))
            : Result.Failure<CourseMutationResponse>(saveError);
    }

    public async Task<Result<PublicationStatusResponse>> RequestPublicationAsync(
        RequestPublicationCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(CourseNotFound());
        }

        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        bool hasDefaultMetadata = await dbContext.CourseLocalizations.AnyAsync(localization =>
            localization.CourseId == course.Id && localization.Locale == course.DefaultLocale &&
            localization.Title != string.Empty && localization.Description != string.Empty,
            cancellationToken);
        bool hasLesson = await dbContext.CourseLessons.AnyAsync(
            lesson => lesson.DraftId == draft.Id && lesson.RemovedAt == null,
            cancellationToken);
        if (!hasDefaultMetadata || !hasLesson)
        {
            return Result.Failure<PublicationStatusResponse>(ResultError.BusinessRule(
                "COURSE.PUBLICATION_INCOMPLETE",
                "Default metadata and at least one curriculum lesson are required."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            course.SubmitForReview(now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<PublicationStatusResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        PublicationReview review = PublicationReview.Create(course.Id, draft.Id, draft.Version, request.UserId, now);
        dbContext.PublicationReviews.Add(review);
        AddAudit(request.UserId, "course.publication-requested", "Course", course.Id, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapPublicationStatus(course, draft, review));
    }

    public async Task<Result<PublicationStatusResponse>> GetPublicationStatusAsync(
        GetPublicationStatusQuery request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(CourseNotFound());
        }

        CourseDraft draft = await dbContext.CourseDrafts.AsNoTracking().SingleAsync(candidate => candidate.CourseId == course.Id, cancellationToken);
        PublicationReview? review = await dbContext.PublicationReviews.AsNoTracking()
            .Where(candidate => candidate.CourseId == course.Id)
            .OrderByDescending(candidate => candidate.RequestedAt)
            .ThenByDescending(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return Result.Success(MapPublicationStatus(course, draft, review));
    }

    public async Task<Result<PublicationStatusResponse>> WithdrawPublicationAsync(
        WithdrawPublicationCommand request,
        CancellationToken cancellationToken)
    {
        Course? course = await FindLockedAccessibleCourseAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(CourseNotFound());
        }

        await LockDraftAsync(course.Id, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(
            candidate => candidate.CourseId == course.Id,
            cancellationToken);
        PublicationReview? pendingReview = await dbContext.PublicationReviews.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.CourseId == course.Id && candidate.Status == PublicationReviewStatus.Pending,
            cancellationToken);
        if (pendingReview is null)
        {
            return Result.Failure<PublicationStatusResponse>(ResultError.BusinessRule(
                "PUBLICATION_REVIEW.NOT_PENDING",
                "The course does not have a pending publication review."));
        }
        await LockPublicationReviewAsync(pendingReview.Id, cancellationToken);
        PublicationReview review = await dbContext.PublicationReviews.SingleAsync(
            candidate => candidate.Id == pendingReview.Id,
            cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            review.Withdraw(now);
            course.WithdrawReview(now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<PublicationStatusResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        AddAudit(request.UserId, "course.publication-withdrawn", "Course", course.Id, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapPublicationStatus(course, draft, review));
    }

    public async Task<Result<PagedResponse<PublicationReviewResponse>>> GetPublicationReviewsAsync(
        GetPublicationReviewsQuery request,
        CancellationToken cancellationToken)
    {
        int limit = NormalizeLimit(request.Limit, 20);
        string canonical = $"publication-reviews|requested-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "publication-reviews", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return CursorFailure<PagedResponse<PublicationReviewResponse>>();
        }

        IQueryable<PublicationReview> query = dbContext.PublicationReviews.AsNoTracking();
        if (after is { } timestamp && afterId is { } id)
        {
            query = query.Where(review => review.RequestedAt < timestamp || review.RequestedAt == timestamp && review.Id.CompareTo(id) < 0);
        }

        List<PublicationReview> reviews = await query
            .OrderByDescending(review => review.RequestedAt)
            .ThenByDescending(review => review.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        return Result.Success(Page(
            reviews,
            limit,
            Map,
            review => review.RequestedAt,
            review => review.Id,
            "publication-reviews",
            canonical));
    }

    public async Task<Result<PublicationReviewResponse>> ReviewPublicationAsync(
        ReviewPublicationCommand request,
        CancellationToken cancellationToken)
    {
        PublicationReview? reviewSnapshot = await dbContext.PublicationReviews.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.ReviewId,
            cancellationToken);
        if (reviewSnapshot is null)
        {
            return Result.Failure<PublicationReviewResponse>(NotFound(
                "PUBLICATION_REVIEW.NOT_FOUND",
                "The publication review was not found."));
        }
        await LockCourseAsync(reviewSnapshot.CourseId, cancellationToken);
        await LockPublicationReviewAsync(reviewSnapshot.Id, cancellationToken);
        await LockDraftAsync(reviewSnapshot.CourseId, cancellationToken);
        PublicationReview review = await dbContext.PublicationReviews.SingleAsync(
            candidate => candidate.Id == request.ReviewId,
            cancellationToken);
        Course course = await dbContext.Courses.SingleAsync(candidate => candidate.Id == review.CourseId, cancellationToken);
        CourseDraft draft = await dbContext.CourseDrafts.SingleAsync(
            candidate => candidate.Id == review.DraftId,
            cancellationToken);
        if (draft.Version != review.DraftVersion)
        {
            return Result.Failure<PublicationReviewResponse>(ResultError.Conflict(
                "PUBLICATION_REVIEW.STALE_DRAFT",
                "The draft changed after this publication review was requested."));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (request.Decision == "approve")
            {
                review.Approve(request.ReviewerUserId, now);
                course.ApproveForPublication(now);
            }
            else
            {
                review.RequestChanges(request.ReviewerUserId, request.Reason!, now);
                course.RequestChanges(now);
            }
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<PublicationReviewResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        AddAudit(request.ReviewerUserId, $"course.review-{request.Decision}", "Course", course.Id, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(review));
    }

    public async Task<Result<PagedResponse<CategoryResponse>>> GetCategoriesAsync(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        string locale = NormalizeLocale(request.Locale);
        int limit = NormalizeLimit(request.Limit, 100);
        string canonical = $"categories|{locale}|display-order|{limit}|all:{request.IncludeInactive}";
        if (!cursorCodec.TryRead(request.Cursor, "categories", canonical, out _, out Guid? afterId, out string? afterKey))
        {
            return CursorFailure<PagedResponse<CategoryResponse>>();
        }

        IQueryable<Category> query = dbContext.Categories.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(category => category.IsActive);
        }
        if (afterId is { } id && int.TryParse(afterKey, NumberStyles.None, CultureInfo.InvariantCulture, out int displayOrder))
        {
            query = query.Where(category =>
                category.DisplayOrder > displayOrder ||
                category.DisplayOrder == displayOrder && category.Id.CompareTo(id) > 0);
        }
        else if (request.Cursor is not null)
        {
            return CursorFailure<PagedResponse<CategoryResponse>>();
        }
        List<Category> categories = await query
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<TaxonomyLocalizationResponse>> localizations = await LoadCategoryLocalizationsAsync(
            categories.Take(limit).Select(category => category.Id).ToArray(),
            cancellationToken);
        bool hasMore = categories.Count > limit;
        List<Category> items = categories.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create(
                "categories",
                canonical,
                null,
                items[^1].Id,
                items[^1].DisplayOrder.ToString(CultureInfo.InvariantCulture))
            : null;
        return Result.Success(new PagedResponse<CategoryResponse>(
            items.Select(category => Map(category, localizations.GetValueOrDefault(category.Id) ?? [])).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<PagedResponse<TagResponse>>> GetTagsAsync(
        GetTagsQuery request,
        CancellationToken cancellationToken)
    {
        string locale = NormalizeLocale(request.Locale);
        int limit = NormalizeLimit(request.Limit, 100);
        string canonical = $"tags|{locale}|code|{limit}|all:{request.IncludeInactive}";
        if (!cursorCodec.TryRead(request.Cursor, "tags", canonical, out _, out Guid? afterId, out string? afterKey))
        {
            return CursorFailure<PagedResponse<TagResponse>>();
        }

        IQueryable<Tag> query = dbContext.Tags.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(tag => tag.IsActive);
        }
        if (afterId is { } id && afterKey is not null)
        {
            query = query.Where(tag =>
                EF.Functions.Collate(tag.Code, "C").CompareTo(afterKey) > 0 ||
                tag.Code == afterKey && tag.Id.CompareTo(id) > 0);
        }
        else if (request.Cursor is not null)
        {
            return CursorFailure<PagedResponse<TagResponse>>();
        }
        List<Tag> tags = await query
            .OrderBy(tag => EF.Functions.Collate(tag.Code, "C"))
            .ThenBy(tag => tag.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<TaxonomyLocalizationResponse>> localizations = await LoadTagLocalizationsAsync(
            tags.Take(limit).Select(tag => tag.Id).ToArray(),
            cancellationToken);
        bool hasMore = tags.Count > limit;
        List<Tag> items = tags.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create("tags", canonical, null, items[^1].Id, items[^1].Code)
            : null;
        return Result.Success(new PagedResponse<TagResponse>(
            items.Select(tag => Map(tag, localizations.GetValueOrDefault(tag.Id) ?? [])).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<CategoryResponse>> UpsertCategoryAsync(
        UpsertCategoryCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Category? category = request.CategoryId is { } id
            ? await dbContext.Categories.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (request.CategoryId is not null && category is null)
        {
            return Result.Failure<CategoryResponse>(NotFound("CATEGORY.NOT_FOUND", "The category was not found."));
        }
        if (category is not null && !string.Equals(category.Code, request.Code, StringComparison.Ordinal))
        {
            return Result.Failure<CategoryResponse>(ResultError.Conflict(
                "CATEGORY.CODE_IMMUTABLE",
                "A category code cannot be changed."));
        }
        if (request.ParentId == request.CategoryId)
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_INVALID",
                "A category cannot be its own parent."));
        }
        Category? parent = request.ParentId is { } parentId
            ? await dbContext.Categories.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == parentId,
                cancellationToken)
            : null;
        if (request.ParentId is not null && parent is null)
        {
            return Result.Failure<CategoryResponse>(NotFound("CATEGORY.PARENT_NOT_FOUND", "The parent category was not found."));
        }
        if (request.IsActive && parent is { IsActive: false })
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_INACTIVE",
                "An active category cannot belong to an inactive parent."));
        }
        if (request.CategoryId is { } existingCategoryId && request.ParentId is { } requestedParentId &&
            await CreatesCategoryCycleAsync(existingCategoryId, requestedParentId, cancellationToken))
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.PARENT_CYCLE",
                "The category parent would create a cycle."));
        }
        if (request.CategoryId is { } categoryIdToDisable && !request.IsActive &&
            await dbContext.Categories.AnyAsync(
                candidate => candidate.ParentId == categoryIdToDisable && candidate.IsActive,
                cancellationToken))
        {
            return Result.Failure<CategoryResponse>(ResultError.BusinessRule(
                "CATEGORY.ACTIVE_CHILDREN",
                "Deactivate or move active child categories first."));
        }

        if (category is null)
        {
            if (await dbContext.Categories.AnyAsync(candidate => candidate.Code == request.Code, cancellationToken))
            {
                return Result.Failure<CategoryResponse>(ResultError.Conflict("CATEGORY.CODE_EXISTS", "The category code already exists."));
            }
            category = Category.Create(request.Code, request.ParentId, request.DisplayOrder, now);
            dbContext.Categories.Add(category);
        }
        else
        {
            category.Update(request.ParentId, request.DisplayOrder, request.IsActive, now);
        }
        if (request.CategoryId is null && !request.IsActive)
        {
            category.Update(request.ParentId, request.DisplayOrder, false, now);
        }

        List<CategoryLocalization> existing = await dbContext.CategoryLocalizations
            .Where(localization => localization.CategoryId == category.Id)
            .ToListAsync(cancellationToken);
        foreach (TaxonomyLocalizationInput input in request.Localizations)
        {
            string locale = NormalizeLocale(input.Locale);
            CategoryLocalization? localization = existing.SingleOrDefault(candidate => candidate.Locale == locale);
            if (localization is null)
            {
                dbContext.CategoryLocalizations.Add(CategoryLocalization.Create(category.Id, locale, input.Name));
            }
            else
            {
                localization.Rename(input.Name);
            }
        }

        AddAudit(request.UserId, "catalog.category-upserted", "Category", category.Id, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(category, request.Localizations.Select(input =>
            new TaxonomyLocalizationResponse(NormalizeLocale(input.Locale), input.Name.Trim())).ToArray()));
    }

    public async Task<Result<TagResponse>> UpsertTagAsync(
        UpsertTagCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Tag? tag = request.TagId is { } id
            ? await dbContext.Tags.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (request.TagId is not null && tag is null)
        {
            return Result.Failure<TagResponse>(NotFound("TAG.NOT_FOUND", "The tag was not found."));
        }
        if (tag is not null && !string.Equals(tag.Code, request.Code, StringComparison.Ordinal))
        {
            return Result.Failure<TagResponse>(ResultError.Conflict("TAG.CODE_IMMUTABLE", "A tag code cannot be changed."));
        }
        if (tag is null)
        {
            if (await dbContext.Tags.AnyAsync(candidate => candidate.Code == request.Code, cancellationToken))
            {
                return Result.Failure<TagResponse>(ResultError.Conflict("TAG.CODE_EXISTS", "The tag code already exists."));
            }
            tag = Tag.Create(request.Code, now);
            dbContext.Tags.Add(tag);
        }
        tag.SetActive(request.IsActive, now);

        List<TagLocalization> existing = await dbContext.TagLocalizations
            .Where(localization => localization.TagId == tag.Id)
            .ToListAsync(cancellationToken);
        foreach (TaxonomyLocalizationInput input in request.Localizations)
        {
            string locale = NormalizeLocale(input.Locale);
            TagLocalization? localization = existing.SingleOrDefault(candidate => candidate.Locale == locale);
            if (localization is null)
            {
                dbContext.TagLocalizations.Add(TagLocalization.Create(tag.Id, locale, input.Name));
            }
            else
            {
                localization.Rename(input.Name);
            }
        }

        AddAudit(request.UserId, "catalog.tag-upserted", "Tag", tag.Id, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(tag, request.Localizations.Select(input =>
            new TaxonomyLocalizationResponse(NormalizeLocale(input.Locale), input.Name.Trim())).ToArray()));
    }

    public Task<Result<PagedResponse<CatalogCourseResponse>>> GetCatalogAsync(
        GetCatalogCoursesQuery request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        string locale = NormalizeLocale(request.Locale);
        string sort = NormalizeCatalogSort(request.Sort);
        int limit = NormalizeLimit(request.Limit, 24);
        string canonical = CanonicalPublicQuery(locale, string.Empty, request.Filters, sort, limit);
        if (!cursorCodec.TryRead(request.Cursor, "catalog", canonical, out _, out _))
        {
            return Task.FromResult(CursorFailure<PagedResponse<CatalogCourseResponse>>());
        }

        // Phase 6 has no CourseRelease/catalog projection by design; drafts are never a fallback.
        return Task.FromResult(Result.Success(new PagedResponse<CatalogCourseResponse>([], null, false)));
    }

    public Task<Result<CatalogCourseResponse>> GetPublicCourseAsync(
        GetPublicCourseQuery request,
        CancellationToken cancellationToken)
    {
        _ = NormalizeLocale(request.Locale);
        _ = cancellationToken;
        return Task.FromResult(Result.Failure<CatalogCourseResponse>(NotFound(
            "CATALOG.COURSE_NOT_FOUND",
            "The published course was not found.")));
    }

    public Task<Result<PagedResponse<SearchCourseResponse>>> SearchAsync(
        SearchCoursesQuery request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        long started = Stopwatch.GetTimestamp();
        string locale = NormalizeLocale(request.Locale);
        string normalizedQuery = SearchTextNormalizer.Normalize(request.Query, locale);
        string sort = NormalizeSearchSort(request.Sort, normalizedQuery.Length == 0);
        int limit = NormalizeLimit(request.Limit, 20);
        string canonical = CanonicalPublicQuery(locale, normalizedQuery, request.Filters, sort, limit);
        if (!cursorCodec.TryRead(request.Cursor, "search", canonical, out _, out _))
        {
            return Task.FromResult(CursorFailure<PagedResponse<SearchCourseResponse>>());
        }

        var response = new PagedResponse<SearchCourseResponse>([], null, false);
        searchTelemetry.Record(
            locale,
            normalizedQuery,
            0,
            Stopwatch.GetElapsedTime(started),
            sort,
            request.Filters);
        return Task.FromResult(Result.Success(response));
    }

    public Task<Result<IReadOnlyList<string>>> SuggestionsAsync(
        SuggestCourseSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        string locale = NormalizeLocale(request.Locale);
        string normalized = SearchTextNormalizer.Normalize(request.Query, locale);
        return Task.FromResult(Result.Success<IReadOnlyList<string>>(normalized.Length < 2 ? [] : []));
    }

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

    private async Task<bool> CreatesCategoryCycleAsync(
        Guid categoryId,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        Guid? current = parentId;
        while (current is { } currentId)
        {
            if (currentId == categoryId)
            {
                return true;
            }

            current = await dbContext.Categories.AsNoTracking()
                .Where(category => category.Id == currentId)
                .Select(category => category.ParentId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return false;
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

    private async Task LockPublicationReviewAsync(Guid reviewId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM authoring.publication_reviews WHERE id = {reviewId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private async Task LockTeacherApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM profiles.teacher_applications WHERE id = {applicationId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private async Task LockTeacherApplicationsAsync(Guid userId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT count(*)::int AS \"Value\" FROM (SELECT 1 FROM profiles.teacher_applications WHERE user_id = {userId} AND status IN ('Pending', 'InReview') FOR UPDATE) AS active_applications")
            .SingleAsync(cancellationToken);

    private async Task<ResultError?> AddInitialMetadataAsync(
        Course course,
        IReadOnlyList<CourseLocalizationInput> inputs,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (CourseLocalizationInput input in inputs)
        {
            string locale = NormalizeLocale(input.Locale);
            string slugValue = input.Slug ?? GenerateSlug(input.Title, course.Id);
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
            string locale = NormalizeLocale(input.Locale);
            string desiredSlug = input.Slug ?? GenerateSlug(input.Title, course.Id);
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
                ETag(currentVersion));
        }
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
                    lessonRevision.Content);
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

    private async Task<Dictionary<Guid, List<TaxonomyLocalizationResponse>>> LoadCategoryLocalizationsAsync(
        Guid[] ids,
        CancellationToken cancellationToken) =>
        (await dbContext.CategoryLocalizations.AsNoTracking()
            .Where(localization => ids.Contains(localization.CategoryId))
            .OrderBy(localization => localization.Locale)
            .ToListAsync(cancellationToken))
        .GroupBy(localization => localization.CategoryId)
        .ToDictionary(
            group => group.Key,
            group => group.Select(localization => new TaxonomyLocalizationResponse(localization.Locale, localization.Name)).ToList());

    private async Task<Dictionary<Guid, List<TaxonomyLocalizationResponse>>> LoadTagLocalizationsAsync(
        Guid[] ids,
        CancellationToken cancellationToken) =>
        (await dbContext.TagLocalizations.AsNoTracking()
            .Where(localization => ids.Contains(localization.TagId))
            .OrderBy(localization => localization.Locale)
            .ToListAsync(cancellationToken))
        .GroupBy(localization => localization.TagId)
        .ToDictionary(
            group => group.Key,
            group => group.Select(localization => new TaxonomyLocalizationResponse(localization.Locale, localization.Name)).ToList());

    private PagedResponse<TResponse> Page<TEntity, TResponse>(
        List<TEntity> source,
        int limit,
        Func<TEntity, TResponse> map,
        Func<TEntity, DateTimeOffset> date,
        Func<TEntity, Guid> id,
        string scope,
        string canonical)
    {
        bool hasMore = source.Count > limit;
        List<TEntity> items = source.Take(limit).ToList();
        string? nextCursor = hasMore
            ? cursorCodec.Create(scope, canonical, date(items[^1]), id(items[^1]))
            : null;
        return new PagedResponse<TResponse>(items.Select(map).ToArray(), nextCursor, hasMore);
    }

    private void AddAudit(Guid actorUserId, string action, string targetType, Guid targetId, string? reason) =>
        dbContext.AuditLogs.Add(AuditLog.Create(
            actorUserId,
            action,
            targetType,
            targetId,
            "Succeeded",
            reason,
            timeProvider.GetUtcNow()));

    private Result<T> FailAndClear<T>(ResultError error)
    {
        dbContext.ChangeTracker.Clear();
        return Result.Failure<T>(error);
    }

    private static TeacherApplicationResponse Map(TeacherApplication application) => new(
        application.Id,
        application.UserId,
        application.Headline,
        application.Biography,
        application.Expertise,
        application.Motivation,
        application.Status.ToString(),
        application.ReviewerReason,
        application.SubmittedAt,
        application.UpdatedAt);

    private static PublicationReviewResponse Map(PublicationReview review) => new(
        review.Id,
        review.CourseId,
        review.DraftId,
        review.DraftVersion,
        review.RequestedByUserId,
        review.Status.ToString(),
        review.ReviewerReason,
        review.RequestedAt,
        review.UpdatedAt);

    private static CategoryResponse Map(Category category, IReadOnlyList<TaxonomyLocalizationResponse> localizations) => new(
        category.Id,
        category.Code,
        category.ParentId,
        category.DisplayOrder,
        category.IsActive,
        localizations);

    private static TagResponse Map(Tag tag, IReadOnlyList<TaxonomyLocalizationResponse> localizations) => new(
        tag.Id,
        tag.Code,
        tag.IsActive,
        localizations);

    private static CourseSummaryResponse MapSummary(Course course, CourseSummaryParts? parts) => new(
        course.Id,
        course.DefaultLocale,
        course.Status.ToString(),
        parts?.DraftVersion ?? 1,
        course.CreatedAt,
        course.UpdatedAt,
        parts?.Title,
        parts?.Slug);

    private static PublicationStatusResponse MapPublicationStatus(
        Course course,
        CourseDraft draft,
        PublicationReview? review) => new(
            course.Id,
            course.Status.ToString(),
            review?.Id,
            review?.Status.ToString(),
            review?.ReviewerReason,
            draft.Version);

    private static Result<T> CursorFailure<T>() => Result.Failure<T>(ResultError.BusinessRule(
        InvalidCursorCode,
        "The cursor is invalid or does not match this query."));

    private static Result<T> VersionConflict<T>(long currentVersion) => Result.Failure<T>(ResultError.PreconditionFailed(
        "COURSE.VERSION_CONFLICT",
        "The course draft was changed by another request.",
        ETag(currentVersion)));

    private static Result<T> PreconditionRequired<T>() => Result.Failure<T>(ResultError.PreconditionRequired(
        "COURSE.IF_MATCH_REQUIRED",
        "The If-Match precondition is required."));

    private static ResultError CourseNotFound() => NotFound(
        "COURSE.NOT_FOUND",
        "The course was not found or is not available to this account.");

    private static ResultError NotFound(string code, string description) => ResultError.NotFound(code, description);

    private static string ETag(long version) => $"\"v{version.ToString(CultureInfo.InvariantCulture)}\"";

    private static int NormalizeLimit(int limit, int defaultValue) => limit <= 0 ? defaultValue : Math.Min(limit, 100);

    private static string NormalizeLocale(string locale) => locale.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Only ar and en are supported."),
    };

    private static string NormalizeLevel(string level) => level.Trim().ToLowerInvariant() switch
    {
        "beginner" => "Beginner",
        "intermediate" => "Intermediate",
        "advanced" => "Advanced",
        "alllevels" => "AllLevels",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static string NormalizeLessonType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "video" => "Video",
        "article" => "Article",
        "document" => "Document",
        "quiz" => "Quiz",
        "assignment" => "Assignment",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static string NormalizeCatalogSort(string sort) => sort.Trim().ToLowerInvariant() switch
    {
        "" or "newest" => "newest",
        "title" => "title",
        "popular" => "popular",
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    private static string NormalizeSearchSort(string sort, bool blankQuery) => sort.Trim().ToLowerInvariant() switch
    {
        "" when blankQuery => "newest",
        "" => "relevance",
        "relevance" when !blankQuery => "relevance",
        "newest" => "newest",
        "title" => "title",
        "popular" => "popular",
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    private static string CanonicalPublicQuery(
        string locale,
        string query,
        CatalogFilterContract filters,
        string sort,
        int limit) => string.Join('|',
            "v1",
            locale,
            query,
            filters.CategoryCode?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Tag?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Language?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Level?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Price?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Duration?.Trim().ToLowerInvariant() ?? string.Empty,
            filters.Instructor?.Trim().ToLowerInvariant() ?? string.Empty,
            sort,
            limit.ToString(CultureInfo.InvariantCulture));

    private static bool HasDuplicateStableIds(IReadOnlyList<SectionInput> sections)
    {
        Guid[] sectionIds = sections.Where(section => section.Id.HasValue).Select(section => section.Id!.Value).ToArray();
        Guid[] lessonIds = sections.SelectMany(section => section.Lessons)
            .Where(lesson => lesson.Id.HasValue)
            .Select(lesson => lesson.Id!.Value)
            .ToArray();
        return sectionIds.Distinct().Count() != sectionIds.Length || lessonIds.Distinct().Count() != lessonIds.Length;
    }

    private static string GenerateSlug(string title, Guid courseId)
    {
        string normalized = title.Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        bool containedLatin = false;
        foreach (char character in normalized)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                containedLatin = containedLatin || char.IsAsciiLetter(character);
            }
            else if (TryTransliterateArabic(character, out string? transliteration))
            {
                builder.Append(transliteration);
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                builder.Append('-');
            }
        }

        string value = CollapseHyphens(builder.ToString()).Trim('-');
        if (value.Length == 0)
        {
            value = "course";
        }
        if (!containedLatin)
        {
            string suffix = Convert.ToHexString(SHA256.HashData(courseId.ToByteArray()))[..8].ToLowerInvariant();
            value = $"{value}-{suffix}";
        }
        return value.Length <= 160 ? value : value[..160].TrimEnd('-');
    }

    private static string CollapseHyphens(string value)
    {
        while (value.Contains("--", StringComparison.Ordinal))
        {
            value = value.Replace("--", "-", StringComparison.Ordinal);
        }
        return value;
    }

    private static bool TryTransliterateArabic(char character, out string? value)
    {
        value = character switch
        {
            'ا' or 'أ' or 'إ' or 'آ' => "a",
            'ب' => "b",
            'ت' or 'ة' => "t",
            'ث' => "th",
            'ج' => "j",
            'ح' => "h",
            'خ' => "kh",
            'د' => "d",
            'ذ' => "dh",
            'ر' => "r",
            'ز' => "z",
            'س' => "s",
            'ش' => "sh",
            'ص' => "s",
            'ض' => "d",
            'ط' => "t",
            'ظ' => "z",
            'ع' => "a",
            'غ' => "gh",
            'ف' => "f",
            'ق' => "q",
            'ك' => "k",
            'ل' => "l",
            'م' => "m",
            'ن' => "n",
            'ه' => "h",
            'و' or 'ؤ' => "w",
            'ي' or 'ى' or 'ئ' => "y",
            'ء' => string.Empty,
            _ => null,
        };
        return value is not null;
    }

    private sealed record CourseSummaryParts(long DraftVersion, string Title, string Slug);
}
