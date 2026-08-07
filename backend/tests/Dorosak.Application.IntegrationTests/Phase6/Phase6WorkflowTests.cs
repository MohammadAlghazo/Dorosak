using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Phase6;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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
