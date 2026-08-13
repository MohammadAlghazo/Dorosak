using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.PublishingCoordinator;

internal sealed class PublishingService(
    DorosakDbContext dbContext,
    ICourseAccessReader courseAccessReader,
    CatalogCursorCodec cursorCodec,
    TimeProvider timeProvider) : IPublishingService
{
    public async Task<Result<PublicationStatusResponse>> RequestPublicationAsync(
        RequestPublicationCommand request,
        CancellationToken cancellationToken)
    {
        if (!await courseAccessReader.CanAccessAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken))
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
        }
        await LockCourseAsync(request.CourseId, cancellationToken);
        if (!await courseAccessReader.CanAccessAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken))
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
        }

        Course? course = await dbContext.Courses.SingleOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
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
        InfrastructureHelpers.AddAudit(dbContext, request.UserId, "course.publication-requested", "Course", course.Id, null, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapPublicationStatus(course, draft, review));
    }

    public async Task<Result<PublicationStatusResponse>> GetPublicationStatusAsync(
        GetPublicationStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (!await courseAccessReader.CanAccessAsync(request.CourseId, request.UserId, CourseAccess.View, cancellationToken))
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
        }

        Course? course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
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
        if (!await courseAccessReader.CanAccessAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken))
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
        }
        await LockCourseAsync(request.CourseId, cancellationToken);
        if (!await courseAccessReader.CanAccessAsync(request.CourseId, request.UserId, CourseAccess.Owner, cancellationToken))
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
        }

        Course? course = await dbContext.Courses.SingleOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return Result.Failure<PublicationStatusResponse>(InfrastructureHelpers.CourseNotFound());
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

        InfrastructureHelpers.AddAudit(dbContext, request.UserId, "course.publication-withdrawn", "Course", course.Id, null, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MapPublicationStatus(course, draft, review));
    }

    public async Task<Result<PagedResponse<PublicationReviewResponse>>> GetPublicationReviewsAsync(
        GetPublicationReviewsQuery request,
        CancellationToken cancellationToken)
    {
        int limit = InfrastructureHelpers.NormalizeLimit(request.Limit, 20);
        string canonical = $"publication-reviews|requested-desc|{limit}";
        if (!cursorCodec.TryRead(request.Cursor, "publication-reviews", canonical, out DateTimeOffset? after, out Guid? afterId))
        {
            return InfrastructureHelpers.CursorFailure<PagedResponse<PublicationReviewResponse>>();
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
        return Result.Success(InfrastructureHelpers.Page(
            reviews,
            limit,
            Map,
            review => review.RequestedAt,
            review => review.Id,
            "publication-reviews",
            canonical,
            cursorCodec));
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
            return Result.Failure<PublicationReviewResponse>(ResultError.NotFound(
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

        InfrastructureHelpers.AddAudit(dbContext, request.ReviewerUserId, $"course.review-{request.Decision}", "Course", course.Id, request.Reason, timeProvider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(review));
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
}
