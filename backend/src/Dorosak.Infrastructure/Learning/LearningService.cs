using System.Text.Json;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Learning;
using Dorosak.Domain.Assessment;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Learning;

internal sealed class LearningService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider) : ILearningService
{
    public async Task<Result<EnrollmentResponse>> EnrollAsync(
        EnrollCourseCommand request,
        CancellationToken cancellationToken)
    {
        await LockCourseAsync(request.CourseId, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        Course? course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.CourseId &&
                candidate.DeletedAt == null &&
                candidate.Status == CourseStatus.Published &&
                candidate.ActiveReleaseId != null,
            cancellationToken);
        if (course is null || course.ActiveReleaseId is not { } releaseId)
        {
            return Failure<EnrollmentResponse>("COURSE.NOT_PUBLISHED", "The course is not available for enrollment.", notFound: true);
        }

        Enrollment? current = await dbContext.Enrollments.SingleOrDefaultAsync(
            enrollment => enrollment.UserId == request.UserId &&
                enrollment.CourseId == request.CourseId &&
                (enrollment.Status == EnrollmentStatus.Active ||
                    enrollment.Status == EnrollmentStatus.Completed ||
                    enrollment.Status == EnrollmentStatus.Suspended),
            cancellationToken);
        if (current is not null)
        {
            return Result.Success(await MapEnrollmentAsync(current, request.Locale, cancellationToken));
        }

        Entitlement? entitlement = await dbContext.Entitlements.SingleOrDefaultAsync(
            item => item.UserId == request.UserId && item.CourseId == request.CourseId &&
                item.Status == EntitlementStatus.Active &&
                (item.ExpiresAt == null || item.ExpiresAt > now),
            cancellationToken);
        if (entitlement is null)
        {
            return Failure<EnrollmentResponse>(
                "ENROLLMENT.ENTITLEMENT_REQUIRED",
                "Complete the demo checkout before enrolling in this course.");
        }

        Enrollment enrollment = Enrollment.Create(
            request.UserId,
            request.CourseId,
            releaseId,
            entitlement.Id,
            now);
        dbContext.Enrollments.Add(enrollment);
        AddAudit(request.UserId, "learning.enrolled", "Enrollment", enrollment.Id, request.CourseId.ToString("D"), now);
        return Result.Success(await MapEnrollmentAsync(enrollment, request.Locale, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<EnrollmentResponse>>> GetEnrollmentsAsync(
        GetEnrollmentsQuery request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var enrollmentRows = await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join entitlement in dbContext.Entitlements.AsNoTracking() on enrollment.EntitlementId equals entitlement.Id
            join release in dbContext.CourseReleases.AsNoTracking() on enrollment.ReleaseId equals release.Id
            where enrollment.UserId == request.UserId &&
                enrollment.Status != EnrollmentStatus.Revoked &&
                enrollment.Status != EnrollmentStatus.Expired &&
                entitlement.Status == EntitlementStatus.Active &&
                (entitlement.ExpiresAt == null || entitlement.ExpiresAt > now)
            orderby enrollment.LastAccessedAt descending, enrollment.Id descending
            select new { Enrollment = enrollment, release.DefaultLocale })
            .ToListAsync(cancellationToken);
        Guid[] releaseIds = enrollmentRows.Select(row => row.Enrollment.ReleaseId).Distinct().ToArray();
        List<CourseReleaseLocalization> localizations = await dbContext.CourseReleaseLocalizations.AsNoTracking()
            .Where(localization => releaseIds.Contains(localization.ReleaseId))
            .ToListAsync(cancellationToken);
        ILookup<Guid, CourseReleaseLocalization> localizationsByRelease = localizations.ToLookup(item => item.ReleaseId);
        EnrollmentResponse[] rows = enrollmentRows.Select(row =>
        {
            CourseReleaseLocalization? localization = localizationsByRelease[row.Enrollment.ReleaseId]
                .OrderBy(item => item.Locale == request.Locale ? 0 : item.Locale == row.DefaultLocale ? 1 : 2)
                .ThenBy(item => item.Locale)
                .FirstOrDefault();
            return new EnrollmentResponse(
                row.Enrollment.Id,
                row.Enrollment.CourseId,
                row.Enrollment.ReleaseId,
                row.Enrollment.Status.ToString(),
                row.Enrollment.EnrolledAt,
                localization?.Title ?? string.Empty,
                localization?.Slug ?? string.Empty);
        }).ToArray();
        return Result.Success<IReadOnlyList<EnrollmentResponse>>(rows);
    }

    public async Task<Result<LearningManifestResponse>> GetManifestAsync(
        GetLearningManifestQuery request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken);
        if (enrollment is null || release is null)
        {
            return Failure<LearningManifestResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }

        CourseReleaseLocalization? localization = await FindLocalizationAsync(release.Id, request.Locale, cancellationToken);
        List<CourseReleaseSection> sections = await dbContext.CourseReleaseSections.AsNoTracking()
            .Where(section => section.ReleaseId == release.Id)
            .OrderBy(section => section.Position)
            .ToListAsync(cancellationToken);
        List<CourseReleaseLesson> lessons = await dbContext.CourseReleaseLessons.AsNoTracking()
            .Where(lesson => lesson.ReleaseId == release.Id)
            .OrderBy(lesson => lesson.Position)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, LessonProgress> progress = await dbContext.LessonProgress.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollment.Id)
            .ToDictionaryAsync(item => item.LessonId, cancellationToken);
        List<CourseReleaseAssessment> accessibleAssessments = await AccessibleAssessments(enrollment)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, CourseReleaseAssessment> assessments = accessibleAssessments
            .ToDictionary(item => item.LessonId);

        LearningSectionResponse[] mappedSections = sections.Select(section =>
        {
            CourseReleaseLesson[] sectionLessons = lessons
                .Where(lesson => lesson.SectionId == section.Id)
                .OrderBy(lesson => lesson.Position)
                .ToArray();
            return new LearningSectionResponse(
                section.Id,
                section.Position,
                section.Title,
                sectionLessons.Select(lesson => MapLessonSummary(lesson, progress.GetValueOrDefault(lesson.Id), assessments.GetValueOrDefault(lesson.Id))).ToArray());
        }).ToArray();
        Guid? nextLessonId = lessons
            .Where(lesson => !progress.TryGetValue(lesson.Id, out LessonProgress? item) || !item.IsCompleted)
            .Select(lesson => (Guid?)lesson.Id)
            .FirstOrDefault();
        return Result.Success(new LearningManifestResponse(
            enrollment.Id,
            enrollment.CourseId,
            release.Id,
            enrollment.Status.ToString(),
            localization?.Locale ?? request.Locale,
            localization?.Title ?? string.Empty,
            localization?.Slug ?? string.Empty,
            mappedSections,
            nextLessonId));
    }

    public async Task<Result<LearningLessonResponse>> GetLessonAsync(
        GetLearningLessonQuery request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken);
        CourseReleaseLesson? lesson = release is null
            ? null
            : await dbContext.CourseReleaseLessons.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == request.LessonId && item.ReleaseId == release.Id,
                cancellationToken);
        if (enrollment is null || release is null || lesson is null)
        {
            return Failure<LearningLessonResponse>("LEARNING.LESSON_NOT_FOUND", "The learning resource was not found.", notFound: true);
        }

        LessonProgress? progress = await dbContext.LessonProgress.AsNoTracking().SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollment.Id && item.LessonId == lesson.Id,
            cancellationToken);
        CourseReleaseAssessment? assessment = await AccessibleAssessments(enrollment).SingleOrDefaultAsync(
            item => item.LessonId == lesson.Id,
            cancellationToken);
        List<CourseReleaseMediaVariant> variants = await dbContext.CourseReleaseMediaVariants.AsNoTracking()
            .Where(item => item.ReleaseId == release.Id && item.LessonId == lesson.Id)
            .OrderBy(item => item.Kind)
            .ToListAsync(cancellationToken);
        List<CourseReleaseCaption> captions = await dbContext.CourseReleaseCaptions.AsNoTracking()
            .Where(item => item.ReleaseId == release.Id && item.LessonId == lesson.Id)
            .OrderBy(item => item.Locale)
            .ToListAsync(cancellationToken);
        return Result.Success(new LearningLessonResponse(
            enrollment.Id,
            release.Id,
            lesson.Id,
            lesson.SectionId,
            lesson.Position,
            lesson.Title,
            lesson.LessonType,
            lesson.Content,
            lesson.CompletionRequirement,
            progress?.IsCompleted ?? false,
            progress?.PositionSeconds ?? 0,
            variants.Select(item => new LearningMediaVariantResponse(
                item.AssetId,
                item.VariantId,
                item.Kind,
                item.ContentType,
                item.Bytes,
                item.Width,
                item.Height,
                item.DurationSeconds)).ToArray(),
            captions.Select(item => new LearningCaptionResponse(
                item.AssetId,
                item.CaptionId,
                item.Locale,
                item.Label)).ToArray(),
            assessment?.QuizVersionId,
            assessment?.AssignmentVersionId));
    }

    public async Task<Result<IReadOnlyList<CourseLearnerResponse>>> GetCourseLearnersAsync(
        GetCourseLearnersQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken))
        {
            return Failure<IReadOnlyList<CourseLearnerResponse>>(
                "LEARNING.COURSE_NOT_FOUND",
                "The course was not found.",
                notFound: true);
        }

        var rows = await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on enrollment.UserId equals user.Id
            where enrollment.CourseId == request.CourseId &&
                enrollment.Status != EnrollmentStatus.Revoked &&
                enrollment.Status != EnrollmentStatus.Expired
            orderby user.DisplayName, user.Id, enrollment.EnrolledAt descending
            select new
            {
                enrollment.UserId,
                user.DisplayName,
                EnrollmentId = enrollment.Id,
                enrollment.ReleaseId,
                Status = enrollment.Status.ToString(),
                enrollment.EnrolledAt,
            }).ToListAsync(cancellationToken);

        CourseLearnerResponse[] learners = rows
            .GroupBy(row => new { row.UserId, row.DisplayName })
            .Select(group => new CourseLearnerResponse(
                group.Key.UserId,
                group.Key.DisplayName,
                group.Select(row => new CourseLearnerEnrollmentResponse(
                    row.EnrollmentId,
                    row.ReleaseId,
                    row.Status,
                    row.EnrolledAt)).ToArray()))
            .ToArray();
        return Result.Success<IReadOnlyList<CourseLearnerResponse>>(learners);
    }

    public async Task<Result<ProgressResponse>> UpdateProgressAsync(
        UpdateLessonProgressCommand request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken,
            lockEnrollment: true);
        CourseReleaseLesson? lesson = release is null
            ? null
            : await dbContext.CourseReleaseLessons.SingleOrDefaultAsync(
                item => item.Id == request.LessonId && item.ReleaseId == release.Id,
                cancellationToken);
        if (enrollment is null || release is null || lesson is null)
        {
            return Failure<ProgressResponse>("LEARNING.LESSON_NOT_FOUND", "The learning resource was not found.", notFound: true);
        }

        LessonProgress? progress = await dbContext.LessonProgress.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollment.Id && item.LessonId == lesson.Id,
            cancellationToken);
        bool isNewProgress = progress is null;
        progress ??= LessonProgress.Create(enrollment.Id, lesson.Id, timeProvider.GetUtcNow());
        if (isNewProgress)
        {
            dbContext.LessonProgress.Add(progress);
        }
        decimal? duration = await dbContext.CourseReleaseMediaVariants.AsNoTracking()
            .Where(item => item.ReleaseId == release.Id && item.LessonId == lesson.Id)
            .Select(item => item.DurationSeconds)
            .Where(item => item != null)
            .MaxAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool applied;
        try
        {
            applied = progress.Apply(
                request.ClientCommandId,
                request.Sequence,
                request.PositionSeconds,
                request.WatchedIntervals.Select(item => new WatchedInterval(item.StartSeconds, item.EndSeconds)).ToArray(),
                request.CompletionIntent,
                lesson.LessonType,
                duration,
                now);
        }
        catch (DomainRuleException exception)
        {
            return Failure<ProgressResponse>(exception.Code, exception.Message);
        }
        if (applied)
        {
            await CompleteCourseIfReadyAsync(enrollment, release.Id, now, cancellationToken);
            enrollment.Touch(now);
            AddAudit(request.UserId, "learning.progress-updated", "Enrollment", enrollment.Id, request.LessonId.ToString("D"), now);
        }
        return Result.Success(new ProgressResponse(
            enrollment.Id,
            lesson.Id,
            progress.LastSequence,
            progress.PositionSeconds,
            progress.IsCompleted,
            progress.CompletedAt,
            applied));
    }

    public async Task<Result<IReadOnlyList<LearningNoteResponse>>> GetNotesAsync(
        GetLearningNotesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<IReadOnlyList<LearningNoteResponse>>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        LearningNoteResponse[] notes = await dbContext.LearningNotes.AsNoTracking()
            .Where(note => note.UserId == request.UserId && note.EnrollmentId == request.EnrollmentId && note.LessonId == request.LessonId)
            .OrderByDescending(note => note.UpdatedAt)
            .Select(note => new LearningNoteResponse(note.Id, note.EnrollmentId, note.LessonId, note.Text, note.CreatedAt, note.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyList<LearningNoteResponse>>(notes);
    }

    public async Task<Result<LearningNoteResponse>> UpsertNoteAsync(
        UpsertLearningNoteCommand request,
        CancellationToken cancellationToken)
    {
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<LearningNoteResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        LearningNote? note = request.NoteId is { } noteId
            ? await dbContext.LearningNotes.SingleOrDefaultAsync(item => item.Id == noteId && item.UserId == request.UserId && item.EnrollmentId == request.EnrollmentId && item.LessonId == request.LessonId, cancellationToken)
            : null;
        if (request.NoteId is not null && note is null)
        {
            return Failure<LearningNoteResponse>("LEARNING.NOTE_NOT_FOUND", "The note was not found.", notFound: true);
        }
        if (note is null)
        {
            note = LearningNote.Create(request.UserId, request.EnrollmentId, request.LessonId, request.Text, now);
            dbContext.LearningNotes.Add(note);
        }
        else
        {
            note.Update(request.Text, now);
        }
        return Result.Success(new LearningNoteResponse(note.Id, note.EnrollmentId, note.LessonId, note.Text, note.CreatedAt, note.UpdatedAt));
    }

    public async Task<Result<LearningOperationResponse>> DeleteNoteAsync(
        DeleteLearningNoteCommand request,
        CancellationToken cancellationToken)
    {
        LearningNote? note = await dbContext.LearningNotes.SingleOrDefaultAsync(
            item => item.Id == request.NoteId && item.UserId == request.UserId && item.EnrollmentId == request.EnrollmentId && item.LessonId == request.LessonId,
            cancellationToken);
        if (note is null)
        {
            return Failure<LearningOperationResponse>("LEARNING.NOTE_NOT_FOUND", "The note was not found.", notFound: true);
        }
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<LearningOperationResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        dbContext.LearningNotes.Remove(note);
        return Result.Success(new LearningOperationResponse(true));
    }

    public async Task<Result<BookmarkResponse>> AddBookmarkAsync(
        AddBookmarkCommand request,
        CancellationToken cancellationToken)
    {
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<BookmarkResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        Bookmark? bookmark = await dbContext.Bookmarks.SingleOrDefaultAsync(
            item => item.UserId == request.UserId && item.EnrollmentId == request.EnrollmentId && item.LessonId == request.LessonId,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (bookmark is null)
        {
            bookmark = Bookmark.Create(request.UserId, request.EnrollmentId, request.LessonId, now);
            dbContext.Bookmarks.Add(bookmark);
        }
        return Result.Success(new BookmarkResponse(bookmark.EnrollmentId, bookmark.LessonId, bookmark.CreatedAt));
    }

    public async Task<Result<LearningOperationResponse>> DeleteBookmarkAsync(
        DeleteBookmarkCommand request,
        CancellationToken cancellationToken)
    {
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<LearningOperationResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        Bookmark? bookmark = await dbContext.Bookmarks.SingleOrDefaultAsync(
            item => item.UserId == request.UserId && item.EnrollmentId == request.EnrollmentId && item.LessonId == request.LessonId,
            cancellationToken);
        if (bookmark is not null)
        {
            dbContext.Bookmarks.Remove(bookmark);
        }
        return Result.Success(new LearningOperationResponse(true));
    }

    public async Task<Result<LearningOperationResponse>> MarkRecentlyViewedAsync(
        MarkRecentlyViewedCommand request,
        CancellationToken cancellationToken)
    {
        if (!await HasLessonAccessAsync(request.UserId, request.EnrollmentId, request.LessonId, cancellationToken))
        {
            return Failure<LearningOperationResponse>("LEARNING.NOT_FOUND", "The learning resource was not found.", notFound: true);
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        RecentlyViewedLesson? item = await dbContext.RecentlyViewedLessons.SingleOrDefaultAsync(
            candidate => candidate.UserId == request.UserId && candidate.EnrollmentId == request.EnrollmentId && candidate.LessonId == request.LessonId,
            cancellationToken);
        if (item is null)
        {
            dbContext.RecentlyViewedLessons.Add(RecentlyViewedLesson.Create(request.UserId, request.EnrollmentId, request.LessonId, now));
        }
        else
        {
            item.Touch(now);
        }
        return Result.Success(new LearningOperationResponse(true));
    }

    public async Task<Result<QuizVersionResponse>> CreateQuizVersionAsync(
        CreateQuizVersionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken) ||
            !await CourseLessonExistsAsync(request.CourseId, request.LessonId, "Quiz", cancellationToken))
        {
            return Failure<QuizVersionResponse>("ASSESSMENT.NOT_FOUND", "The assessment resource was not found.", notFound: true);
        }
        if (!Enum.TryParse(request.AudienceType, true, out AssessmentAudienceType audienceType) ||
            !await ValidateAudienceAsync(request.CourseId, audienceType, request.SelectedLearnerUserIds, cancellationToken))
        {
            return Failure<QuizVersionResponse>("ASSESSMENT.AUDIENCE_INVALID", "The selected assessment audience is invalid.");
        }
        var parsedQuestions = new List<(QuizQuestionInput Input, QuizQuestionType Type)>(request.Questions.Count);
        foreach (QuizQuestionInput input in request.Questions.OrderBy(item => item.Position))
        {
            if (!Enum.TryParse(input.Type, true, out QuizQuestionType type))
            {
                return Failure<QuizVersionResponse>("QUIZ.QUESTION_TYPE_INVALID", "The quiz question type is invalid.");
            }
            int correctCount = input.Options.Count(option => option.IsCorrect);
            bool optionsValid = type switch
            {
                QuizQuestionType.ShortAnswer => input.Options.Count == 0,
                QuizQuestionType.SingleChoice => input.Options.Count >= 2 && correctCount == 1,
                QuizQuestionType.MultipleChoice => input.Options.Count >= 2 && correctCount >= 1,
                QuizQuestionType.TrueFalse => input.Options.Count == 2 && correctCount == 1,
                _ => false,
            };
            if (!optionsValid)
            {
                return Failure<QuizVersionResponse>("QUIZ.OPTIONS_INVALID", "The question options do not match the question type.");
            }
            if (type != QuizQuestionType.ShortAnswer && !string.IsNullOrWhiteSpace(input.AcceptedAnswer))
            {
                return Failure<QuizVersionResponse>("QUIZ.ACCEPTED_ANSWER_INVALID", "Accepted text answers are only valid for short-answer questions.");
            }
            parsedQuestions.Add((input, type));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        Quiz? quiz = await dbContext.Quizzes.SingleOrDefaultAsync(
            item => item.CourseId == request.CourseId && item.LessonId == request.LessonId,
            cancellationToken);
        bool addQuiz = quiz is null;
        quiz ??= Quiz.Create(request.CourseId, request.LessonId, request.ActorUserId, now);
        int versionNumber = (await dbContext.QuizVersions.Where(item => item.QuizId == quiz.Id).Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        QuizVersion version;
        try
        {
            version = QuizVersion.Create(quiz.Id, versionNumber, request.Title, request.AttemptLimit, request.DurationMinutes, request.Deadline, request.PassScore, audienceType, now);
        }
        catch (DomainRuleException exception)
        {
            return Failure<QuizVersionResponse>(exception.Code, exception.Message);
        }
        var questions = new List<QuizQuestion>(parsedQuestions.Count);
        var questionOptions = new List<QuizQuestionOption>();
        foreach ((QuizQuestionInput input, QuizQuestionType type) in parsedQuestions)
        {
            QuizQuestion question;
            try
            {
                question = QuizQuestion.Create(version.Id, input.Position, type, input.Prompt, input.Points, input.AcceptedAnswer);
            }
            catch (DomainRuleException exception)
            {
                return Failure<QuizVersionResponse>(exception.Code, exception.Message);
            }
            questions.Add(question);
            foreach (QuizOptionInput option in input.Options.OrderBy(item => item.Position))
            {
                questionOptions.Add(QuizQuestionOption.Create(question.Id, option.Position, option.Text, option.IsCorrect));
            }
        }
        if (addQuiz)
        {
            dbContext.Quizzes.Add(quiz);
        }
        dbContext.QuizVersions.Add(version);
        dbContext.QuizQuestions.AddRange(questions);
        dbContext.QuizQuestionOptions.AddRange(questionOptions);
        Guid[] selectedUserIds = audienceType == AssessmentAudienceType.SelectedLearners
            ? request.SelectedLearnerUserIds!.Distinct().ToArray()
            : [];
        dbContext.QuizAudienceMembers.AddRange(selectedUserIds.Select(userId => QuizAudienceMember.Create(version.Id, userId, now)));
        return Result.Success(MapQuizVersion(version, quiz, selectedUserIds));
    }

    public async Task<Result<QuizVersionResponse>> MarkQuizVersionReadyAsync(
        MarkQuizVersionReadyCommand request,
        CancellationToken cancellationToken)
    {
        QuizVersion? version = await (
            from item in dbContext.QuizVersions
            join quizRow in dbContext.Quizzes on item.QuizId equals quizRow.Id
            where item.Id == request.VersionId && quizRow.CourseId == request.CourseId
            select item).SingleOrDefaultAsync(cancellationToken);
        if (version is null || !await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken))
        {
            return Failure<QuizVersionResponse>("QUIZ.VERSION_NOT_FOUND", "The quiz version was not found.", notFound: true);
        }
        int count = await dbContext.QuizQuestions.CountAsync(item => item.QuizVersionId == version.Id, cancellationToken);
        try
        {
            version.MarkReady(count, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Failure<QuizVersionResponse>(exception.Code, exception.Message);
        }
        Quiz quiz = await dbContext.Quizzes.SingleAsync(item => item.Id == version.QuizId, cancellationToken);
        return Result.Success(MapQuizVersion(version, quiz, await QuizAudienceUserIdsAsync(version.Id, cancellationToken)));
    }

    public async Task<Result<QuizAttemptResponse>> StartQuizAttemptAsync(
        StartQuizAttemptCommand request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken,
            lockEnrollment: true);
        if (enrollment is null || release is null)
        {
            return Failure<QuizAttemptResponse>("QUIZ.NOT_FOUND", "The quiz was not found.", notFound: true);
        }
        CourseReleaseAssessment? reference = await dbContext.CourseReleaseAssessments.AsNoTracking().SingleOrDefaultAsync(
            item => item.ReleaseId == release.Id && item.QuizVersionId == request.QuizVersionId,
            cancellationToken);
        QuizVersion? version = await dbContext.QuizVersions.SingleOrDefaultAsync(
            item => item.Id == request.QuizVersionId && item.Status == AssessmentVersionStatus.Ready,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (reference is null || version is null || !await CanAccessQuizAsync(version, request.UserId, cancellationToken))
        {
            return Failure<QuizAttemptResponse>("QUIZ.NOT_FOUND", "The quiz was not found.", notFound: true);
        }
        QuizAttempt? inProgress = await dbContext.QuizAttempts.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollment.Id && item.QuizVersionId == version.Id && item.Status == QuizAttemptStatus.InProgress,
            cancellationToken);
        if (version.Deadline is { } deadline && deadline <= now)
        {
            inProgress?.Expire(now);
            return Failure<QuizAttemptResponse>("QUIZ.NOT_FOUND", "The quiz was not found.", notFound: true);
        }
        if (inProgress is not null && inProgress.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            inProgress.Expire(now);
            inProgress = null;
        }
        if (inProgress is not null && (inProgress.ExpiresAt is null || inProgress.ExpiresAt > now))
        {
            return Result.Success(await MapQuizAttemptAsync(inProgress, cancellationToken));
        }
        int attemptCount = await dbContext.QuizAttempts.CountAsync(
            item => item.EnrollmentId == enrollment.Id && item.QuizVersionId == version.Id,
            cancellationToken);
        if (attemptCount >= version.AttemptLimit)
        {
            return Failure<QuizAttemptResponse>("QUIZ.ATTEMPT_LIMIT", "The quiz attempt limit has been reached.");
        }
        QuizAttempt attempt = QuizAttempt.Start(enrollment.Id, version.Id, attemptCount + 1, now, version.DurationMinutes);
        dbContext.QuizAttempts.Add(attempt);
        return Result.Success(await MapQuizAttemptAsync(attempt, cancellationToken));
    }

    public async Task<Result<QuizAttemptResponse>> GetQuizAttemptAsync(
        GetQuizAttemptQuery request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken);
        QuizVersion? version = enrollment is null || release is null
            ? null
            : await dbContext.QuizVersions.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == request.QuizVersionId && item.Status == AssessmentVersionStatus.Ready,
                cancellationToken);
        if (enrollment is null || release is null || version is null ||
            !await CanAccessQuizAsync(version, request.UserId, cancellationToken) ||
            !await dbContext.CourseReleaseAssessments.AsNoTracking().AnyAsync(
                item => item.ReleaseId == release.Id && item.QuizVersionId == version.Id,
                cancellationToken))
        {
            return Failure<QuizAttemptResponse>("QUIZ.NOT_FOUND", "The quiz was not found.", notFound: true);
        }
        QuizAttempt? attempt = await dbContext.QuizAttempts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.AttemptId && item.EnrollmentId == request.EnrollmentId && item.QuizVersionId == request.QuizVersionId,
            cancellationToken);
        return attempt is null
            ? Failure<QuizAttemptResponse>("QUIZ.ATTEMPT_NOT_FOUND", "The quiz attempt was not found.", notFound: true)
            : Result.Success(await MapQuizAttemptAsync(attempt, cancellationToken));
    }

    public async Task<Result<QuizAttemptResponse>> SubmitQuizAttemptAsync(
        SubmitQuizAttemptCommand request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken,
            lockEnrollment: true);
        await LockQuizAttemptAsync(request.AttemptId, cancellationToken);
        QuizAttempt? attempt = enrollment is null
            ? null
            : await dbContext.QuizAttempts.SingleOrDefaultAsync(
                item => item.Id == request.AttemptId && item.EnrollmentId == enrollment.Id && item.QuizVersionId == request.QuizVersionId,
                cancellationToken);
        QuizVersion? version = attempt is null
            ? null
            : await dbContext.QuizVersions.SingleOrDefaultAsync(item => item.Id == attempt.QuizVersionId && item.Status == AssessmentVersionStatus.Ready, cancellationToken);
        if (enrollment is null || release is null || attempt is null || version is null ||
            !await dbContext.CourseReleaseAssessments.AnyAsync(item => item.ReleaseId == release.Id && item.QuizVersionId == version.Id, cancellationToken) ||
            !await CanAccessQuizAsync(version, request.UserId, cancellationToken))
        {
            return Failure<QuizAttemptResponse>("QUIZ.NOT_FOUND", "The quiz was not found.", notFound: true);
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (attempt.Status == QuizAttemptStatus.Expired)
        {
            return Failure<QuizAttemptResponse>("QUIZ.ATTEMPT_EXPIRED", "The quiz attempt has expired.");
        }
        if (attempt.Status != QuizAttemptStatus.InProgress)
        {
            return Result.Success(await MapQuizAttemptAsync(attempt, cancellationToken));
        }
        if (attempt.ExpiresAt is { } attemptExpiresAt && attemptExpiresAt <= now)
        {
            attempt.Expire(now);
            return Failure<QuizAttemptResponse>("QUIZ.ATTEMPT_EXPIRED", "The quiz attempt has expired.");
        }
        if (version.Deadline is { } deadline && deadline <= now)
        {
            attempt.Expire(now);
            return Failure<QuizAttemptResponse>("QUIZ.DEADLINE_PASSED", "The quiz deadline has passed.");
        }
        List<QuizQuestion> questions = await dbContext.QuizQuestions.AsNoTracking()
            .Where(item => item.QuizVersionId == version.Id)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, QuizQuestionInputAnswer> answers = request.Answers
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => new QuizQuestionInputAnswer(
                group.First().TextAnswer,
                group.First().SelectedOptionIds ?? []));
        if (answers.Count != request.Answers.Count || request.Answers.Any(answer => !questions.Any(question => question.Id == answer.QuestionId)))
        {
            return Failure<QuizAttemptResponse>("QUIZ.ANSWER_INVALID", "The submitted answers are invalid.");
        }
        Dictionary<Guid, QuizQuestionOption[]> options = await dbContext.QuizQuestionOptions.AsNoTracking()
            .Where(option => questions.Select(question => question.Id).Contains(option.QuestionId))
            .GroupBy(option => option.QuestionId)
            .ToDictionaryAsync(group => group.Key, group => group.OrderBy(option => option.Position).ToArray(), cancellationToken);
        foreach (QuizQuestion question in questions)
        {
            QuizQuestionInputAnswer? answer = answers.GetValueOrDefault(question.Id);
            Guid[] validOptionIds = options.GetValueOrDefault(question.Id, [])
                .Select(option => option.Id)
                .ToArray();
            if ((answer?.SelectedOptionIds ?? []).Any(optionId => !validOptionIds.Contains(optionId)) ||
                question.Type == QuizQuestionType.ShortAnswer && (answer?.SelectedOptionIds.Count ?? 0) > 0)
            {
                return Failure<QuizAttemptResponse>("QUIZ.ANSWER_INVALID", "The submitted answers are invalid.");
            }
        }
        decimal totalPoints = questions.Sum(question => question.Points);
        decimal objectivePoints = 0;
        bool requiresManualGrade = false;
        var answerEntities = new List<QuizAnswer>(questions.Count);
        foreach (QuizQuestion question in questions)
        {
            answers.TryGetValue(question.Id, out QuizQuestionInputAnswer? answer);
            decimal? awarded = null;
            if (question.Type == QuizQuestionType.ShortAnswer)
            {
                if (question.AcceptedAnswer is null)
                {
                    requiresManualGrade = true;
                }
                else if (NormalizeAnswer(answer?.TextAnswer) == NormalizeAnswer(question.AcceptedAnswer))
                {
                    awarded = question.Points;
                }
            }
            else
            {
                Guid[] selected = answer?.SelectedOptionIds.Distinct().Order().ToArray() ?? [];
                Guid[] correct = options.GetValueOrDefault(question.Id, []).Where(option => option.IsCorrect).Select(option => option.Id).Order().ToArray();
                if (selected.SequenceEqual(correct))
                {
                    awarded = question.Points;
                }
            }
            objectivePoints += awarded ?? 0;
            answerEntities.Add(QuizAnswer.Create(
                attempt.Id,
                question.Id,
                answer?.TextAnswer,
                answer?.SelectedOptionIds ?? [],
                awarded));
        }
        decimal objectiveScore = totalPoints == 0 ? 0 : Math.Round(objectivePoints / totalPoints * 100, 2);
        try
        {
            attempt.Submit(objectiveScore, requiresManualGrade, version.PassScore, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Failure<QuizAttemptResponse>(exception.Code, exception.Message);
        }
        dbContext.QuizAnswers.AddRange(answerEntities);
        AddAudit(request.UserId, "assessment.quiz-submitted", "QuizAttempt", attempt.Id, null, timeProvider.GetUtcNow());
        if (attempt.Passed == true)
        {
            await CompleteAssessmentLessonAsync(enrollment, release.Id, version.Id, timeProvider.GetUtcNow(), cancellationToken);
        }
        return Result.Success(await MapQuizAttemptAsync(attempt, cancellationToken));
    }

    public async Task<Result<GradeResponse>> GradeQuizAttemptAsync(
        GradeQuizAttemptCommand request,
        CancellationToken cancellationToken)
    {
        QuizAttempt? attemptSnapshot = await dbContext.QuizAttempts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.AttemptId, cancellationToken);
        if (attemptSnapshot is null)
        {
            return Failure<GradeResponse>("QUIZ.ATTEMPT_NOT_FOUND", "The quiz attempt was not found.", notFound: true);
        }
        await LockEnrollmentAsync(attemptSnapshot.EnrollmentId, cancellationToken);
        await LockQuizAttemptAsync(request.AttemptId, cancellationToken);
        QuizAttempt? attempt = await dbContext.QuizAttempts.SingleOrDefaultAsync(item => item.Id == request.AttemptId, cancellationToken);
        QuizVersion? version = attempt is null ? null : await dbContext.QuizVersions.SingleOrDefaultAsync(item => item.Id == attempt.QuizVersionId, cancellationToken);
        Quiz? quiz = version is null ? null : await dbContext.Quizzes.SingleOrDefaultAsync(item => item.Id == version.QuizId && item.CourseId == request.CourseId, cancellationToken);
        if (attempt is null || version is null || quiz is null || !await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken))
        {
            return Failure<GradeResponse>("QUIZ.ATTEMPT_NOT_FOUND", "The quiz attempt was not found.", notFound: true);
        }
        if (attempt.Status is QuizAttemptStatus.InProgress or QuizAttemptStatus.Expired)
        {
            return Failure<GradeResponse>("QUIZ.ATTEMPT_NOT_GRADABLE", "The quiz attempt is not ready for grading.");
        }
        int revisionNumber = (await dbContext.QuizGradeRevisions
            .Where(item => item.AttemptId == attempt.Id)
            .Select(item => (int?)item.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        DateTimeOffset now = timeProvider.GetUtcNow();
        QuizGradeRevision revision;
        try
        {
            revision = QuizGradeRevision.Create(
                attempt.Id,
                revisionNumber,
                request.Score,
                request.Feedback,
                request.ActorUserId,
                now);
            attempt.ApplyManualGrade(request.Score, version.PassScore);
        }
        catch (DomainRuleException exception)
        {
            return Failure<GradeResponse>(exception.Code, exception.Message);
        }
        dbContext.QuizGradeRevisions.Add(revision);
        Enrollment? enrollment = await dbContext.Enrollments.SingleAsync(item => item.Id == attempt.EnrollmentId, cancellationToken);
        if (attempt.Passed == true)
        {
            await CompleteAssessmentLessonAsync(enrollment, (await dbContext.CourseReleases.AsNoTracking().SingleAsync(item => item.Id == enrollment.ReleaseId, cancellationToken)).Id, version.Id, now, cancellationToken);
        }
        AddAudit(request.ActorUserId, "assessment.quiz-graded", "QuizAttempt", attempt.Id, request.AuditReason, now);
        return Result.Success(new GradeResponse(attempt.Id, revision.Score, revision.Feedback, revision.RevisionNumber, now));
    }

    public async Task<Result<AssignmentVersionResponse>> CreateAssignmentVersionAsync(
        CreateAssignmentVersionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken) ||
            !await CourseLessonExistsAsync(request.CourseId, request.LessonId, "Assignment", cancellationToken))
        {
            return Failure<AssignmentVersionResponse>("ASSESSMENT.NOT_FOUND", "The assessment resource was not found.", notFound: true);
        }
        if (!Enum.TryParse(request.AudienceType, true, out AssessmentAudienceType audienceType) ||
            !await ValidateAudienceAsync(request.CourseId, audienceType, request.SelectedLearnerUserIds, cancellationToken))
        {
            return Failure<AssignmentVersionResponse>("ASSESSMENT.AUDIENCE_INVALID", "The selected assessment audience is invalid.");
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        Assignment? assignment = await dbContext.Assignments.SingleOrDefaultAsync(
            item => item.CourseId == request.CourseId && item.LessonId == request.LessonId,
            cancellationToken);
        bool addAssignment = assignment is null;
        assignment ??= Assignment.Create(request.CourseId, request.LessonId, request.ActorUserId, now);
        int versionNumber = (await dbContext.AssignmentVersions.Where(item => item.AssignmentId == assignment.Id).Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        AssignmentVersion version;
        try
        {
            version = AssignmentVersion.Create(assignment.Id, versionNumber, request.Title, request.Instructions, request.Deadline, request.AllowMultipleSubmissions, audienceType, now);
        }
        catch (DomainRuleException exception)
        {
            return Failure<AssignmentVersionResponse>(exception.Code, exception.Message);
        }
        if (addAssignment)
        {
            dbContext.Assignments.Add(assignment);
        }
        dbContext.AssignmentVersions.Add(version);
        Guid[] selectedUserIds = audienceType == AssessmentAudienceType.SelectedLearners
            ? request.SelectedLearnerUserIds!.Distinct().ToArray()
            : [];
        dbContext.AssignmentAudienceMembers.AddRange(selectedUserIds.Select(userId => AssignmentAudienceMember.Create(version.Id, userId, now)));
        return Result.Success(MapAssignmentVersion(version, assignment, selectedUserIds));
    }

    public async Task<Result<AssignmentVersionResponse>> MarkAssignmentVersionReadyAsync(
        MarkAssignmentVersionReadyCommand request,
        CancellationToken cancellationToken)
    {
        AssignmentVersion? version = await (
            from item in dbContext.AssignmentVersions
            join assignmentRow in dbContext.Assignments on item.AssignmentId equals assignmentRow.Id
            where item.Id == request.VersionId && assignmentRow.CourseId == request.CourseId
            select item).SingleOrDefaultAsync(cancellationToken);
        if (version is null || !await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken))
        {
            return Failure<AssignmentVersionResponse>("ASSIGNMENT.VERSION_NOT_FOUND", "The assignment version was not found.", notFound: true);
        }
        version.MarkReady(timeProvider.GetUtcNow());
        Assignment assignment = await dbContext.Assignments.SingleAsync(item => item.Id == version.AssignmentId, cancellationToken);
        return Result.Success(MapAssignmentVersion(version, assignment, await AssignmentAudienceUserIdsAsync(version.Id, cancellationToken)));
    }

    public async Task<Result<AssignmentSubmissionResponse>> SubmitAssignmentAsync(
        SubmitAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken,
            lockEnrollment: true);
        AssignmentVersion? version = await dbContext.AssignmentVersions.SingleOrDefaultAsync(
            item => item.Id == request.AssignmentVersionId && item.Status == AssessmentVersionStatus.Ready,
            cancellationToken);
        CourseReleaseAssessment? reference = release is null ? null : await dbContext.CourseReleaseAssessments.AsNoTracking().SingleOrDefaultAsync(
            item => item.ReleaseId == release.Id && item.AssignmentVersionId == request.AssignmentVersionId,
            cancellationToken);
        if (enrollment is null || release is null || version is null || reference is null ||
            !await CanAccessAssignmentAsync(version, request.UserId, cancellationToken))
        {
            return Failure<AssignmentSubmissionResponse>("ASSIGNMENT.NOT_FOUND", "The assignment was not found.", notFound: true);
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (version.Deadline is { } deadline && deadline <= now)
        {
            return Failure<AssignmentSubmissionResponse>("ASSIGNMENT.DEADLINE_PASSED", "The assignment deadline has passed.");
        }
        int previous = await dbContext.AssignmentSubmissions.CountAsync(
            item => item.EnrollmentId == enrollment.Id && item.AssignmentVersionId == version.Id,
            cancellationToken);
        if (!version.AllowMultipleSubmissions && previous > 0)
        {
            return Failure<AssignmentSubmissionResponse>("ASSIGNMENT.SUBMISSION_EXISTS", "Only one submission is allowed.");
        }
        AssignmentSubmission submission;
        try
        {
            submission = AssignmentSubmission.Submit(enrollment.Id, version.Id, previous + 1, request.Text, now);
        }
        catch (DomainRuleException exception)
        {
            return Failure<AssignmentSubmissionResponse>(exception.Code, exception.Message);
        }
        dbContext.AssignmentSubmissions.Add(submission);
        AddAudit(request.UserId, "assessment.assignment-submitted", "AssignmentSubmission", submission.Id, null, now);
        return Result.Success(await MapAssignmentSubmissionAsync(submission, cancellationToken));
    }

    public async Task<Result<AssignmentSubmissionResponse>> GetAssignmentSubmissionAsync(
        GetAssignmentSubmissionQuery request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken);
        AssignmentVersion? version = enrollment is null || release is null
            ? null
            : await dbContext.AssignmentVersions.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == request.AssignmentVersionId && item.Status == AssessmentVersionStatus.Ready,
                cancellationToken);
        AssignmentSubmission? submission = enrollment is null || release is null || version is null ||
            !await CanAccessAssignmentAsync(version, request.UserId, cancellationToken)
            ? null
            : await dbContext.AssignmentSubmissions.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == request.SubmissionId && item.EnrollmentId == enrollment.Id &&
                    item.AssignmentVersionId == request.AssignmentVersionId &&
                    dbContext.CourseReleaseAssessments.Any(reference => reference.ReleaseId == release.Id &&
                        reference.AssignmentVersionId == item.AssignmentVersionId),
                cancellationToken);
        return submission is null
            ? Failure<AssignmentSubmissionResponse>("ASSIGNMENT.SUBMISSION_NOT_FOUND", "The assignment submission was not found.", notFound: true)
            : Result.Success(await MapAssignmentSubmissionAsync(submission, cancellationToken));
    }

    public async Task<Result<AssignmentSubmissionResponse>> GetCurrentAssignmentSubmissionAsync(
        GetCurrentAssignmentSubmissionQuery request,
        CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(
            request.UserId,
            request.EnrollmentId,
            cancellationToken);
        AssignmentVersion? version = enrollment is null || release is null
            ? null
            : await dbContext.AssignmentVersions.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == request.AssignmentVersionId && item.Status == AssessmentVersionStatus.Ready,
                cancellationToken);
        if (enrollment is null || release is null || version is null ||
            !await CanAccessAssignmentAsync(version, request.UserId, cancellationToken) ||
            !await dbContext.CourseReleaseAssessments.AsNoTracking().AnyAsync(
                item => item.ReleaseId == release.Id && item.AssignmentVersionId == version.Id,
                cancellationToken))
        {
            return Failure<AssignmentSubmissionResponse>(
                "ASSIGNMENT.SUBMISSION_NOT_FOUND",
                "The assignment submission was not found.",
                notFound: true);
        }

        AssignmentSubmission? submission = await dbContext.AssignmentSubmissions.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollment.Id && item.AssignmentVersionId == version.Id)
            .OrderByDescending(item => item.SubmissionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return submission is null
            ? Failure<AssignmentSubmissionResponse>("ASSIGNMENT.SUBMISSION_NOT_FOUND", "The assignment submission was not found.", notFound: true)
            : Result.Success(await MapAssignmentSubmissionAsync(submission, cancellationToken));
    }

    public async Task<Result<GradeResponse>> GradeAssignmentAsync(
        GradeAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        AssignmentSubmission? submissionSnapshot = await dbContext.AssignmentSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SubmissionId, cancellationToken);
        if (submissionSnapshot is null)
        {
            return Failure<GradeResponse>("ASSIGNMENT.SUBMISSION_NOT_FOUND", "The assignment submission was not found.", notFound: true);
        }
        await LockEnrollmentAsync(submissionSnapshot.EnrollmentId, cancellationToken);
        await LockAssignmentSubmissionAsync(request.SubmissionId, cancellationToken);
        AssignmentSubmission? submission = await dbContext.AssignmentSubmissions.SingleOrDefaultAsync(
            item => item.Id == request.SubmissionId,
            cancellationToken);
        AssignmentVersion? version = submission is null ? null : await dbContext.AssignmentVersions.SingleOrDefaultAsync(
            item => item.Id == submission.AssignmentVersionId,
            cancellationToken);
        Assignment? assignment = version is null ? null : await dbContext.Assignments.SingleOrDefaultAsync(
            item => item.Id == version.AssignmentId && item.CourseId == request.CourseId,
            cancellationToken);
        if (submission is null || version is null || assignment is null || !await CanManageCourseAsync(request.ActorUserId, request.CourseId, cancellationToken))
        {
            return Failure<GradeResponse>("ASSIGNMENT.SUBMISSION_NOT_FOUND", "The assignment submission was not found.", notFound: true);
        }
        bool hasPendingFiles = await (
            from file in dbContext.AssignmentSubmissionFiles.AsNoTracking()
            join asset in dbContext.MediaAssets.AsNoTracking() on file.AssetId equals asset.Id
            where file.SubmissionId == submission.Id &&
                asset.State != MediaAssetState.Ready &&
                asset.State != MediaAssetState.Rejected &&
                asset.State != MediaAssetState.Deleted
            select file.Id).AnyAsync(cancellationToken);
        if (hasPendingFiles)
        {
            return Failure<GradeResponse>(
                "ASSIGNMENT.FILES_NOT_READY",
                "Assignment files must finish scanning and processing before grading.");
        }
        int revisionNumber = (await dbContext.GradeRevisions.Where(item => item.SubmissionId == submission.Id).Select(item => (int?)item.RevisionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        GradeRevision revision;
        try
        {
            revision = GradeRevision.Create(submission.Id, revisionNumber, request.Score, request.Feedback, request.ActorUserId, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Failure<GradeResponse>(exception.Code, exception.Message);
        }
        dbContext.GradeRevisions.Add(revision);
        Enrollment enrollment = await dbContext.Enrollments.SingleAsync(item => item.Id == submission.EnrollmentId, cancellationToken);
        CourseRelease release = await dbContext.CourseReleases.AsNoTracking().SingleAsync(item => item.Id == enrollment.ReleaseId, cancellationToken);
        await CompleteAssessmentLessonAsync(enrollment, release.Id, version.Id, revision.GradedAt, cancellationToken);
        AddAudit(request.ActorUserId, "assessment.assignment-graded", "AssignmentSubmission", submission.Id, request.AuditReason, revision.GradedAt);
        return Result.Success(new GradeResponse(submission.Id, revision.Score, revision.Feedback, revision.RevisionNumber, revision.GradedAt));
    }

    private async Task<(Enrollment? Enrollment, CourseRelease? Release)> FindLearningContextAsync(
        Guid userId,
        Guid enrollmentId,
        CancellationToken cancellationToken,
        bool lockEnrollment = false)
    {
        if (lockEnrollment)
        {
            await LockEnrollmentAsync(enrollmentId, cancellationToken);
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        Enrollment? enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            item => item.Id == enrollmentId && item.UserId == userId &&
                (item.Status == EnrollmentStatus.Active ||
                    item.Status == EnrollmentStatus.Completed),
            cancellationToken);
        if (enrollment is null)
        {
            return (null, null);
        }
        bool entitled = await dbContext.Entitlements.AsNoTracking().AnyAsync(
            item => item.Id == enrollment.EntitlementId && item.UserId == userId && item.CourseId == enrollment.CourseId && item.Status == EntitlementStatus.Active &&
                (item.ExpiresAt == null || item.ExpiresAt > now),
            cancellationToken);
        CourseRelease? release = entitled
            ? await dbContext.CourseReleases.AsNoTracking().SingleOrDefaultAsync(item => item.Id == enrollment.ReleaseId && item.CourseId == enrollment.CourseId, cancellationToken)
            : null;
        return entitled ? (enrollment, release) : (null, null);
    }

    private async Task<bool> HasLessonAccessAsync(Guid userId, Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        (Enrollment? enrollment, CourseRelease? release) = await FindLearningContextAsync(userId, enrollmentId, cancellationToken);
        return enrollment is not null && release is not null && await dbContext.CourseReleaseLessons.AsNoTracking().AnyAsync(
            lesson => lesson.Id == lessonId && lesson.ReleaseId == release.Id,
            cancellationToken);
    }

    private IQueryable<CourseReleaseAssessment> AccessibleAssessments(Enrollment enrollment) =>
        dbContext.CourseReleaseAssessments.AsNoTracking().Where(reference =>
            reference.ReleaseId == enrollment.ReleaseId &&
            (reference.QuizVersionId != null && dbContext.QuizVersions.Any(version =>
                version.Id == reference.QuizVersionId &&
                (version.AudienceType == AssessmentAudienceType.AllEnrolled ||
                    dbContext.QuizAudienceMembers.Any(member => member.QuizVersionId == version.Id && member.UserId == enrollment.UserId))) ||
             reference.AssignmentVersionId != null && dbContext.AssignmentVersions.Any(version =>
                version.Id == reference.AssignmentVersionId &&
                (version.AudienceType == AssessmentAudienceType.AllEnrolled ||
                    dbContext.AssignmentAudienceMembers.Any(member => member.AssignmentVersionId == version.Id && member.UserId == enrollment.UserId)))));

    private Task<bool> CanAccessQuizAsync(QuizVersion version, Guid userId, CancellationToken cancellationToken) =>
        version.AudienceType == AssessmentAudienceType.AllEnrolled
            ? Task.FromResult(true)
            : dbContext.QuizAudienceMembers.AsNoTracking().AnyAsync(
                member => member.QuizVersionId == version.Id && member.UserId == userId,
                cancellationToken);

    private Task<bool> CanAccessAssignmentAsync(AssignmentVersion version, Guid userId, CancellationToken cancellationToken) =>
        version.AudienceType == AssessmentAudienceType.AllEnrolled
            ? Task.FromResult(true)
            : dbContext.AssignmentAudienceMembers.AsNoTracking().AnyAsync(
                member => member.AssignmentVersionId == version.Id && member.UserId == userId,
                cancellationToken);

    private async Task<bool> ValidateAudienceAsync(
        Guid courseId,
        AssessmentAudienceType audienceType,
        IReadOnlyList<Guid>? selectedLearnerUserIds,
        CancellationToken cancellationToken)
    {
        if (audienceType == AssessmentAudienceType.AllEnrolled)
        {
            return selectedLearnerUserIds is null || selectedLearnerUserIds.Count == 0;
        }

        Guid[] selected = selectedLearnerUserIds?.Distinct().ToArray() ?? [];
        if (selected.Length == 0)
        {
            return false;
        }

        int enrolled = await dbContext.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.CourseId == courseId && selected.Contains(enrollment.UserId) &&
                (enrollment.Status == EnrollmentStatus.Active ||
                    enrollment.Status == EnrollmentStatus.Completed ||
                    enrollment.Status == EnrollmentStatus.Suspended))
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        return enrolled == selected.Length;
    }

    private Task<Guid[]> QuizAudienceUserIdsAsync(Guid versionId, CancellationToken cancellationToken) =>
        dbContext.QuizAudienceMembers.AsNoTracking()
            .Where(member => member.QuizVersionId == versionId)
            .OrderBy(member => member.UserId)
            .Select(member => member.UserId)
            .ToArrayAsync(cancellationToken);

    private Task<Guid[]> AssignmentAudienceUserIdsAsync(Guid versionId, CancellationToken cancellationToken) =>
        dbContext.AssignmentAudienceMembers.AsNoTracking()
            .Where(member => member.AssignmentVersionId == versionId)
            .OrderBy(member => member.UserId)
            .Select(member => member.UserId)
            .ToArrayAsync(cancellationToken);

    private async Task<bool> CanManageCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId && course.DeletedAt == null &&
                (course.OwnerUserId == userId || dbContext.CourseInstructors.Any(instructor => instructor.CourseId == courseId && instructor.UserId == userId && instructor.Role != CourseCollaboratorRole.Reviewer)),
            cancellationToken) || await HasPermissionAsync(userId, Permissions.CourseManageAny, cancellationToken);

    private Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.RoleClaims.AsNoTracking(), role => role.RoleId, claim => claim.RoleId, (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId &&
                item.claim.ClaimType == IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == permission, cancellationToken);

    private Task<bool> CourseLessonExistsAsync(
        Guid courseId,
        Guid lessonId,
        string lessonType,
        CancellationToken cancellationToken) =>
        dbContext.CourseLessons.AsNoTracking().AnyAsync(
            lesson => lesson.Id == lessonId && lesson.RemovedAt == null && lesson.CurrentRevisionId != null &&
                dbContext.CourseDrafts.Any(draft => draft.Id == lesson.DraftId && draft.CourseId == courseId) &&
                dbContext.LessonRevisions.Any(revision => revision.Id == lesson.CurrentRevisionId && revision.LessonType == lessonType),
            cancellationToken);

    private async Task<CourseReleaseLocalization?> FindLocalizationAsync(
        Guid releaseId,
        string locale,
        CancellationToken cancellationToken)
    {
        string defaultLocale = await dbContext.CourseReleases.AsNoTracking()
            .Where(release => release.Id == releaseId)
            .Select(release => release.DefaultLocale)
            .SingleOrDefaultAsync(cancellationToken) ?? locale;
        CourseReleaseLocalization? requested = await dbContext.CourseReleaseLocalizations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ReleaseId == releaseId && item.Locale == locale, cancellationToken);
        if (requested is not null)
        {
            return requested;
        }

        return await dbContext.CourseReleaseLocalizations.AsNoTracking()
            .Where(item => item.ReleaseId == releaseId)
            .OrderBy(item => item.Locale == defaultLocale ? 0 : 1)
            .ThenBy(item => item.Locale)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<EnrollmentResponse> MapEnrollmentAsync(Enrollment enrollment, string locale, CancellationToken cancellationToken)
    {
        CourseReleaseLocalization? localization = await FindLocalizationAsync(enrollment.ReleaseId, locale, cancellationToken);
        return new EnrollmentResponse(
            enrollment.Id,
            enrollment.CourseId,
            enrollment.ReleaseId,
            enrollment.Status.ToString(),
            enrollment.EnrolledAt,
            localization?.Title ?? string.Empty,
            localization?.Slug ?? string.Empty);
    }

    private static LearningLessonSummaryResponse MapLessonSummary(
        CourseReleaseLesson lesson,
        LessonProgress? progress,
        CourseReleaseAssessment? assessment) => new(
            lesson.Id,
            lesson.Position,
            lesson.Title,
            lesson.LessonType,
            lesson.CompletionRequirement,
            progress?.IsCompleted ?? false,
            progress?.PositionSeconds ?? 0,
            assessment?.QuizVersionId,
            assessment?.AssignmentVersionId);

    private async Task<QuizAttemptResponse> MapQuizAttemptAsync(QuizAttempt attempt, CancellationToken cancellationToken)
    {
        List<QuizQuestion> questions = await dbContext.QuizQuestions.AsNoTracking()
            .Where(item => item.QuizVersionId == attempt.QuizVersionId)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);
        Guid[] questionIds = questions.Select(item => item.Id).ToArray();
        List<QuizQuestionOption> options = await dbContext.QuizQuestionOptions.AsNoTracking()
            .Where(item => questionIds.Contains(item.QuestionId))
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);
        return new QuizAttemptResponse(
            attempt.Id,
            attempt.EnrollmentId,
            attempt.QuizVersionId,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.StartedAt,
            attempt.ExpiresAt,
            attempt.SubmittedAt,
            attempt.Score,
            attempt.Passed,
            questions.Select(question => new QuizAttemptQuestionResponse(
                question.Id,
                question.Position,
                question.Type.ToString(),
                question.Prompt,
                question.Points,
                options.Where(option => option.QuestionId == question.Id)
                    .Select(option => new QuizAttemptOptionResponse(option.Id, option.Position, option.Text))
                    .ToArray())).ToArray());
    }

    private async Task<AssignmentSubmissionResponse> MapAssignmentSubmissionAsync(
        AssignmentSubmission submission,
        CancellationToken cancellationToken)
    {
        GradeRevision? grade = await dbContext.GradeRevisions.AsNoTracking()
            .Where(item => item.SubmissionId == submission.Id)
            .OrderByDescending(item => item.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        AssignmentSubmissionFileResponse[] files = await (
            from file in dbContext.AssignmentSubmissionFiles.AsNoTracking()
            join asset in dbContext.MediaAssets.AsNoTracking() on file.AssetId equals asset.Id
            where file.SubmissionId == submission.Id
            orderby file.CreatedAt, file.Id
            select new AssignmentSubmissionFileResponse(
                file.Id,
                asset.Id,
                file.ClientFileId,
                asset.FileName,
                asset.ContentType,
                asset.DeclaredBytes,
                asset.State.ToString(),
                asset.RejectionCode,
                file.CreatedAt,
                asset.ReadyAt)).ToArrayAsync(cancellationToken);
        return new AssignmentSubmissionResponse(
            submission.Id,
            submission.EnrollmentId,
            submission.AssignmentVersionId,
            submission.SubmissionNumber,
            submission.Text,
            submission.SubmittedAt,
            grade?.Score,
            grade?.Feedback,
            grade?.RevisionNumber ?? 0,
            files);
    }

    private static QuizVersionResponse MapQuizVersion(QuizVersion version, Quiz quiz, IReadOnlyList<Guid> selectedLearnerUserIds) => new(
        quiz.Id,
        version.Id,
        quiz.CourseId,
        quiz.LessonId,
        version.VersionNumber,
        version.Title,
        version.Status.ToString(),
        version.AttemptLimit,
        version.DurationMinutes,
        version.Deadline,
        version.PassScore,
        version.AudienceType.ToString(),
        selectedLearnerUserIds);

    private static AssignmentVersionResponse MapAssignmentVersion(AssignmentVersion version, Assignment assignment, IReadOnlyList<Guid> selectedLearnerUserIds) => new(
        assignment.Id,
        version.Id,
        assignment.CourseId,
        assignment.LessonId,
        version.VersionNumber,
        version.Title,
        version.Instructions,
        version.Status.ToString(),
        version.Deadline,
        version.AllowMultipleSubmissions,
        version.AudienceType.ToString(),
        selectedLearnerUserIds);

    private async Task CompleteAssessmentLessonAsync(
        Enrollment enrollment,
        Guid releaseId,
        Guid versionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CourseReleaseAssessment? reference = await dbContext.CourseReleaseAssessments.SingleOrDefaultAsync(
            item => item.ReleaseId == releaseId && (item.QuizVersionId == versionId || item.AssignmentVersionId == versionId),
            cancellationToken);
        if (reference is null)
        {
            return;
        }
        LessonProgress? progress = await dbContext.LessonProgress.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollment.Id && item.LessonId == reference.LessonId,
            cancellationToken);
        if (progress is null)
        {
            progress = LessonProgress.Create(enrollment.Id, reference.LessonId, now);
            dbContext.LessonProgress.Add(progress);
        }
        progress.CompleteFromAssessment(now);
        await CompleteCourseIfReadyAsync(enrollment, releaseId, now, cancellationToken);
    }

    private async Task CompleteCourseIfReadyAsync(
        Enrollment enrollment,
        Guid releaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            return;
        }
        Guid[] lessonIds = await dbContext.CourseReleaseLessons.AsNoTracking()
            .Where(item => item.ReleaseId == releaseId)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (lessonIds.Length == 0)
        {
            return;
        }
        Dictionary<Guid, bool> completion = await dbContext.LessonProgress.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollment.Id && lessonIds.Contains(item.LessonId))
            .ToDictionaryAsync(item => item.LessonId, item => item.IsCompleted, cancellationToken);
        foreach (LessonProgress tracked in dbContext.LessonProgress.Local.Where(item => item.EnrollmentId == enrollment.Id))
        {
            completion[tracked.LessonId] = tracked.IsCompleted;
        }
        if (lessonIds.Any(lessonId => !completion.GetValueOrDefault(lessonId)))
        {
            return;
        }
        enrollment.Complete(now);
        if (!await dbContext.CourseCompletions.AnyAsync(item => item.EnrollmentId == enrollment.Id, cancellationToken))
        {
            dbContext.CourseCompletions.Add(CourseCompletion.Create(enrollment.Id, enrollment.CourseId, releaseId, now));
            string payload = JsonSerializer.Serialize(new { CompletionEnrollmentId = enrollment.Id });
            dbContext.OutboxMessages.Add(OutboxMessage.Create(
                "learning.course-completed",
                1,
                payload,
                "{}",
                now));
        }
        AddAudit(enrollment.UserId, "learning.course-completed", "Enrollment", enrollment.Id, null, now);
    }

    private void AddAudit(Guid actorUserId, string action, string targetType, Guid targetId, string? reason, DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", reason, now));

    private async Task LockCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM catalog.courses WHERE id = {courseId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task LockEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM learning.enrollments WHERE id = {enrollmentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task LockQuizAttemptAsync(Guid attemptId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM assessment.quiz_attempts WHERE id = {attemptId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task LockAssignmentSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM assessment.assignment_submissions WHERE id = {submissionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static string NormalizeAnswer(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static Result<T> Failure<T>(string code, string description, bool notFound = false) =>
        Result.Failure<T>(notFound ? ResultError.NotFound(code, description) : ResultError.BusinessRule(code, description));

    private sealed record QuizQuestionInputAnswer(string? TextAnswer, IReadOnlyList<Guid> SelectedOptionIds);
}
