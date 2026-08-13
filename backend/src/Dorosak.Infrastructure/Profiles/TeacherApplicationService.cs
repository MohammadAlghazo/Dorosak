using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Domain.Common;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Operations;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Profiles;

internal sealed class TeacherApplicationService(
    DorosakDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    CatalogCursorCodec cursorCodec,
    TimeProvider timeProvider) : ITeacherApplicationService
{
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
            ? Result.Failure<TeacherApplicationResponse>(ResultError.NotFound("TEACHER_APPLICATION.NOT_FOUND", "No teacher application was found."))
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
            return Result.Failure<TeacherApplicationResponse>(ResultError.NotFound(
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
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 20);
        string canonicalQuery = $"teacher-applications|submitted-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "teacher-applications", canonicalQuery, out DateTimeOffset? after, out Guid? afterId))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<TeacherApplicationResponse>>();
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
        return Result.Success(InfrastructureHelpers.Page(
            applications,
            limit,
            application => Map(application),
            application => application.SubmittedAt,
            application => application.Id,
            "teacher-applications",
            canonicalQuery,
            cursorCodec));
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
            return Result.Failure<TeacherApplicationResponse>(ResultError.NotFound(
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
                if (!await userManager.IsInRoleAsync(user, Identity.IdentityConstants.TeacherRole))
                {
                    IdentityResult roleResult = await userManager.AddToRoleAsync(
                        user,
                        Identity.IdentityConstants.TeacherRole);
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

        InfrastructureHelpers.AddAudit(dbContext, request.ReviewerUserId, $"teacher-application.{request.Decision}", "TeacherApplication", application.Id, request.Reason, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(application));
    }

    private async Task LockTeacherApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM profiles.teacher_applications WHERE id = {applicationId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private async Task LockTeacherApplicationsAsync(Guid userId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database.SqlQuery<int>(
                $"SELECT count(*)::int AS \"Value\" FROM (SELECT 1 FROM profiles.teacher_applications WHERE user_id = {userId} AND status IN ('Pending', 'InReview') FOR UPDATE) AS active_applications")
            .SingleAsync(cancellationToken);

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
}
