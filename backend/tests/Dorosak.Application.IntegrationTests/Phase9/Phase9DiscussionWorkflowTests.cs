using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Commerce;
using Dorosak.Application.Features.Engagement;
using Dorosak.Application.Features.Learning;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;
using Dorosak.Application.Features.Publishing;
using Dorosak.Domain.Learning;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Phase9;

[Collection(InfrastructureTestGroup.Name)]
public sealed class Phase9DiscussionWorkflowTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Discussions_EnforceScopeDepthIdempotencyAndUniqueLikes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid teacherId = await CreateApprovedTeacherAsync(cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("phase9-discussion-reviewer", cancellationToken);
        Guid learnerId = await CreateConfirmedUserAsync("phase9-discussion-learner", cancellationToken);
        Guid secondLearnerId = await CreateConfirmedUserAsync("phase9-discussion-second", cancellationToken);
        Guid outsiderId = await CreateConfirmedUserAsync("phase9-discussion-outsider", cancellationToken);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> course = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    "Discussion Security",
                    "Release-scoped participation",
                    "A synthetic course used to verify the Phase 9 discussion contract.")],
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
                    "Discussion section",
                    [new LessonInput(
                        null,
                        0,
                        "Discuss the release",
                        "Article",
                        "Synthetic lesson content for discussion tests.")])]),
            cancellationToken);
        Assert.True(curriculum.IsSuccess);
        Result<PublicationStatusResponse> publication = await sender.Send(
            new RequestPublicationCommand(teacherId, course.Value.CourseId),
            cancellationToken);
        Assert.True(publication.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewPublicationCommand(reviewerId, publication.Value.ReviewId!.Value, "approve", null),
            cancellationToken)).IsSuccess);
        Result<CourseReleaseResponse> release = await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                course.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "Synthetic discussion release approved"),
            cancellationToken);
        Assert.True(release.IsSuccess);

        Assert.True((await sender.Send(
            new CreateDemoCheckoutCommand(
                learnerId,
                course.Value.CourseId,
                "success",
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken)).IsSuccess);
        Assert.True((await sender.Send(
            new CreateDemoCheckoutCommand(
                secondLearnerId,
                course.Value.CourseId,
                "success",
                "en",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken)).IsSuccess);

        EnrollmentResponse learnerEnrollment = Assert.Single((await sender.Send(
            new GetEnrollmentsQuery(learnerId, "en"),
            cancellationToken)).Value);
        EnrollmentResponse secondEnrollment = Assert.Single((await sender.Send(
            new GetEnrollmentsQuery(secondLearnerId, "en"),
            cancellationToken)).Value);
        LearningManifestResponse manifest = (await sender.Send(
            new GetLearningManifestQuery(learnerId, learnerEnrollment.Id, "en"),
            cancellationToken)).Value;
        Guid lessonId = Assert.Single(Assert.Single(manifest.Sections).Lessons).Id;
        DiscussionScope learnerScope = DiscussionScope.ForEnrollment(learnerEnrollment.Id, lessonId);
        string threadKey = Guid.CreateVersion7().ToString("N");
        var createThread = new CreateDiscussionThreadCommand(
            learnerId,
            learnerScope,
            "How is this discussion scoped?",
            "It should stay attached to the enrollment's pinned release.",
            threadKey);

        Result<DiscussionThreadResponse> thread = await sender.Send(createThread, cancellationToken);
        Result<DiscussionThreadResponse> replayedThread = await sender.Send(createThread, cancellationToken);
        Assert.True(thread.IsSuccess);
        Assert.Equal(thread.Value.Id, replayedThread.Value.Id);
        Assert.Equal(lessonId, thread.Value.LessonId);
        Assert.True(thread.Value.CanEdit);
        Assert.True(thread.Value.CanDelete);

        DiscussionScope instructorScope = DiscussionScope.ForInstructor(
            course.Value.CourseId,
            release.Value.ReleaseId,
            lessonId);
        Result<DiscussionThreadPageResponse> instructorPage = await sender.Send(
            new GetDiscussionThreadsQuery(teacherId, instructorScope, 20, null),
            cancellationToken);
        Assert.Contains(instructorPage.Value.Items, item => item.Id == thread.Value.Id);

        Result<DiscussionThreadResponse> courseThread = await sender.Send(
            new CreateDiscussionThreadCommand(
                teacherId,
                DiscussionScope.ForInstructor(course.Value.CourseId, release.Value.ReleaseId, null),
                "Course-wide announcement question",
                "This thread is not tied to one lesson.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.True(courseThread.IsSuccess);
        Assert.Null(courseThread.Value.LessonId);
        Result<DiscussionThreadPageResponse> learnerCoursePage = await sender.Send(
            new GetDiscussionThreadsQuery(
                learnerId,
                DiscussionScope.ForEnrollment(learnerEnrollment.Id, null),
                20,
                null),
            cancellationToken);
        Assert.Contains(learnerCoursePage.Value.Items, item => item.Id == courseThread.Value.Id);

        ResourceNotFoundException denied = await Assert.ThrowsAsync<ResourceNotFoundException>(() => sender.Send(
            new GetDiscussionThreadsQuery(
                outsiderId,
                DiscussionScope.ForEnrollment(learnerEnrollment.Id, lessonId),
                20,
                null),
            cancellationToken));
        Assert.Equal("DISCUSSION.NOT_FOUND", denied.Code);

        var rootCommand = new CreateDiscussionCommentCommand(
            learnerId,
            learnerScope,
            thread.Value.Id,
            null,
            "Root answer",
            Guid.CreateVersion7().ToString("N"));
        Result<DiscussionCommentResponse> root = await sender.Send(rootCommand, cancellationToken);
        Result<DiscussionCommentResponse> reply = await sender.Send(
            new CreateDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                root.Value.Id,
                "First nested reply",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Result<DiscussionCommentResponse> nested = await sender.Send(
            new CreateDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                reply.Value.Id,
                "Second nested reply",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Result<DiscussionCommentResponse> tooDeep = await sender.Send(
            new CreateDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                nested.Value.Id,
                "This reply exceeds the configured depth.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        Assert.Equal(0, root.Value.Depth);
        Assert.Equal(1, reply.Value.Depth);
        Assert.Equal(2, nested.Value.Depth);
        Assert.False(tooDeep.IsSuccess);
        Assert.Equal("COMMENT.DEPTH_LIMIT", tooDeep.Failure.Code);

        Result<CommentLikeResponse> firstLike = await sender.Send(
            new LikeDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                nested.Value.Id),
            cancellationToken);
        Result<CommentLikeResponse> repeatedLike = await sender.Send(
            new LikeDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                nested.Value.Id),
            cancellationToken);
        Result<CommentLikeResponse> secondLike = await sender.Send(
            new LikeDiscussionCommentCommand(
                secondLearnerId,
                DiscussionScope.ForEnrollment(secondEnrollment.Id, lessonId),
                thread.Value.Id,
                nested.Value.Id),
            cancellationToken);
        Assert.Equal(1, firstLike.Value.LikeCount);
        Assert.Equal(1, repeatedLike.Value.LikeCount);
        Assert.Equal(2, secondLike.Value.LikeCount);

        Result<DiscussionCommentResponse> concurrentComment = await sender.Send(
            new CreateDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                null,
                "A comment used to verify concurrent duplicate likes.",
                Guid.CreateVersion7().ToString("N")),
            cancellationToken);
        var concurrentLike = new LikeDiscussionCommentCommand(
            learnerId,
            learnerScope,
            thread.Value.Id,
            concurrentComment.Value.Id);
        Result<CommentLikeResponse>[] concurrentLikes = await Task.WhenAll(
            SendLikeInNewScopeAsync(concurrentLike, cancellationToken),
            SendLikeInNewScopeAsync(concurrentLike, cancellationToken));
        Assert.All(concurrentLikes, result =>
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.LikeCount);
        });
        await using (AsyncServiceScope verificationScope = fixture.Services.CreateAsyncScope())
        {
            int persistedLikes = await verificationScope.ServiceProvider
                .GetRequiredService<DorosakDbContext>()
                .Set<Dorosak.Domain.Engagement.CommentLike>()
                .CountAsync(
                    like => like.CommentId == concurrentComment.Value.Id && like.UserId == learnerId,
                    cancellationToken);
            Assert.Equal(1, persistedLikes);
        }

        Assert.True((await sender.Send(
            new DeleteDiscussionCommentCommand(
                learnerId,
                learnerScope,
                thread.Value.Id,
                root.Value.Id),
            cancellationToken)).IsSuccess);
        Result<DiscussionCommentResponse> removedReplay = await sender.Send(rootCommand, cancellationToken);
        Assert.Equal("Removed", removedReplay.Value.Status);
        Assert.Empty(removedReplay.Value.Body);
        Assert.Equal(Guid.Empty, removedReplay.Value.AuthorUserId);
        Assert.False(removedReplay.Value.CanEdit);
        Assert.False(removedReplay.Value.CanDelete);

        await RevokeEntitlementAsync(learnerEnrollment.Id, cancellationToken);
        ResourceNotFoundException revokedReplay = await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            SendInNewScopeAsync(createThread, cancellationToken));
        Assert.Equal("DISCUSSION.NOT_FOUND", revokedReplay.Code);
    }

    private async Task RevokeEntitlementAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        Enrollment enrollment = await dbContext.Set<Enrollment>().SingleAsync(
            item => item.Id == enrollmentId,
            cancellationToken);
        Entitlement entitlement = await dbContext.Set<Entitlement>().SingleAsync(
            item => item.Id == enrollment.EntitlementId,
            cancellationToken);
        entitlement.Revoke(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<DiscussionThreadResponse>> SendInNewScopeAsync(
        CreateDiscussionThreadCommand command,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private async Task<Result<CommentLikeResponse>> SendLikeInNewScopeAsync(
        LikeDiscussionCommentCommand command,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private async Task<Guid> CreateApprovedTeacherAsync(CancellationToken cancellationToken)
    {
        Guid userId = await CreateConfirmedUserAsync("phase9-discussion-teacher", cancellationToken);
        Guid reviewerId = await CreateConfirmedUserAsync("phase9-teacher-reviewer", cancellationToken);
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> submitted = await sender.Send(
            new SubmitTeacherApplicationCommand(
                userId,
                "Discussion systems instructor",
                "I teach secure release-scoped collaboration and moderation workflows.",
                "Application security and learning systems",
                "I want to teach safe online collaboration."),
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

