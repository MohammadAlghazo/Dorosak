using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Engagement;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Engagement;

internal sealed class EngagementService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider,
    CatalogCursorCodec cursorCodec) : IEngagementService, IDiscussionAccessReader
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
        AddAudit(request.UserId, "engagement.review-published", "CourseReview", review.Id, null, now);
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
        AddAudit(request.UserId, "engagement.review-updated", "CourseReview", review.Id, null, now);
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
        AddAudit(request.UserId, "engagement.review-removed", "CourseReview", review.Id, null, now);
        return Result.Success(new EngagementOperationResponse(true));
    }

    public async Task<bool> CanAccessAsync(
        Guid userId,
        DiscussionScope scope,
        CancellationToken cancellationToken) =>
        await FindDiscussionContextAsync(userId, scope, cancellationToken) is not null;

    public async Task<Result<DiscussionThreadPageResponse>> GetDiscussionThreadsAsync(
        GetDiscussionThreadsQuery request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionThreadPageResponse>();
        }

        string canonicalQuery = ThreadCanonicalQuery(request.UserId, request.Scope, request.Limit);
        if (!cursorCodec.TryRead(
                request.Cursor,
                "discussion-threads",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId))
        {
            return Result.Failure<DiscussionThreadPageResponse>(ResultError.BusinessRule(
                "CURSOR.INVALID", "The discussion cursor is invalid or does not match this query."));
        }

        IQueryable<DiscussionThread> query = dbContext.DiscussionThreads.AsNoTracking().Where(thread =>
            thread.CourseId == context.CourseId &&
            thread.ReleaseId == context.ReleaseId &&
            thread.LessonId == request.Scope.LessonId &&
            thread.Status != DiscussionStatus.Removed);
        if (afterCreatedAt is { } timestamp && afterId is { } id)
        {
            query = query.Where(thread =>
                thread.CreatedAt < timestamp ||
                thread.CreatedAt == timestamp && thread.Id.CompareTo(id) < 0);
        }

        List<DiscussionThread> threads = await query
            .OrderByDescending(thread => thread.CreatedAt)
            .ThenByDescending(thread => thread.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = threads.Count > request.Limit;
        DiscussionThread[] page = threads.Take(request.Limit).ToArray();
        Guid[] threadIds = page.Select(thread => thread.Id).ToArray();
        Dictionary<Guid, int> commentCounts = await dbContext.DiscussionComments.AsNoTracking()
            .Where(comment => threadIds.Contains(comment.ThreadId) && comment.Status != DiscussionCommentStatus.Removed)
            .GroupBy(comment => comment.ThreadId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        Guid[] authorIds = page.Select(thread => thread.AuthorUserId).Distinct().ToArray();
        Dictionary<Guid, string> names = await dbContext.Users.AsNoTracking()
            .Where(user => authorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create("discussion-threads", canonicalQuery, page[^1].CreatedAt, page[^1].Id)
            : null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        return Result.Success(new DiscussionThreadPageResponse(
            page.Select(thread => MapThreadSummary(
                thread,
                names.GetValueOrDefault(thread.AuthorUserId, "Learner"),
                commentCounts.GetValueOrDefault(thread.Id),
                request.UserId,
                now)).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<DiscussionThreadResponse>> GetDiscussionThreadAsync(
        GetDiscussionThreadQuery request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionThreadResponse>();
        }

        DiscussionThread? thread = await dbContext.DiscussionThreads.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.ThreadId &&
                candidate.CourseId == context.CourseId &&
                candidate.ReleaseId == context.ReleaseId &&
                candidate.LessonId == request.Scope.LessonId,
            cancellationToken);
        if (thread is null)
        {
            return DiscussionNotFound<DiscussionThreadResponse>();
        }

        string canonicalQuery = CommentCanonicalQuery(
            request.UserId,
            request.Scope,
            thread.Id,
            request.CommentLimit);
        if (!cursorCodec.TryRead(
                request.CommentCursor,
                "discussion-comments",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId))
        {
            return Result.Failure<DiscussionThreadResponse>(ResultError.BusinessRule(
                "CURSOR.INVALID", "The discussion comment cursor is invalid or does not match this query."));
        }

        DiscussionCommentPageResponse commentPage = new([], null, false);
        int commentCount = 0;
        if (thread.Status == DiscussionStatus.Published)
        {
            IQueryable<DiscussionComment> commentsQuery = dbContext.DiscussionComments.AsNoTracking()
                .Where(comment => comment.ThreadId == thread.Id);
            if (afterCreatedAt is { } timestamp && afterId is { } id)
            {
                commentsQuery = commentsQuery.Where(comment =>
                    comment.CreatedAt > timestamp ||
                    comment.CreatedAt == timestamp && comment.Id.CompareTo(id) > 0);
            }

            List<DiscussionComment> comments = await commentsQuery
                .OrderBy(comment => comment.CreatedAt)
                .ThenBy(comment => comment.Id)
                .Take(request.CommentLimit + 1)
                .ToListAsync(cancellationToken);
            bool hasMore = comments.Count > request.CommentLimit;
            commentPage = await MapCommentPageAsync(
                comments.Take(request.CommentLimit).ToArray(),
                request.UserId,
                canonicalQuery,
                hasMore,
                cancellationToken);
            commentCount = await GetCommentCountAsync(thread.Id, cancellationToken);
        }

        string authorName = await GetDisplayNameAsync(thread.AuthorUserId, cancellationToken);
        return Result.Success(MapThread(
            thread,
            authorName,
            request.UserId,
            commentCount,
            commentPage,
            timeProvider.GetUtcNow()));
    }

    public async Task<Result<DiscussionThreadResponse>> CreateDiscussionThreadAsync(
        CreateDiscussionThreadCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionThreadResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DiscussionThread thread;
        try
        {
            thread = DiscussionThread.Create(
                context.CourseId,
                context.ReleaseId,
                request.Scope.LessonId,
                request.UserId,
                request.Title,
                request.Body,
                now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<DiscussionThreadResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        dbContext.DiscussionThreads.Add(thread);
        AddAudit(request.UserId, "engagement.discussion-thread-created", "DiscussionThread", thread.Id, null, now);
        return Result.Success(MapThread(
            thread,
            await GetDisplayNameAsync(request.UserId, cancellationToken),
            request.UserId,
            0,
            new DiscussionCommentPageResponse([], null, false),
            now));
    }

    public async Task<Result<DiscussionThreadResponse>> UpdateDiscussionThreadAsync(
        UpdateDiscussionThreadCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionThreadResponse>();
        }

        await LockThreadAsync(request.ThreadId, cancellationToken);
        DiscussionThread? thread = await dbContext.DiscussionThreads.SingleOrDefaultAsync(candidate =>
            candidate.Id == request.ThreadId &&
            candidate.CourseId == context.CourseId &&
            candidate.ReleaseId == context.ReleaseId &&
            candidate.LessonId == request.Scope.LessonId &&
            candidate.AuthorUserId == request.UserId,
            cancellationToken);
        if (thread is null)
        {
            return DiscussionNotFound<DiscussionThreadResponse>();
        }

        try
        {
            thread.Update(request.Title, request.Body, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<DiscussionThreadResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        AddAudit(request.UserId, "engagement.discussion-thread-updated", "DiscussionThread", thread.Id, null, thread.UpdatedAt);
        DiscussionCommentPageResponse comments = await GetInitialCommentsForThreadAsync(
            thread.Id,
            request.UserId,
            request.Scope,
            50,
            cancellationToken);
        return Result.Success(MapThread(
            thread,
            await GetDisplayNameAsync(request.UserId, cancellationToken),
            request.UserId,
            await GetCommentCountAsync(thread.Id, cancellationToken),
            comments,
            thread.UpdatedAt));
    }

    public async Task<Result<EngagementOperationResponse>> DeleteDiscussionThreadAsync(
        DeleteDiscussionThreadCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<EngagementOperationResponse>();
        }

        await LockThreadAsync(request.ThreadId, cancellationToken);
        DiscussionThread? thread = await dbContext.DiscussionThreads.SingleOrDefaultAsync(candidate =>
            candidate.Id == request.ThreadId &&
            candidate.CourseId == context.CourseId &&
            candidate.ReleaseId == context.ReleaseId &&
            candidate.LessonId == request.Scope.LessonId &&
            candidate.AuthorUserId == request.UserId,
            cancellationToken);
        if (thread is null)
        {
            return DiscussionNotFound<EngagementOperationResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (thread.Remove(now))
            {
                AddAudit(request.UserId, "engagement.discussion-thread-removed", "DiscussionThread", thread.Id, null, now);
            }
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<EngagementOperationResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }
        return Result.Success(new EngagementOperationResponse(true));
    }

    public async Task<Result<DiscussionCommentResponse>> CreateDiscussionCommentAsync(
        CreateDiscussionCommentCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        await LockThreadAsync(request.ThreadId, cancellationToken);
        DiscussionThread? thread = await FindPublishedThreadAsync(
            context,
            request.Scope.LessonId,
            request.ThreadId,
            cancellationToken);
        if (thread is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        DiscussionComment? parent = null;
        if (request.ParentCommentId is { } parentId)
        {
            await LockCommentAsync(parentId, cancellationToken);
            parent = await dbContext.DiscussionComments.SingleOrDefaultAsync(comment =>
                comment.Id == parentId && comment.ThreadId == thread.Id &&
                comment.Status == DiscussionCommentStatus.Published,
                cancellationToken);
            if (parent is null)
            {
                return DiscussionNotFound<DiscussionCommentResponse>();
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DiscussionComment comment;
        try
        {
            comment = DiscussionComment.Create(
                thread.Id,
                request.ParentCommentId,
                parent?.Depth ?? -1,
                request.UserId,
                request.Body,
                now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<DiscussionCommentResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        dbContext.DiscussionComments.Add(comment);
        AddAudit(request.UserId, "engagement.discussion-comment-created", "DiscussionComment", comment.Id, null, now);
        return Result.Success(await MapCommentAsync(comment, request.UserId, cancellationToken));
    }

    public async Task<Result<DiscussionCommentResponse>> UpdateDiscussionCommentAsync(
        UpdateDiscussionCommentCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        await LockThreadAsync(request.ThreadId, cancellationToken);
        if (await FindPublishedThreadAsync(
                context,
                request.Scope.LessonId,
                request.ThreadId,
                cancellationToken) is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        await LockCommentAsync(request.CommentId, cancellationToken);
        DiscussionComment? comment = await dbContext.DiscussionComments.SingleOrDefaultAsync(candidate =>
            candidate.Id == request.CommentId && candidate.ThreadId == request.ThreadId &&
            candidate.AuthorUserId == request.UserId,
            cancellationToken);
        if (comment is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        try
        {
            comment.Update(request.Body, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<DiscussionCommentResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        AddAudit(request.UserId, "engagement.discussion-comment-updated", "DiscussionComment", comment.Id, null, comment.UpdatedAt);
        return Result.Success(await MapCommentAsync(comment, request.UserId, cancellationToken));
    }

    public async Task<Result<EngagementOperationResponse>> DeleteDiscussionCommentAsync(
        DeleteDiscussionCommentCommand request,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<EngagementOperationResponse>();
        }

        await LockThreadAsync(request.ThreadId, cancellationToken);
        if (await FindPublishedThreadAsync(
                context,
                request.Scope.LessonId,
                request.ThreadId,
                cancellationToken) is null)
        {
            return DiscussionNotFound<EngagementOperationResponse>();
        }

        await LockCommentAsync(request.CommentId, cancellationToken);
        DiscussionComment? comment = await dbContext.DiscussionComments.SingleOrDefaultAsync(candidate =>
            candidate.Id == request.CommentId && candidate.ThreadId == request.ThreadId &&
            candidate.AuthorUserId == request.UserId,
            cancellationToken);
        if (comment is null)
        {
            return DiscussionNotFound<EngagementOperationResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (comment.Remove(now))
            {
                AddAudit(request.UserId, "engagement.discussion-comment-removed", "DiscussionComment", comment.Id, null, now);
            }
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<EngagementOperationResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }
        return Result.Success(new EngagementOperationResponse(true));
    }

    public Task<Result<CommentLikeResponse>> LikeDiscussionCommentAsync(
        LikeDiscussionCommentCommand request,
        CancellationToken cancellationToken) => ChangeCommentLikeAsync(
            request.UserId,
            request.Scope,
            request.ThreadId,
            request.CommentId,
            true,
            cancellationToken);

    public Task<Result<CommentLikeResponse>> UnlikeDiscussionCommentAsync(
        UnlikeDiscussionCommentCommand request,
        CancellationToken cancellationToken) => ChangeCommentLikeAsync(
            request.UserId,
            request.Scope,
            request.ThreadId,
            request.CommentId,
            false,
            cancellationToken);

    public async Task<Result<DiscussionCommentResponse>> GetDiscussionCommentForReplayAsync(
        Guid userId,
        DiscussionScope scope,
        Guid threadId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(userId, scope, cancellationToken);
        if (context is null || await FindPublishedThreadAsync(
                context,
                scope.LessonId,
                threadId,
                cancellationToken) is null)
        {
            return DiscussionNotFound<DiscussionCommentResponse>();
        }

        DiscussionComment? comment = await dbContext.DiscussionComments.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == commentId && candidate.ThreadId == threadId,
            cancellationToken);
        return comment is null
            ? DiscussionNotFound<DiscussionCommentResponse>()
            : Result.Success(await MapCommentAsync(comment, userId, cancellationToken));
    }

    private async Task<Result<CommentLikeResponse>> ChangeCommentLikeAsync(
        Guid userId,
        DiscussionScope scope,
        Guid threadId,
        Guid commentId,
        bool liked,
        CancellationToken cancellationToken)
    {
        DiscussionContext? context = await FindDiscussionContextAsync(userId, scope, cancellationToken);
        if (context is null)
        {
            return DiscussionNotFound<CommentLikeResponse>();
        }

        await LockThreadAsync(threadId, cancellationToken);
        if (await FindPublishedThreadAsync(context, scope.LessonId, threadId, cancellationToken) is null)
        {
            return DiscussionNotFound<CommentLikeResponse>();
        }

        await LockCommentAsync(commentId, cancellationToken);
        DiscussionComment? comment = await dbContext.DiscussionComments.SingleOrDefaultAsync(candidate =>
            candidate.Id == commentId && candidate.ThreadId == threadId &&
            candidate.Status == DiscussionCommentStatus.Published,
            cancellationToken);
        if (comment is null)
        {
            return DiscussionNotFound<CommentLikeResponse>();
        }

        CommentLike? existing = await dbContext.CommentLikes.SingleOrDefaultAsync(
            like => like.CommentId == commentId && like.UserId == userId,
            cancellationToken);
        if (liked && existing is null)
        {
            dbContext.CommentLikes.Add(CommentLike.Create(commentId, userId, timeProvider.GetUtcNow()));
        }
        else if (!liked && existing is not null)
        {
            dbContext.CommentLikes.Remove(existing);
        }

        int count = await dbContext.CommentLikes.CountAsync(item => item.CommentId == commentId, cancellationToken);
        int adjustedCount = count + (liked && existing is null ? 1 : 0) - (!liked && existing is not null ? 1 : 0);
        return Result.Success(new CommentLikeResponse(commentId, liked, adjustedCount));
    }

    private async Task<DiscussionContext?> FindDiscussionContextAsync(
        Guid userId,
        DiscussionScope scope,
        CancellationToken cancellationToken)
    {
        if (!scope.IsValid)
        {
            return null;
        }

        DiscussionContext? context;
        if (scope.EnrollmentId is { } enrollmentId)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            Enrollment? enrollment = await dbContext.Enrollments.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == enrollmentId && item.UserId == userId &&
                    (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed),
                cancellationToken);
            if (enrollment is null || !await dbContext.Entitlements.AsNoTracking().AnyAsync(
                    item => item.Id == enrollment.EntitlementId && item.UserId == userId &&
                        item.CourseId == enrollment.CourseId && item.Status == EntitlementStatus.Active &&
                        (item.ExpiresAt == null || item.ExpiresAt > now),
                    cancellationToken))
            {
                return null;
            }

            CourseRelease? release = await dbContext.CourseReleases.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == enrollment.ReleaseId && item.CourseId == enrollment.CourseId,
                cancellationToken);
            context = release is null ? null : new DiscussionContext(enrollment.CourseId, release.Id);
        }
        else
        {
            Guid courseId = scope.CourseId!.Value;
            Guid releaseId = scope.ReleaseId!.Value;
            if (!await CanManageCourseAsync(userId, courseId, cancellationToken))
            {
                return null;
            }

            bool releaseExists = await dbContext.CourseReleases.AsNoTracking().AnyAsync(
                release => release.Id == releaseId && release.CourseId == courseId,
                cancellationToken);
            context = releaseExists ? new DiscussionContext(courseId, releaseId) : null;
        }

        if (context is null || scope.LessonId is not { } lessonId)
        {
            return context;
        }

        return await dbContext.CourseReleaseLessons.AsNoTracking().AnyAsync(
            lesson => lesson.Id == lessonId && lesson.ReleaseId == context.ReleaseId,
            cancellationToken)
            ? context
            : null;
    }

    private Task<DiscussionThread?> FindPublishedThreadAsync(
        DiscussionContext context,
        Guid? lessonId,
        Guid threadId,
        CancellationToken cancellationToken) =>
        dbContext.DiscussionThreads.SingleOrDefaultAsync(thread =>
            thread.Id == threadId && thread.CourseId == context.CourseId &&
            thread.ReleaseId == context.ReleaseId && thread.LessonId == lessonId &&
            thread.Status == DiscussionStatus.Published,
            cancellationToken);

    private async Task<DiscussionCommentPageResponse> GetInitialCommentsForThreadAsync(
        Guid threadId,
        Guid viewerUserId,
        DiscussionScope scope,
        int limit,
        CancellationToken cancellationToken)
    {
        List<DiscussionComment> comments = await dbContext.DiscussionComments.AsNoTracking()
            .Where(comment => comment.ThreadId == threadId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = comments.Count > limit;
        return await MapCommentPageAsync(
            comments.Take(limit).ToArray(),
            viewerUserId,
            CommentCanonicalQuery(viewerUserId, scope, threadId, limit),
            hasMore,
            cancellationToken);
    }

    private async Task<DiscussionCommentPageResponse> MapCommentPageAsync(
        DiscussionComment[] comments,
        Guid viewerUserId,
        string canonicalQuery,
        bool hasMore,
        CancellationToken cancellationToken)
    {
        Guid[] commentIds = comments.Select(comment => comment.Id).ToArray();
        Guid[] authorIds = comments.Select(comment => comment.AuthorUserId).Distinct().ToArray();
        Dictionary<Guid, string> names = await dbContext.Users.AsNoTracking()
            .Where(user => authorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        Dictionary<Guid, int> likeCounts = await dbContext.CommentLikes.AsNoTracking()
            .Where(like => commentIds.Contains(like.CommentId))
            .GroupBy(like => like.CommentId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        HashSet<Guid> viewerLikes = (await dbContext.CommentLikes.AsNoTracking()
            .Where(like => like.UserId == viewerUserId && commentIds.Contains(like.CommentId))
            .Select(like => like.CommentId)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        DiscussionComment? last = comments.Length == 0 ? null : comments[^1];
        string? nextCursor = hasMore && last is not null
            ? cursorCodec.Create("discussion-comments", canonicalQuery, last.CreatedAt, last.Id)
            : null;
        return new DiscussionCommentPageResponse(
            comments.Select(comment => MapComment(
                comment,
                names.GetValueOrDefault(comment.AuthorUserId, "Learner"),
                viewerUserId,
                likeCounts.GetValueOrDefault(comment.Id),
                viewerLikes.Contains(comment.Id))).ToArray(),
            nextCursor,
            hasMore);
    }

    private async Task<DiscussionCommentResponse> MapCommentAsync(
        DiscussionComment comment,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        string name = await GetDisplayNameAsync(comment.AuthorUserId, cancellationToken);
        int likeCount = await dbContext.CommentLikes.AsNoTracking().CountAsync(
            like => like.CommentId == comment.Id,
            cancellationToken);
        bool liked = await dbContext.CommentLikes.AsNoTracking().AnyAsync(
            like => like.CommentId == comment.Id && like.UserId == viewerUserId,
            cancellationToken);
        return MapComment(comment, name, viewerUserId, likeCount, liked);
    }

    private DiscussionCommentResponse MapComment(
        DiscussionComment comment,
        string authorName,
        Guid viewerUserId,
        int likeCount,
        bool likedByViewer)
    {
        bool published = comment.Status == DiscussionCommentStatus.Published;
        return new DiscussionCommentResponse(
            comment.Id,
            comment.ThreadId,
            comment.ParentCommentId,
            published ? comment.AuthorUserId : Guid.Empty,
            published ? authorName : "Learner",
            published ? comment.Body : string.Empty,
            comment.Depth,
            comment.Status.ToString(),
            published && comment.EditedAt is not null,
            published ? likeCount : 0,
            published && likedByViewer,
            comment.CreatedAt,
            comment.UpdatedAt,
            published && comment.AuthorUserId == viewerUserId &&
                timeProvider.GetUtcNow() <= comment.CreatedAt.AddMinutes(DiscussionComment.EditWindowMinutes),
            published && comment.AuthorUserId == viewerUserId);
    }

    private static DiscussionThreadResponse MapThread(
        DiscussionThread thread,
        string authorName,
        Guid viewerUserId,
        int commentCount,
        DiscussionCommentPageResponse comments,
        DateTimeOffset now)
    {
        bool published = thread.Status == DiscussionStatus.Published;
        return new DiscussionThreadResponse(
            thread.Id,
            thread.CourseId,
            thread.ReleaseId,
            thread.LessonId,
            published ? thread.AuthorUserId : Guid.Empty,
            published ? authorName : "Learner",
            published ? thread.Title : string.Empty,
            published ? thread.Body : string.Empty,
            thread.Status.ToString(),
            thread.CreatedAt,
            thread.UpdatedAt,
            published && thread.EditedAt is not null,
            published ? commentCount : 0,
            published && thread.AuthorUserId == viewerUserId &&
                now <= thread.CreatedAt.AddMinutes(DiscussionThread.EditWindowMinutes),
            published && thread.AuthorUserId == viewerUserId,
            comments);
    }

    private static DiscussionThreadSummaryResponse MapThreadSummary(
        DiscussionThread thread,
        string authorName,
        int commentCount,
        Guid viewerUserId,
        DateTimeOffset now)
    {
        bool published = thread.Status == DiscussionStatus.Published;
        string body = published ? thread.Body : string.Empty;
        string preview = body.Length <= 280 ? body : string.Concat(body.AsSpan(0, 277), "...");
        return new DiscussionThreadSummaryResponse(
            thread.Id,
            thread.LessonId,
            published ? thread.AuthorUserId : Guid.Empty,
            published ? authorName : "Learner",
            published ? thread.Title : string.Empty,
            preview,
            thread.Status.ToString(),
            thread.CreatedAt,
            thread.UpdatedAt,
            published && thread.EditedAt is not null,
            published ? commentCount : 0,
            published && thread.AuthorUserId == viewerUserId &&
                now <= thread.CreatedAt.AddMinutes(DiscussionThread.EditWindowMinutes),
            published && thread.AuthorUserId == viewerUserId);
    }

    private Task<int> GetCommentCountAsync(Guid threadId, CancellationToken cancellationToken) =>
        dbContext.DiscussionComments.AsNoTracking().CountAsync(
            comment => comment.ThreadId == threadId && comment.Status != DiscussionCommentStatus.Removed,
            cancellationToken);

    private static string ThreadCanonicalQuery(Guid userId, DiscussionScope scope, int limit) =>
        $"discussion-threads|{userId:D}|{ScopeKey(scope)}|{LessonKey(scope.LessonId)}|created-desc|{limit}";

    private static string CommentCanonicalQuery(
        Guid userId,
        DiscussionScope scope,
        Guid threadId,
        int limit) =>
        $"discussion-comments|{userId:D}|{ScopeKey(scope)}|{LessonKey(scope.LessonId)}|{threadId:D}|created-asc|{limit}";

    private static string ScopeKey(DiscussionScope scope) => scope.EnrollmentId is { } enrollmentId
        ? $"enrollment:{enrollmentId:D}"
        : $"course:{scope.CourseId!.Value:D}:release:{scope.ReleaseId!.Value:D}";

    private static string LessonKey(Guid? lessonId) => lessonId is { } id ? id.ToString("D") : "course";

    private Task<int> LockThreadAsync(Guid threadId, CancellationToken cancellationToken) =>
        LockDiscussionResourceAsync($"discussion-thread:{threadId:D}", cancellationToken);

    private Task<int> LockCommentAsync(Guid commentId, CancellationToken cancellationToken) =>
        LockDiscussionResourceAsync($"discussion-comment:{commentId:D}", cancellationToken);

    private Task<int> LockDiscussionResourceAsync(string identity, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))",
            cancellationToken);

    private async Task<bool> CanManageCourseAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId && course.DeletedAt == null &&
                (course.OwnerUserId == userId || dbContext.CourseInstructors.Any(instructor =>
                    instructor.CourseId == courseId && instructor.UserId == userId &&
                    instructor.Role != CourseCollaboratorRole.Reviewer)),
            cancellationToken) || await HasPermissionAsync(
            userId,
            Permissions.CourseManageAny,
            cancellationToken);

    private Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking()
            .Join(
                dbContext.RoleClaims.AsNoTracking(),
                role => role.RoleId,
                claim => claim.RoleId,
                (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId &&
                item.claim.ClaimType == IdentityConstants.PermissionClaimType &&
                item.claim.ClaimValue == permission,
                cancellationToken);

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "Learner";

    private static Result<T> DiscussionNotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "DISCUSSION.NOT_FOUND", "The discussion resource was not found."));

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

    private void AddAudit(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string? reason,
        DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", reason, now));

    private sealed record DiscussionContext(Guid CourseId, Guid ReleaseId);
}
