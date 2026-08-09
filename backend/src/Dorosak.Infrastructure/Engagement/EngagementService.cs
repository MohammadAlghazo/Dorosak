using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Engagement;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Engagement;

internal sealed class EngagementService(DorosakDbContext dbContext, TimeProvider timeProvider) : IEngagementService
{
    public async Task<Result<CourseReviewPageResponse>> GetCourseReviewsAsync(
        GetCourseReviewsQuery request,
        CancellationToken cancellationToken)
    {
        bool published = await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == request.CourseId && course.Status == CourseStatus.Published &&
                course.ActiveReleaseId != null && course.DeletedAt == null,
            cancellationToken);
        if (!published)
        {
            return Result.Failure<CourseReviewPageResponse>(ResultError.NotFound(
                "REVIEW.COURSE_NOT_FOUND", "The published course was not found."));
        }
        CourseReview[] reviews = await dbContext.CourseReviews.AsNoTracking()
            .Where(review => review.CourseId == request.CourseId && review.Status == CourseReviewStatus.Published)
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Take(request.Limit + 1)
            .ToArrayAsync(cancellationToken);
        bool hasMore = reviews.Length > request.Limit;
        CourseReview[] page = reviews.Take(request.Limit).ToArray();
        Guid[] userIds = page.Select(review => review.UserId).Distinct().ToArray();
        Dictionary<Guid, string> names = await dbContext.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        int total = await dbContext.CourseReviews.AsNoTracking()
            .CountAsync(review => review.CourseId == request.CourseId && review.Status == CourseReviewStatus.Published, cancellationToken);
        decimal average = total == 0
            ? 0
            : await dbContext.CourseReviews.AsNoTracking()
                .Where(review => review.CourseId == request.CourseId && review.Status == CourseReviewStatus.Published)
                .AverageAsync(review => (decimal)review.Rating, cancellationToken);
        return Result.Success(new CourseReviewPageResponse(
            page.Select(review => Map(review, names.GetValueOrDefault(review.UserId, "Learner"), exposeUserId: false)).ToArray(),
            decimal.Round(average, 2), total, hasMore));
    }

    public async Task<Result<CourseReviewResponse>> GetMyCourseReviewAsync(
        GetMyCourseReviewQuery request,
        CancellationToken cancellationToken)
    {
        CourseReview? review = await dbContext.CourseReviews.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.UserId == request.UserId && candidate.CourseId == request.CourseId &&
                candidate.Status != CourseReviewStatus.Removed,
            cancellationToken);
        return review is null
            ? Result.Failure<CourseReviewResponse>(ResultError.NotFound("REVIEW.NOT_FOUND", "The course review was not found."))
            : Result.Success(await MapAsync(review, cancellationToken));
    }

    public async Task<Result<CourseReviewResponse>> CreateCourseReviewAsync(
        CreateCourseReviewCommand request,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"course-review:{request.UserId:D}:{request.CourseId:D}"}, 0))",
            cancellationToken);
        if (!await HasEligibleEnrollmentAsync(request.UserId, request.CourseId, cancellationToken))
        {
            return Result.Failure<CourseReviewResponse>(ResultError.Forbidden(
                "REVIEW.ENROLLMENT_REQUIRED", "Only enrolled learners can review a course."));
        }
        CourseReview? existing = await dbContext.CourseReviews.SingleOrDefaultAsync(
            review => review.UserId == request.UserId && review.CourseId == request.CourseId,
            cancellationToken);
        if (existing is not null && existing.Status != CourseReviewStatus.Removed)
        {
            return Result.Failure<CourseReviewResponse>(ResultError.Conflict(
                "REVIEW.ALREADY_EXISTS", "This learner already has a review for the course."));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        CourseReview review;
        if (existing is null)
        {
            review = CourseReview.Create(request.UserId, request.CourseId, request.Rating, request.Text, now);
            dbContext.CourseReviews.Add(review);
        }
        else
        {
            review = existing;
            review.Republish(request.Rating, request.Text, now);
        }
        AddAudit(request.UserId, "engagement.review-published", review.Id, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(review, cancellationToken));
    }

    public async Task<Result<CourseReviewResponse>> UpdateCourseReviewAsync(
        UpdateCourseReviewCommand request,
        CancellationToken cancellationToken)
    {
        CourseReview? review = await dbContext.CourseReviews.SingleOrDefaultAsync(
            candidate => candidate.Id == request.ReviewId && candidate.UserId == request.UserId &&
                candidate.CourseId == request.CourseId,
            cancellationToken);
        if (review is null)
        {
            return Result.Failure<CourseReviewResponse>(ResultError.NotFound(
                "REVIEW.NOT_FOUND", "The course review was not found."));
        }
        if (!await HasEligibleEnrollmentAsync(request.UserId, request.CourseId, cancellationToken))
        {
            return Result.Failure<CourseReviewResponse>(ResultError.Forbidden(
                "REVIEW.ENROLLMENT_REQUIRED", "Only enrolled learners can edit a course review."));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        review.Update(request.Rating, request.Text, now);
        AddAudit(request.UserId, "engagement.review-updated", review.Id, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(review, cancellationToken));
    }

    public async Task<Result<EngagementOperationResponse>> DeleteCourseReviewAsync(
        DeleteCourseReviewCommand request,
        CancellationToken cancellationToken)
    {
        CourseReview? review = await dbContext.CourseReviews.SingleOrDefaultAsync(
            candidate => candidate.Id == request.ReviewId && candidate.UserId == request.UserId &&
                candidate.CourseId == request.CourseId,
            cancellationToken);
        if (review is null)
        {
            return Result.Failure<EngagementOperationResponse>(ResultError.NotFound(
                "REVIEW.NOT_FOUND", "The course review was not found."));
        }
        if (!await HasEligibleEnrollmentAsync(request.UserId, request.CourseId, cancellationToken))
        {
            return Result.Failure<EngagementOperationResponse>(ResultError.Forbidden(
                "REVIEW.ENROLLMENT_REQUIRED", "Only enrolled learners can remove a course review."));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        review.Remove(now);
        AddAudit(request.UserId, "engagement.review-removed", review.Id, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new EngagementOperationResponse(true));
    }

    private async Task<bool> HasEligibleEnrollmentAsync(Guid userId, Guid courseId, CancellationToken cancellationToken) =>
        await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join entitlement in dbContext.Entitlements.AsNoTracking() on enrollment.EntitlementId equals entitlement.Id
            where enrollment.UserId == userId && enrollment.CourseId == courseId &&
                (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Completed) &&
                entitlement.Status == EntitlementStatus.Active &&
                (entitlement.ExpiresAt == null || entitlement.ExpiresAt > timeProvider.GetUtcNow())
            select enrollment.Id).AnyAsync(cancellationToken);

    private async Task<CourseReviewResponse> MapAsync(CourseReview review, CancellationToken cancellationToken)
    {
        string name = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == review.UserId)
            .Select(user => user.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "Learner";
        return Map(review, name, exposeUserId: true);
    }

    private static CourseReviewResponse Map(CourseReview review, string authorName, bool exposeUserId) => new(
        review.Id, review.CourseId, exposeUserId ? review.UserId : Guid.Empty, authorName, review.Rating, review.Text,
        review.Status.ToString(), review.CreatedAt, review.UpdatedAt);

    private void AddAudit(Guid actorUserId, string action, Guid reviewId, string? reason, DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, "CourseReview", reviewId, "Succeeded", reason, now));
}
