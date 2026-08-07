using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dorosak.Application.IntegrationTests.Phase6;

[Collection(InfrastructureTestGroup.Name)]
public sealed class Phase6WorkflowTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task TeacherApplication_AllowsNewSubmissionAfterWithdrawalButNotWhileActive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateConfirmedUserAsync("teacher-applicant", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        SubmitTeacherApplicationCommand command = Application(userId);

        Result<TeacherApplicationResponse> first = await sender.Send(command, cancellationToken);
        Result<TeacherApplicationResponse> duplicate = await sender.Send(command, cancellationToken);
        Result<TeacherApplicationResponse> withdrawn = await sender.Send(
            new WithdrawTeacherApplicationCommand(userId),
            cancellationToken);
        Result<TeacherApplicationResponse> replacement = await sender.Send(command, cancellationToken);

        Assert.True(first.IsSuccess);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("TEACHER_APPLICATION.ACTIVE_EXISTS", duplicate.Failure.Code);
        Assert.Equal("Withdrawn", withdrawn.Value.Status);
        Assert.True(replacement.IsSuccess);
        Assert.NotEqual(first.Value.Id, replacement.Value.Id);
    }

    [Fact]
    public async Task TeacherApprovalAndWithdrawal_CannotCommitContradictoryState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid userId = await CreateConfirmedUserAsync("teacher-race-applicant", cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("teacher-race-reviewer", cancellationToken);
        Guid applicationId;
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<TeacherApplicationResponse> submitted = await sender.Send(Application(userId), cancellationToken);
            Assert.True(submitted.IsSuccess);
            applicationId = submitted.Value.Id;
            Assert.True((await sender.Send(
                new ReviewTeacherApplicationCommand(reviewerId, applicationId, "start", null),
                cancellationToken)).IsSuccess);
        }

        Task<Result<TeacherApplicationResponse>> approval = ExecuteAsync(
            sender => sender.Send(
                new ReviewTeacherApplicationCommand(reviewerId, applicationId, "approve", null),
                cancellationToken));
        Task<Result<TeacherApplicationResponse>> withdrawal = ExecuteAsync(
            sender => sender.Send(new WithdrawTeacherApplicationCommand(userId), cancellationToken));
        await Task.WhenAll(approval, withdrawal);

        await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
        ISender verificationSender = verificationScope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> current = await verificationSender.Send(
            new GetTeacherApplicationQuery(userId),
            cancellationToken);
        UserManager<ApplicationUser> userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(userId.ToString("D")));
        bool isTeacher = await userManager.IsInRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.TeacherRole);
        Assert.Equal(current.Value.Status == "Approved", isTeacher);
    }

    [Fact]
    public async Task DraftUpdate_UsesPostgresOptimisticConcurrencyAndReturnsCurrentEtag()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid courseId;
        await using (AsyncServiceScope scope = fixture.Services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<CourseMutationResponse> created = await sender.Send(
                new CreateCourseCommand(
                    teacherId,
                    "en",
                    "Beginner",
                    [new CourseLocalizationInput(
                        "en",
                        "Concurrency Course",
                        "Safe drafts",
                        "A complete plain-text course description.")],
                    ["technology"],
                    []),
                cancellationToken);
            Assert.True(created.IsSuccess);
            courseId = created.Value.CourseId;
        }

        Task<Result<CourseMutationResponse>> first = UpdateMetadataAsync(teacherId, courseId, "First update", cancellationToken);
        Task<Result<CourseMutationResponse>> second = UpdateMetadataAsync(teacherId, courseId, "Second update", cancellationToken);
        Result<CourseMutationResponse>[] results = await Task.WhenAll(first, second);

        Result<CourseMutationResponse> success = Assert.Single(results, result => result.IsSuccess);
        Result<CourseMutationResponse> conflict = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal(2, success.Value.DraftVersion);
        Assert.Equal("COURSE.VERSION_CONFLICT", conflict.Failure.Code);
        Assert.Equal("\"v2\"", conflict.Failure.ETag);
    }

    [Fact]
    public async Task CurriculumAndReview_StopAtReadyToPublishWithImmutableRevisions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("publication-reviewer", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "ar",
                "Beginner",
                [new CourseLocalizationInput(
                    "ar",
                    "هندسة البرمجيات العملية",
                    "مسودة خاصة",
                    "وصف مكتمل للمقرر قبل طلب مراجعته.")],
                ["technology"],
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
                    "المقدمة",
                    [new LessonInput(null, 0, "الدرس الأول", "Article", "محتوى نصي آمن.")])]),
            cancellationToken);
        Assert.True(curriculum.IsSuccess);
        Assert.Equal(2, curriculum.Value.DraftVersion);

        Result<PublicationStatusResponse> submitted = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(submitted.IsSuccess);
        Assert.Equal("InReview", submitted.Value.CourseStatus);

        Result<CurriculumResponse> firstCurriculum = await sender.Send(
            new GetCurriculumQuery(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(firstCurriculum.IsSuccess);
        SectionResponse section = Assert.Single(firstCurriculum.Value.Sections);
        LessonResponse lesson = Assert.Single(section.Lessons);
        Result<PublicationReviewResponse> changesRequested = await sender.Send(
            new ReviewPublicationCommand(
                reviewerId,
                submitted.Value.ReviewId!.Value,
                "changesRequested",
                "Expand the first lesson."),
            cancellationToken);
        Assert.True(changesRequested.IsSuccess);
        Result<CourseMutationResponse> revised = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                created.Value.CourseId,
                2,
                [new SectionInput(
                    section.Id,
                    0,
                    "المقدمة المحدثة",
                    [new LessonInput(lesson.Id, 0, "الدرس الأول المحدث", "Article", "محتوى نصي موسع وآمن.")])]),
            cancellationToken);
        Assert.True(revised.IsSuccess);
        Assert.Equal(3, revised.Value.DraftVersion);
        Result<PublicationStatusResponse> resubmitted = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(resubmitted.IsSuccess);
        Result<PublicationReviewResponse> approved = await sender.Send(
            new ReviewPublicationCommand(reviewerId, resubmitted.Value.ReviewId!.Value, "approve", null),
            cancellationToken);
        Assert.True(approved.IsSuccess);
        Result<PublicationStatusResponse> status = await sender.Send(
            new GetPublicationStatusQuery(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(status.IsSuccess);
        Assert.Equal("ReadyToPublish", status.Value.CourseStatus);
        Assert.Equal("Approved", status.Value.ReviewStatus);

        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT (SELECT count(*) FROM authoring.section_revisions WHERE section_id = @section_id) || '|' || (SELECT count(*) FROM authoring.lesson_revisions WHERE lesson_id = @lesson_id)",
            connection);
        command.Parameters.AddWithValue("section_id", section.Id);
        command.Parameters.AddWithValue("lesson_id", lesson.Id);
        Assert.Equal("2|2", Assert.IsType<string>(await command.ExecuteScalarAsync(cancellationToken)));
    }

    [Fact]
    public async Task TaxonomyCursor_UsesCodeAndIdSortKeys()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid actorId = await CreateConfirmedUserAsync("taxonomy-admin", cancellationToken);
        string suffix = Guid.CreateVersion7().ToString("N")[..8];
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        foreach (string code in new[] { $"alpha-{suffix}", $"beta-{suffix}" })
        {
            Result<TagResponse> created = await sender.Send(
                new UpsertTagCommand(
                    actorId,
                    null,
                    code,
                    true,
                    [
                        new TaxonomyLocalizationInput("ar", $"وسم {code}"),
                        new TaxonomyLocalizationInput("en", $"Tag {code}"),
                    ]),
                cancellationToken);
            Assert.True(created.IsSuccess);
        }

        Result<PagedResponse<TagResponse>> first = await sender.Send(
            new GetTagsQuery("en", 1, null),
            cancellationToken);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value.HasMore);
        Assert.NotNull(first.Value.NextCursor);
        Result<PagedResponse<TagResponse>> second = await sender.Send(
            new GetTagsQuery("en", 1, first.Value.NextCursor),
            cancellationToken);
        Assert.True(second.IsSuccess);
        Assert.Single(second.Value.Items);
        Assert.True(string.Compare(
            first.Value.Items[0].Code,
            second.Value.Items[0].Code,
            StringComparison.Ordinal) < 0);
    }

    [Fact]
    public async Task InactiveTaxonomy_IsHiddenPubliclyAndVisibleToAdministration()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid actorId = await CreateConfirmedUserAsync("inactive-taxonomy-admin", cancellationToken);
        string code = $"inactive-{Guid.CreateVersion7():N}";
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TagResponse> created = await sender.Send(
            new UpsertTagCommand(
                actorId,
                null,
                code,
                false,
                [
                    new TaxonomyLocalizationInput("ar", "وسم غير نشط"),
                    new TaxonomyLocalizationInput("en", "Inactive tag"),
                ]),
            cancellationToken);
        Assert.True(created.IsSuccess);

        Result<PagedResponse<TagResponse>> publicResult = await sender.Send(
            new GetTagsQuery("en", 100, null),
            cancellationToken);
        Result<PagedResponse<TagResponse>> administrativeResult = await sender.Send(
            new GetTagsQuery("en", 100, null, IncludeInactive: true),
            cancellationToken);

        Assert.True(publicResult.IsSuccess);
        Assert.DoesNotContain(publicResult.Value.Items, tag => tag.Code == code);
        Assert.True(administrativeResult.IsSuccess);
        Assert.Contains(administrativeResult.Value.Items, tag => tag.Code == code && !tag.IsActive);
    }

    [Fact]
    public async Task SlugRotation_RetainsPermanentHistoricalSlug()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput("en", "Original Slug", "Draft", "Original description.")],
                [],
                []),
            cancellationToken);
        Assert.True(created.IsSuccess);
        Result<CourseMutationResponse> updated = await sender.Send(
            new UpdateCourseMetadataCommand(
                teacherId,
                created.Value.CourseId,
                1,
                "en",
                "Beginner",
                [new CourseLocalizationInput("en", "Replacement Slug", "Draft", "Updated description.")],
                [],
                []),
            cancellationToken);
        Assert.True(updated.IsSuccess);

        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) || '|' || count(*) FILTER (WHERE is_current) FROM catalog.course_slugs WHERE course_id = @course_id",
            connection);
        command.Parameters.AddWithValue("course_id", created.Value.CourseId);
        Assert.Equal("2|1", Assert.IsType<string>(await command.ExecuteScalarAsync(cancellationToken)));
    }

    [Fact]
    public async Task FailedOwnershipTransfer_DoesNotAdvanceDraftVersion()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput("en", "Ownership Course", "Draft", "Ownership test draft.")],
                [],
                []),
            cancellationToken);
        Assert.True(created.IsSuccess);

        Result<CourseMutationResponse> transfer = await sender.Send(
            new TransferCourseOwnershipCommand(teacherId, created.Value.CourseId, teacherId, 1),
            cancellationToken);
        Result<CourseDetailsResponse> details = await sender.Send(
            new GetCourseQuery(teacherId, created.Value.CourseId),
            cancellationToken);

        Assert.False(transfer.IsSuccess);
        Assert.Equal("COURSE.OWNER_UNCHANGED", transfer.Failure.Code);
        Assert.True(details.IsSuccess);
        Assert.Equal(1, details.Value.DraftVersion);
    }

    private async Task<Result<CourseMutationResponse>> UpdateMetadataAsync(
        Guid teacherId,
        Guid courseId,
        string description,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(
            new UpdateCourseMetadataCommand(
                teacherId,
                courseId,
                1,
                "en",
                "Beginner",
                [new CourseLocalizationInput("en", "Concurrency Course", "Safe drafts", description)],
                ["technology"],
                []),
            cancellationToken);
    }

    private async Task<Result<TeacherApplicationResponse>> ExecuteAsync(
        Func<ISender, Task<Result<TeacherApplicationResponse>>> operation)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    private async Task<Guid> CreateApprovedTeacherAsync(CancellationToken cancellationToken)
    {
        Guid userId = await CreateConfirmedUserAsync("approved-teacher", cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("teacher-reviewer", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> submitted = await sender.Send(Application(userId), cancellationToken);
        Assert.True(submitted.IsSuccess);
        Result<TeacherApplicationResponse> started = await sender.Send(
            new ReviewTeacherApplicationCommand(reviewerId, submitted.Value.Id, "start", null),
            cancellationToken);
        Assert.True(started.IsSuccess);
        Result<TeacherApplicationResponse> approved = await sender.Send(
            new ReviewTeacherApplicationCommand(reviewerId, submitted.Value.Id, "approve", null),
            cancellationToken);
        Assert.True(approved.IsSuccess);
        return userId;
    }

    private async Task<Guid> CreateConfirmedUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string email = $"{prefix}-{Guid.CreateVersion7():N}@example.test";
        ApplicationUser user = ApplicationUser.Create(prefix, email, DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        IdentityResult created = await userManager.CreateAsync(user, "correct horse battery staple");
        Assert.True(created.Succeeded);
        IdentityResult assigned = await userManager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Assert.True(assigned.Succeeded);
        _ = cancellationToken;
        return user.Id;
    }

    private static SubmitTeacherApplicationCommand Application(Guid userId) => new(
        userId,
        "Practical software instructor",
        "I have extensive experience building production software.",
        "PostgreSQL, backend engineering, and application security",
        "I want to provide practical Arabic-first engineering courses.");
}
