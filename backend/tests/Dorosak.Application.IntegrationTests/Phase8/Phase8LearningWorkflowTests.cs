using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Learning;
using Dorosak.Application.Features.Phase6;
using Dorosak.Application.Features.Publishing;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Phase8;

[Collection(InfrastructureTestGroup.Name)]
public sealed class Phase8LearningWorkflowTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task PublishEnrollAndComplete_PinsReleaseAndSurvivesUnpublish()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("phase8-reviewer", cancellationToken);
        Guid learnerId = await CreateConfirmedUserAsync("phase8-learner", cancellationToken);
        Guid secondLearnerId = await CreateConfirmedUserAsync("phase8-second-learner", cancellationToken);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    "Release Pinned Learning",
                    "A stable learner journey",
                    "A complete course used to verify immutable release-pinned learning.")],
                [],
                []),
            cancellationToken);
        Assert.True(created.IsSuccess);

        Result<CourseMutationResponse> curriculum = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                created.Value.CourseId,
                1,
                [new SectionInput(
                    null,
                    0,
                    "Foundation",
                    [new LessonInput(
                        null,
                        0,
                        "Read the release contract",
                        "Article",
                        "This lesson belongs to the immutable release manifest.")])]),
            cancellationToken);
        Assert.True(curriculum.IsSuccess);

        Result<PublicationStatusResponse> submitted = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(submitted.IsSuccess);
        Result<PublicationReviewResponse> approved = await sender.Send(
            new ReviewPublicationCommand(reviewerId, submitted.Value.ReviewId!.Value, "approve", null),
            cancellationToken);
        Assert.True(approved.IsSuccess);

        Result<CourseReleaseResponse> published = await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                created.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Approved content and release dependencies"),
            cancellationToken);
        Assert.True(published.IsSuccess);
        Assert.Equal("Active", published.Value.State);

        Result<EnrollmentResponse> enrolled = await sender.Send(
            new EnrollCourseCommand(
                learnerId,
                created.Value.CourseId,
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(enrolled.IsSuccess);
        Assert.Equal(published.Value.ReleaseId, enrolled.Value.ReleaseId);

        Result<CourseMutationResponse> nextDraft = await sender.Send(
            new StartNewDraftCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(nextDraft.IsSuccess);
        Result<PublicCourseLookupResponse> firstReleaseWhileDrafting = await sender.Send(
            new ResolvePublicCourseQuery("en", "release-pinned-learning"),
            cancellationToken);
        Assert.True(firstReleaseWhileDrafting.IsSuccess);
        Assert.Equal(published.Value.ReleaseId, firstReleaseWhileDrafting.Value.Course!.ReleaseId);

        Result<CourseMutationResponse> revisedMetadata = await sender.Send(
            new UpdateCourseMetadataCommand(
                teacherId,
                created.Value.CourseId,
                nextDraft.Value.DraftVersion,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    "Release Pinned Learning Revised",
                    "A stable learner journey",
                    "A revised course that verifies historical release redirects.")],
                [],
                []),
            cancellationToken);
        Assert.True(revisedMetadata.IsSuccess);
        Result<PublicationStatusResponse> secondReview = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(secondReview.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewPublicationCommand(reviewerId, secondReview.Value.ReviewId!.Value, "approve", null),
            cancellationToken)).IsSuccess);
        Result<CourseReleaseResponse> secondRelease = await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                created.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Approved revised metadata and retained curriculum"),
            cancellationToken);
        Assert.True(secondRelease.IsSuccess);
        Assert.Equal(2, secondRelease.Value.ReleaseNumber);
        Assert.NotEqual(published.Value.ReleaseId, secondRelease.Value.ReleaseId);

        Result<PublicCourseLookupResponse> historicalSlug = await sender.Send(
            new ResolvePublicCourseQuery("en", "release-pinned-learning"),
            cancellationToken);
        Assert.True(historicalSlug.IsSuccess);
        Assert.Null(historicalSlug.Value.Course);
        Assert.Equal("release-pinned-learning-revised", historicalSlug.Value.RedirectSlug);

        Result<LearningManifestResponse> manifest = await sender.Send(
            new GetLearningManifestQuery(learnerId, enrolled.Value.Id, "en"),
            cancellationToken);
        Assert.True(manifest.IsSuccess);
        LearningLessonSummaryResponse lesson = Assert.Single(Assert.Single(manifest.Value.Sections).Lessons);
        Result<IReadOnlyList<EnrollmentResponse>> fallbackEnrollments = await sender.Send(
            new GetEnrollmentsQuery(learnerId, "ar"),
            cancellationToken);
        Assert.Equal("Release Pinned Learning", Assert.Single(fallbackEnrollments.Value).Title);
        Result<LearningManifestResponse> fallbackManifest = await sender.Send(
            new GetLearningManifestQuery(learnerId, enrolled.Value.Id, "ar"),
            cancellationToken);
        Assert.Equal("Release Pinned Learning", fallbackManifest.Value.Title);
        Guid clientCommandId = Guid.CreateVersion7();
        var progressCommand = new UpdateLessonProgressCommand(
            learnerId,
            enrolled.Value.Id,
            lesson.Id,
            clientCommandId,
            1,
            0,
            [],
            true);

        Result<ProgressResponse> completed = await sender.Send(progressCommand, cancellationToken);
        Result<ProgressResponse> replayed = await sender.Send(progressCommand, cancellationToken);
        Assert.True(completed.IsSuccess);
        Assert.True(completed.Value.IsCompleted);
        Assert.Equal(completed.Value, replayed.Value);

        Result<CourseReleaseResponse> unpublished = await sender.Send(
            new UnpublishCourseCommand(
                reviewerId,
                created.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Course intentionally removed from discovery"),
            cancellationToken);
        Assert.True(unpublished.IsSuccess);
        Assert.Equal("Unpublished", unpublished.Value.State);

        Result<LearningManifestResponse> pinnedAccess = await sender.Send(
            new GetLearningManifestQuery(learnerId, enrolled.Value.Id, "en"),
            cancellationToken);
        Assert.True(pinnedAccess.IsSuccess);
        Assert.Equal(published.Value.ReleaseId, pinnedAccess.Value.ReleaseId);
        Assert.Equal("Completed", pinnedAccess.Value.Status);

        Result<EnrollmentResponse> blockedNewEnrollment = await sender.Send(
            new EnrollCourseCommand(
                secondLearnerId,
                created.Value.CourseId,
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(blockedNewEnrollment.IsSuccess);
        Assert.Equal("COURSE.NOT_PUBLISHED", blockedNewEnrollment.Failure.Code);
    }

    [Fact]
    public async Task QuizAndAssignment_CompleteThroughScoringSubmissionAndGradeRevision()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("phase8-assessment-reviewer", cancellationToken);
        Guid learnerId = await CreateConfirmedUserAsync("phase8-assessment-learner", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Intermediate",
                [new CourseLocalizationInput(
                    "en",
                    "Assessment Workflow",
                    "Quiz and assignment",
                    "A complete course that verifies objective scoring and append-only grading.")],
                [],
                []),
            cancellationToken);
        Assert.True(created.IsSuccess);
        Result<CourseMutationResponse> initialCurriculum = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                created.Value.CourseId,
                1,
                [new SectionInput(
                    null,
                    0,
                    "Assessment section",
                    [
                        new LessonInput(null, 0, "Knowledge check", "Quiz", string.Empty),
                        new LessonInput(null, 1, "Written exercise", "Assignment", "Explain the release contract."),
                    ])]),
            cancellationToken);
        Assert.True(initialCurriculum.IsSuccess);
        Result<CurriculumResponse> draft = await sender.Send(
            new GetCurriculumQuery(teacherId, created.Value.CourseId),
            cancellationToken);
        SectionResponse section = Assert.Single(draft.Value.Sections);
        LessonResponse quizLesson = section.Lessons.Single(lesson => lesson.LessonType == "Quiz");
        LessonResponse assignmentLesson = section.Lessons.Single(lesson => lesson.LessonType == "Assignment");

        Result<QuizVersionResponse> invalidQuiz = await sender.Send(
            new CreateQuizVersionCommand(
                teacherId,
                created.Value.CourseId,
                quizLesson.Id,
                "Invalid knowledge check",
                2,
                30,
                null,
                70,
                [new QuizQuestionInput(
                    0,
                    "SingleChoice",
                    "This question does not have enough options.",
                    10,
                    null,
                    [new QuizOptionInput(0, "Only option", true)])]),
            cancellationToken);
        Assert.False(invalidQuiz.IsSuccess);
        Assert.Equal("QUIZ.OPTIONS_INVALID", invalidQuiz.Failure.Code);

        Result<QuizVersionResponse> quiz = await sender.Send(
            new CreateQuizVersionCommand(
                teacherId,
                created.Value.CourseId,
                quizLesson.Id,
                "Release knowledge check",
                2,
                30,
                null,
                70,
                [new QuizQuestionInput(
                    0,
                    "SingleChoice",
                    "Which record pins the learner experience?",
                    10,
                    null,
                    [
                        new QuizOptionInput(0, "CourseRelease", true),
                        new QuizOptionInput(1, "CourseDraft", false),
                    ])]),
            cancellationToken);
        Assert.True(quiz.IsSuccess);
        Assert.Equal(1, quiz.Value.VersionNumber);
        Assert.True((await sender.Send(
            new MarkQuizVersionReadyCommand(teacherId, created.Value.CourseId, quiz.Value.VersionId),
            cancellationToken)).IsSuccess);

        Result<AssignmentVersionResponse> assignment = await sender.Send(
            new CreateAssignmentVersionCommand(
                teacherId,
                created.Value.CourseId,
                assignmentLesson.Id,
                "Release explanation",
                "Explain why an enrollment remains pinned after unpublish.",
                null,
                false),
            cancellationToken);
        Assert.True(assignment.IsSuccess);
        Assert.True((await sender.Send(
            new MarkAssignmentVersionReadyCommand(teacherId, created.Value.CourseId, assignment.Value.VersionId),
            cancellationToken)).IsSuccess);

        Result<CourseMutationResponse> linkedCurriculum = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                created.Value.CourseId,
                initialCurriculum.Value.DraftVersion,
                [new SectionInput(
                    section.Id,
                    0,
                    section.Title,
                    [
                        new LessonInput(
                            quizLesson.Id,
                            0,
                            quizLesson.Title,
                            quizLesson.LessonType,
                            quizLesson.Content,
                            QuizVersionId: quiz.Value.VersionId),
                        new LessonInput(
                            assignmentLesson.Id,
                            1,
                            assignmentLesson.Title,
                            assignmentLesson.LessonType,
                            assignmentLesson.Content,
                            AssignmentVersionId: assignment.Value.VersionId),
                    ])]),
            cancellationToken);
        Assert.True(linkedCurriculum.IsSuccess);
        Result<PublicationStatusResponse> review = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(review.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewPublicationCommand(reviewerId, review.Value.ReviewId!.Value, "approve", null),
            cancellationToken)).IsSuccess);
        Result<CourseReleaseResponse> release = await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                created.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Assessment definitions and content approved"),
            cancellationToken);
        Assert.True(release.IsSuccess);

        Result<EnrollmentResponse> enrollment = await sender.Send(
            new EnrollCourseCommand(
                learnerId,
                created.Value.CourseId,
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(enrollment.IsSuccess);
        Result<QuizAttemptResponse> attempt = await sender.Send(
            new StartQuizAttemptCommand(
                learnerId,
                enrollment.Value.Id,
                quiz.Value.VersionId,
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(attempt.IsSuccess);
        Dorosak.Infrastructure.Persistence.DorosakDbContext database = scope.ServiceProvider
            .GetRequiredService<Dorosak.Infrastructure.Persistence.DorosakDbContext>();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE assessment.quiz_attempts SET expires_at = CURRENT_TIMESTAMP - INTERVAL '1 minute' WHERE id = {attempt.Value.Id}",
            cancellationToken);
        database.ChangeTracker.Clear();
        Result<QuizAttemptResponse> replacementAttempt = await sender.Send(
            new StartQuizAttemptCommand(
                learnerId,
                enrollment.Value.Id,
                quiz.Value.VersionId,
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(replacementAttempt.IsSuccess);
        Assert.Equal(2, replacementAttempt.Value.AttemptNumber);
        Assert.NotEqual(attempt.Value.Id, replacementAttempt.Value.Id);
        Result<QuizAttemptResponse> expiredSubmission = await sender.Send(
            new SubmitQuizAttemptCommand(
                learnerId,
                enrollment.Value.Id,
                quiz.Value.VersionId,
                attempt.Value.Id,
                [],
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(expiredSubmission.IsSuccess);
        Assert.Equal("QUIZ.ATTEMPT_EXPIRED", expiredSubmission.Failure.Code);
        attempt = replacementAttempt;
        QuizAttemptQuestionResponse question = Assert.Single(attempt.Value.Questions);
        Guid correctOptionId = question.Options.Single(option => option.Text == "CourseRelease").Id;
        Result<QuizAttemptResponse> rejectedAnswer = await sender.Send(
            new SubmitQuizAttemptCommand(
                learnerId,
                enrollment.Value.Id,
                quiz.Value.VersionId,
                attempt.Value.Id,
                [new QuizAnswerInput(question.Id, null, [Guid.CreateVersion7()])],
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(rejectedAnswer.IsSuccess);
        Assert.Equal("QUIZ.ANSWER_INVALID", rejectedAnswer.Failure.Code);
        Result<QuizAttemptResponse> scored = await sender.Send(
            new SubmitQuizAttemptCommand(
                learnerId,
                enrollment.Value.Id,
                quiz.Value.VersionId,
                attempt.Value.Id,
                [new QuizAnswerInput(question.Id, null, [correctOptionId])],
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(scored.IsSuccess);
        Assert.Equal(100, scored.Value.Score);
        Assert.True(scored.Value.Passed);

        Result<GradeResponse> quizGrade = await sender.Send(
            new GradeQuizAttemptCommand(
                teacherId,
                created.Value.CourseId,
                attempt.Value.Id,
                95,
                "Manual review confirms the objective result.",
                "Reviewed quiz result for the learner"),
            cancellationToken);
        Assert.True(quizGrade.IsSuccess);
        Assert.Equal(1, quizGrade.Value.RevisionNumber);
        Result<GradeResponse> quizOverride = await sender.Send(
            new GradeQuizAttemptCommand(
                teacherId,
                created.Value.CourseId,
                attempt.Value.Id,
                85,
                "Second review adjusted the score.",
                "Adjusted quiz result after a second review"),
            cancellationToken);
        Assert.True(quizOverride.IsSuccess);
        Assert.Equal(2, quizOverride.Value.RevisionNumber);
        Assert.Equal("Second review adjusted the score.", quizOverride.Value.Feedback);

        Guid adminId = await CreateConfirmedUserAsync("phase8-grading-admin", cancellationToken);
        await using (AsyncServiceScope adminScope = fixture.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = adminScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser admin = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(adminId.ToString("D")));
            Assert.True((await userManager.AddToRoleAsync(
                admin,
                Dorosak.Infrastructure.Identity.IdentityConstants.AdminRole)).Succeeded);
        }
        Result<GradeResponse> adminOverride = await sender.Send(
            new GradeQuizAttemptCommand(
                adminId,
                created.Value.CourseId,
                attempt.Value.Id,
                90,
                "Administrative moderation review.",
                "Adjusted quiz result after moderation review"),
            cancellationToken);
        Assert.True(adminOverride.IsSuccess);
        Assert.Equal(3, adminOverride.Value.RevisionNumber);

        Result<AssignmentSubmissionResponse> submission = await sender.Send(
            new SubmitAssignmentCommand(
                learnerId,
                enrollment.Value.Id,
                assignment.Value.VersionId,
                "The enrollment stores the immutable release identifier.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(submission.IsSuccess);
        Result<GradeResponse> grade = await sender.Send(
            new GradeAssignmentCommand(
                teacherId,
                created.Value.CourseId,
                submission.Value.Id,
                90,
                "Correct and concise explanation.",
                "Reviewed the learner text submission"),
            cancellationToken);
        Assert.True(grade.IsSuccess);
        Assert.Equal(1, grade.Value.RevisionNumber);

        Result<LearningManifestResponse> completed = await sender.Send(
            new GetLearningManifestQuery(learnerId, enrollment.Value.Id, "en"),
            cancellationToken);
        Assert.True(completed.IsSuccess);
        Assert.Equal("Completed", completed.Value.Status);
        Assert.All(completed.Value.Sections.SelectMany(item => item.Lessons), lesson => Assert.True(lesson.IsCompleted));

        await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
        int quizRevisionCount = await verificationScope.ServiceProvider
            .GetRequiredService<Dorosak.Infrastructure.Persistence.DorosakDbContext>()
            .Set<Dorosak.Domain.Assessment.QuizGradeRevision>()
            .CountAsync(item => item.AttemptId == attempt.Value.Id, cancellationToken);
        Assert.Equal(3, quizRevisionCount);
    }

    private async Task<Guid> CreateApprovedTeacherAsync(CancellationToken cancellationToken)
    {
        Guid userId = await CreateConfirmedUserAsync("phase8-teacher", cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("phase8-teacher-reviewer", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> submitted = await sender.Send(
            new SubmitTeacherApplicationCommand(
                userId,
                "Release engineering instructor",
                "I build and teach production learning systems with immutable content releases.",
                "PostgreSQL, learning systems, and application security",
                "I want to teach reproducible and secure software practices."),
            cancellationToken);
        Assert.True(submitted.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewTeacherApplicationCommand(reviewerId, submitted.Value.Id, "start", null),
            cancellationToken)).IsSuccess);
        Assert.True((await sender.Send(
            new ReviewTeacherApplicationCommand(reviewerId, submitted.Value.Id, "approve", null),
            cancellationToken)).IsSuccess);
        return userId;
    }

    private async Task<Guid> CreateConfirmedUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        _ = cancellationToken;
        return user.Id;
    }
}
