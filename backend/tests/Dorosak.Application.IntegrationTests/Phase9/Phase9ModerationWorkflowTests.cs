using System.Security.Claims;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Moderation;
using Dorosak.Application.Features.Phase6;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Phase9;

[Collection(InfrastructureTestGroup.Name)]
public sealed class Phase9ModerationWorkflowTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Reports_EnforceOwnershipPermissionIdempotencyAndAppendOnlyActions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid reporterId = await CreateUserAsync(
            "phase9-report-reporter",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid outsiderId = await CreateUserAsync(
            "phase9-report-outsider",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid adminId = await CreateUserAsync(
            "phase9-report-admin",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        await GrantModerationPermissionAsync(adminId);
        Guid courseId = await CreatePublishedCourseAsync(cancellationToken);
        string reportKey = Guid.CreateVersion7().ToString("N");
        var createReport = new CreateContentReportCommand(
            reporterId,
            courseId,
            null,
            null,
            null,
            null,
            "Harassment",
            "Synthetic report details for a moderation workflow.",
            reportKey);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<ContentReportResponse> created = await sender.Send(createReport, cancellationToken);
        Result<ContentReportResponse> replayed = await sender.Send(createReport, cancellationToken);

        Assert.True(created.IsSuccess);
        Assert.Equal(created.Value.Id, replayed.Value.Id);
        Assert.Equal("Course", created.Value.TargetKind);
        Assert.Equal("Open", created.Value.Status);

        Result<ContentReportResponse> duplicate = await sender.Send(
            createReport with { IdempotencyKey = Guid.CreateVersion7().ToString("N") },
            cancellationToken);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("REPORT.ALREADY_OPEN", duplicate.Failure.Code);

        Result<ContentReportResponse> foreignRead = await sender.Send(
            new GetMyContentReportQuery(outsiderId, created.Value.Id),
            cancellationToken);
        Assert.False(foreignRead.IsSuccess);
        Assert.Equal("MODERATION.NOT_FOUND", foreignRead.Failure.Code);

        Result<ContentReportPageResponse> deniedQueue = await sender.Send(
            new GetAdminContentReportsQuery(reporterId, null, null, 20, null),
            cancellationToken);
        Assert.False(deniedQueue.IsSuccess);
        Assert.Equal("MODERATION.PERMISSION_REQUIRED", deniedQueue.Failure.Code);

        Result<ModerationCasePageResponse> cases = await sender.Send(
            new GetModerationCasesQuery(adminId, "Open", 20, null),
            cancellationToken);
        ModerationCaseSummaryResponse moderationCase = Assert.Single(
            cases.Value.Items,
            item => item.ReportId == created.Value.Id);
        string startKey = Guid.CreateVersion7().ToString("N");
        var startReview = new ApplyModerationActionCommand(
            adminId,
            moderationCase.Id,
            "StartReview",
            "Reviewing the synthetic account report.",
            moderationCase.Version,
            "Synthetic integration moderation audit.",
            startKey);
        Result<ModerationCaseResponse> started = await sender.Send(startReview, cancellationToken);
        Result<ModerationCaseResponse> replayedStart = await sender.Send(startReview, cancellationToken);

        Assert.Equal("InReview", started.Value.Case.Status);
        Assert.Equal(adminId, started.Value.Case.AssignedToUserId);
        Assert.Single(started.Value.Actions);
        Assert.Single(replayedStart.Value.Actions);

        Result<ModerationCaseResponse> unsupported = await sender.Send(
            new ApplyModerationActionCommand(
                adminId,
                moderationCase.Id,
                "HideContent",
                "Account visibility belongs to Identity.",
                started.Value.Case.Version,
                "Synthetic unsupported action audit.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(unsupported.IsSuccess);
        Assert.Equal("MODERATION.TARGET_ACTION_UNSUPPORTED", unsupported.Failure.Code);

        Result<ModerationCaseResponse> dismissed = await sender.Send(
            new ApplyModerationActionCommand(
                adminId,
                moderationCase.Id,
                "Dismiss",
                "The synthetic report lacks actionable evidence.",
                started.Value.Case.Version,
                "Synthetic dismissal audit reason.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(dismissed.IsSuccess);
        Assert.Equal("Dismissed", dismissed.Value.Case.Status);
        Assert.Equal("Dismissed", dismissed.Value.Case.Report.Report.Status);
        Assert.Equal(2, dismissed.Value.Actions.Count);

        var raceOne = new CreateContentReportCommand(
            reporterId,
            courseId,
            null,
            null,
            null,
            null,
            "Spam",
            null,
            Guid.CreateVersion7().ToString("N"));
        CreateContentReportCommand raceTwo = raceOne with { IdempotencyKey = Guid.CreateVersion7().ToString("N") };
        Result<ContentReportResponse>[] raced = await Task.WhenAll(
            SendInNewScopeAsync(raceOne, cancellationToken),
            SendInNewScopeAsync(raceTwo, cancellationToken));
        Assert.Single(raced, result => result.IsSuccess);
        Assert.Single(raced, result => !result.IsSuccess && result.Failure.Code == "REPORT.ALREADY_OPEN");

        await using AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        Assert.Equal(2, await dbContext.Set<ModerationAction>().CountAsync(
            action => action.CaseId == moderationCase.Id,
            cancellationToken));
        Assert.Equal(2, await dbContext.Set<AuditLog>().CountAsync(
            audit => audit.ActorUserId == adminId && audit.TargetId == moderationCase.Id &&
                audit.Action.StartsWith("moderation.action-"),
            cancellationToken));
    }

    private async Task<Result<ContentReportResponse>> SendInNewScopeAsync(
        CreateContentReportCommand command,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private async Task<Guid> CreateUserAsync(string prefix, string role)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }

    private async Task<Guid> CreatePublishedCourseAsync(CancellationToken cancellationToken)
    {
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid reviewerId = await CreateUserAsync(
            "phase9-moderation-publication-reviewer",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> course = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    "Moderation Safety",
                    "Synthetic report target",
                    "A synthetic published course used by the moderation workflow.")],
                [],
                []),
            cancellationToken);
        Assert.True(course.IsSuccess);
        Result<CourseMutationResponse> curriculum = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                course.Value.CourseId,
                1,
                [new SectionInput(
                    null,
                    0,
                    "Moderation section",
                    [new LessonInput(null, 0, "Moderation lesson", "Article", "Synthetic lesson body.")])]),
            cancellationToken);
        Assert.True(curriculum.IsSuccess);
        Result<PublicationStatusResponse> publication = await sender.Send(
            new RequestPublicationCommand(teacherId, course.Value.CourseId),
            cancellationToken);
        Assert.True(publication.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewPublicationCommand(reviewerId, publication.Value.ReviewId!.Value, "approve", null),
            cancellationToken)).IsSuccess);
        Assert.True((await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                course.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Synthetic moderation release approved"),
            cancellationToken)).IsSuccess);
        return course.Value.CourseId;
    }

    private async Task<Guid> CreateApprovedTeacherAsync(CancellationToken cancellationToken)
    {
        Guid userId = await CreateUserAsync(
            "phase9-moderation-teacher",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid reviewerId = await CreateUserAsync(
            "phase9-moderation-teacher-reviewer",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> submitted = await sender.Send(
            new SubmitTeacherApplicationCommand(
                userId,
                "Moderation systems instructor",
                "I teach safe online collaboration and reporting workflows.",
                "Application security and moderation",
                "I want to teach healthy community practices."),
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

    private async Task GrantModerationPermissionAsync(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(userId.ToString()));
        Assert.True((await userManager.AddClaimAsync(
            user,
            new Claim(
                Dorosak.Infrastructure.Identity.IdentityConstants.PermissionClaimType,
                Permissions.ModerationReviewAny))).Succeeded);
    }
}
