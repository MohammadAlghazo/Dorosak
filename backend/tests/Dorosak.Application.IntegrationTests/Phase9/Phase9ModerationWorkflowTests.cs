using System.Security.Claims;
using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Commerce;
using Dorosak.Application.Features.Communications;
using Dorosak.Application.Features.Engagement;
using Dorosak.Application.Features.Moderation;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Learning;
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
        Result<ContentReportPageResponse> reports = await sender.Send(
            new GetAdminContentReportsQuery(adminId, "Open", "Course", 20, null),
            cancellationToken);
        Assert.Single(reports.Value.Items, item => item.Report.Id == created.Value.Id);
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

    [Fact]
    public async Task MessageReports_RequireCurrentReadAccessAndKeepAnImmutableModeratorSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid senderId = await CreateUserAsync(
            "phase9-message-report-sender",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid reporterId = await CreateUserAsync(
            "phase9-message-report-reporter",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid revokedParticipantId = await CreateUserAsync(
            "phase9-message-report-revoked",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid outsiderId = await CreateUserAsync(
            "phase9-message-report-outsider",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid adminId = await CreateUserAsync(
            "phase9-message-report-admin",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        await GrantModerationPermissionAsync(adminId);
        Guid courseId = await CreatePublishedCourseAsync(cancellationToken);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        foreach (Guid userId in new[] { senderId, reporterId, revokedParticipantId, outsiderId })
        {
            Result<DemoCheckoutResponse> checkout = await sender.Send(
                new CreateDemoCheckoutCommand(
                    userId,
                    courseId,
                    "success",
                    "en",
                    Guid.CreateVersion7().ToString("N")),
                cancellationToken);
            Assert.True(checkout.IsSuccess);
        }

        Result<ConversationResponse> conversation = await sender.Send(
            new CreateConversationCommand(
                senderId,
                [reporterId, revokedParticipantId],
                courseId,
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(conversation.IsSuccess);
        const string messageBody = "Immutable message body preserved for the moderation case.";
        Result<MessageResponse> message = await sender.Send(
            new CreateMessageCommand(
                senderId,
                conversation.Value.Id,
                Guid.CreateVersion7(),
                messageBody,
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Result<MessageResponse> secondMessage = await sender.Send(
            new CreateMessageCommand(
                senderId,
                conversation.Value.Id,
                Guid.CreateVersion7(),
                "A second immutable message verifies the idempotency payload.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(message.IsSuccess);
        Assert.True(secondMessage.IsSuccess);

        Result<ContentReportResponse> selfReport = await sender.Send(
            MessageReportCommand(senderId, message.Value.Id, Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(selfReport.IsSuccess);
        Assert.Equal("MODERATION.NOT_FOUND", selfReport.Failure.Code);

        Result<ContentReportResponse> outsiderReport = await sender.Send(
            MessageReportCommand(outsiderId, message.Value.Id, Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(outsiderReport.IsSuccess);
        Assert.Equal("MODERATION.NOT_FOUND", outsiderReport.Failure.Code);

        string idempotencyKey = Guid.CreateVersion7().ToString("N");
        CreateContentReportCommand createReport = MessageReportCommand(
            reporterId,
            message.Value.Id,
            idempotencyKey);
        Result<ContentReportResponse> created = await sender.Send(createReport, cancellationToken);
        Result<ContentReportResponse> replayed = await sender.Send(createReport, cancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Equal(created.Value.Id, replayed.Value.Id);
        Assert.Equal("Message", created.Value.TargetKind);
        Assert.Equal(message.Value.Id, created.Value.TargetId);

        RequestConflictException reusedKey = await Assert.ThrowsAsync<RequestConflictException>(() => sender.Send(
            createReport with { MessageId = secondMessage.Value.Id },
            cancellationToken));
        Assert.Equal("IDEMPOTENCY.KEY_REUSED", reusedKey.Code);
        Result<ContentReportResponse> duplicate = await sender.Send(
            createReport with { IdempotencyKey = Guid.CreateVersion7().ToString("N") },
            cancellationToken);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("REPORT.ALREADY_OPEN", duplicate.Failure.Code);

        Result<ContentReportPageResponse> reports = await sender.Send(
            new GetAdminContentReportsQuery(adminId, "Open", "Message", 100, null),
            cancellationToken);
        AdminContentReportResponse adminReport = Assert.Single(
            reports.Value.Items,
            item => item.Report.Id == created.Value.Id);
        MessageReportSnapshotResponse snapshot = Assert.IsType<MessageReportSnapshotResponse>(adminReport.MessageSnapshot);
        Assert.Equal(senderId, snapshot.SenderUserId);
        Assert.Equal("phase9-message-report-sender", snapshot.SenderName);
        Assert.Equal(courseId, snapshot.CourseId);
        Assert.Equal(conversation.Value.Id, snapshot.ConversationId);
        Assert.Equal(message.Value.Sequence, snapshot.Sequence);
        Assert.Equal(messageBody, snapshot.Body);

        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        await dbContext.Users.Where(user => user.Id == senderId).ExecuteUpdateAsync(
            setters => setters.SetProperty(user => user.DisplayName, "Changed sender name"),
            cancellationToken);
        await dbContext.CourseLocalizations.Where(localization => localization.CourseId == courseId).ExecuteUpdateAsync(
            setters => setters.SetProperty(localization => localization.Title, "Changed course title"),
            cancellationToken);
        Result<ModerationCaseResponse> moderationCase = await sender.Send(
            new GetModerationCaseQuery(adminId, adminReport.CaseId),
            cancellationToken);
        Assert.Equal(messageBody, moderationCase.Value.TargetPreview.Body);
        Assert.Equal(snapshot.SenderName, moderationCase.Value.TargetPreview.AuthorName);
        Assert.Equal(snapshot.CourseTitle, moderationCase.Value.TargetPreview.Title);
        Assert.Equal(snapshot, moderationCase.Value.Case.Report.MessageSnapshot);

        Entitlement entitlement = await dbContext.Set<Entitlement>().SingleAsync(
            item => item.UserId == revokedParticipantId && item.CourseId == courseId,
            cancellationToken);
        entitlement.Revoke(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        Result<ContentReportResponse> revokedAccess = await sender.Send(
            MessageReportCommand(revokedParticipantId, message.Value.Id, Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(revokedAccess.IsSuccess);
        Assert.Equal("MODERATION.NOT_FOUND", revokedAccess.Failure.Code);

        Assert.True((await sender.Send(
            new LeaveConversationCommand(reporterId, conversation.Value.Id),
            cancellationToken)).IsSuccess);
        Result<ContentReportResponse> formerParticipant = await sender.Send(
            createReport with { IdempotencyKey = Guid.CreateVersion7().ToString("N") },
            cancellationToken);
        Assert.False(formerParticipant.IsSuccess);
        Assert.Equal("MODERATION.NOT_FOUND", formerParticipant.Failure.Code);
    }

    [Fact]
    public async Task ReviewModeration_UsesVersionedLocksAndCannotBeBypassedByTheAuthor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid learnerId = await CreateUserAsync(
            "phase9-moderated-review-author",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid reporterId = await CreateUserAsync(
            "phase9-moderated-review-reporter",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        Guid moderatorId = await CreateUserAsync(
            "phase9-moderated-review-moderator",
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole);
        await GrantModerationPermissionAsync(moderatorId);
        Guid courseId = await CreatePublishedCourseAsync(cancellationToken);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.True((await sender.Send(
            new CreateDemoCheckoutCommand(
                learnerId,
                courseId,
                "success",
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken)).IsSuccess);
        Result<CourseReviewResponse> review = await sender.Send(
            new CreateCourseReviewCommand(
                learnerId,
                courseId,
                4,
                "Synthetic review content that requires moderation.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(review.IsSuccess);
        Result<ContentReportResponse> report = await sender.Send(
            new CreateContentReportCommand(
                reporterId,
                null,
                review.Value.Id,
                null,
                null,
                null,
                null,
                "Harassment",
                "Synthetic report details for the review target.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(report.IsSuccess);
        ModerationCaseSummaryResponse moderationCase = Assert.Single(
            (await sender.Send(
                new GetModerationCasesQuery(moderatorId, "Open", 100, null),
                cancellationToken)).Value.Items,
            item => item.ReportId == report.Value.Id);

        var invalidAction = new ApplyModerationActionCommand(
            moderatorId,
            moderationCase.Id,
            "3",
            "Synthetic invalid action reason.",
            moderationCase.Version,
            "Synthetic invalid action audit.",
            Guid.CreateVersion7().ToString("N"));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => sender.Send(invalidAction, cancellationToken));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => sender.Send(
            invalidAction with
            {
                Action = "StartReview",
                Reason = "        ",
                IdempotencyKey = Guid.CreateVersion7().ToString("N"),
            },
            cancellationToken));

        Result<ModerationCaseResponse> started = await sender.Send(
            new ApplyModerationActionCommand(
                moderatorId,
                moderationCase.Id,
                "StartReview",
                "Starting the synthetic review investigation.",
                moderationCase.Version,
                "Synthetic review investigation audit.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.Equal(2, started.Value.Case.Version);

        var hide = new ApplyModerationActionCommand(
            moderatorId,
            moderationCase.Id,
            "HideContent",
            "Hiding the review while the report is investigated.",
            started.Value.Case.Version,
            "Synthetic review visibility audit.",
            Guid.CreateVersion7().ToString("N"));
        Result<ModerationCaseResponse>[] concurrent = await Task.WhenAll(
            SendActionInNewScopeAsync(hide, cancellationToken),
            SendActionInNewScopeAsync(
                hide with { IdempotencyKey = Guid.CreateVersion7().ToString("N") },
                cancellationToken));
        Result<ModerationCaseResponse> hidden = Assert.Single(concurrent, result => result.IsSuccess);
        Assert.Single(concurrent, result =>
            !result.IsSuccess && result.Failure.Code == "MODERATION.VERSION_CONFLICT");
        Assert.Equal("Hidden", hidden.Value.TargetPreview.Status);
        Assert.Equal(3, hidden.Value.Case.Version);

        Result<ModerationCaseResponse> repeatedHide = await SendActionInNewScopeAsync(
            hide with
            {
                ExpectedVersion = hidden.Value.Case.Version,
                IdempotencyKey = Guid.CreateVersion7().ToString("N"),
            },
            cancellationToken);
        Assert.True(repeatedHide.IsSuccess);
        Assert.Equal(4, repeatedHide.Value.Case.Version);
        Assert.Equal(3, repeatedHide.Value.Actions.Count);

        Result<EngagementOperationResponse> authorDelete = await SendDeleteReviewInNewScopeAsync(
            new DeleteCourseReviewCommand(learnerId, courseId, review.Value.Id),
            cancellationToken);
        Assert.False(authorDelete.IsSuccess);
        Assert.Equal("REVIEW.NOT_REMOVABLE", authorDelete.Failure.Code);

        Result<ModerationCaseResponse> staleRestore = await SendActionInNewScopeAsync(
            new ApplyModerationActionCommand(
                moderatorId,
                moderationCase.Id,
                "RestoreContent",
                "Restoring the synthetic review after investigation.",
                hidden.Value.Case.Version,
                "Synthetic stale restore audit.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.False(staleRestore.IsSuccess);
        Assert.Equal("MODERATION.VERSION_CONFLICT", staleRestore.Failure.Code);

        Result<ModerationCaseResponse> restored = await SendActionInNewScopeAsync(
            new ApplyModerationActionCommand(
                moderatorId,
                moderationCase.Id,
                "RestoreContent",
                "Restoring the synthetic review after investigation.",
                repeatedHide.Value.Case.Version,
                "Synthetic approved restore audit.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(restored.IsSuccess);
        Assert.Equal("Published", restored.Value.TargetPreview.Status);
        Assert.Equal(5, restored.Value.Case.Version);
        Assert.Equal(4, restored.Value.Actions.Count);
    }

    private async Task<Result<ContentReportResponse>> SendInNewScopeAsync(
        CreateContentReportCommand command,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private static CreateContentReportCommand MessageReportCommand(
        Guid userId,
        Guid messageId,
        string idempotencyKey) => new(
        userId,
        null,
        null,
        null,
        null,
        messageId,
        null,
        "Harassment",
        "Synthetic report details for a conversation message.",
        idempotencyKey);

    private async Task<Result<ModerationCaseResponse>> SendActionInNewScopeAsync(
        ApplyModerationActionCommand command,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private async Task<Result<EngagementOperationResponse>> SendDeleteReviewInNewScopeAsync(
        DeleteCourseReviewCommand command,
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
        string suffix = Guid.CreateVersion7().ToString("N")[24..];
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> course = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    $"Moderation Safety {suffix}",
                    "Synthetic report target",
                    "A synthetic published course used by the moderation workflow.")],
                [],
                []),
            cancellationToken);
        Assert.True(
            course.IsSuccess,
            course.IsSuccess ? string.Empty : $"{course.Failure.Code}: {course.Failure.Description}");
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

