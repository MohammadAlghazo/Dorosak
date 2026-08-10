using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Moderation;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;
using Dorosak.Domain.Engagement;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Moderation;

internal sealed class ModerationService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider,
    CatalogCursorCodec cursorCodec) : IModerationService
{
    public async Task<Result<ContentReportResponse>> CreateContentReportAsync(
        CreateContentReportCommand request,
        CancellationToken cancellationToken)
    {
        if (!await HasReportTargetAccessAsync(request.UserId, request, cancellationToken))
        {
            return NotFound<ContentReportResponse>();
        }

        string targetIdentity = TargetIdentity(request.CourseId, request.ReviewId, request.CommentId, request.ReportedUserId);
        await LockAsync($"report-target:{request.UserId:D}:{targetIdentity}", cancellationToken);
        if (await HasOpenDuplicateAsync(request, cancellationToken))
        {
            return Result.Failure<ContentReportResponse>(ResultError.Conflict(
                "REPORT.ALREADY_OPEN", "An open report for this target already exists."));
        }

        if (!TryParseNamedEnum(request.Reason, out ContentReportReason reason))
        {
            return Result.Failure<ContentReportResponse>(ResultError.BusinessRule(
                "REPORT.REASON_INVALID", "The report reason is not supported."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ContentReport report;
        try
        {
            report = ContentReport.Create(
                request.UserId,
                request.CourseId,
                request.ReviewId,
                request.CommentId,
                request.ReportedUserId,
                reason,
                request.Details,
                now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<ContentReportResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        dbContext.ContentReports.Add(report);
        dbContext.ModerationCases.Add(ModerationCase.Create(report.Id, now));
        AddAudit(request.UserId, "moderation.report-created", "ContentReport", report.Id, null, now);
        return Result.Success(MapReport(report));
    }

    public async Task<Result<ContentReportResponse>> GetMyContentReportAsync(
        GetMyContentReportQuery request,
        CancellationToken cancellationToken)
    {
        ContentReport? report = await dbContext.ContentReports.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ReportId && item.ReporterUserId == request.UserId,
            cancellationToken);
        return report is null
            ? NotFound<ContentReportResponse>()
            : Result.Success(MapReport(report));
    }

    public async Task<Result<ContentReportPageResponse>> GetAdminContentReportsAsync(
        GetAdminContentReportsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanModerateAsync(request.ActorUserId, cancellationToken))
        {
            return Forbidden<ContentReportPageResponse>();
        }

        ContentReportStatus? status = ParseStatus<ContentReportStatus>(request.Status);
        string? targetKind = NormalizeTargetKind(request.TargetKind);
        string canonicalQuery = $"moderation-reports|{request.ActorUserId:D}|{status}|{targetKind}|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "moderation-reports",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId))
        {
            return CursorInvalid<ContentReportPageResponse>();
        }

        IQueryable<ContentReport> query = dbContext.ContentReports.AsNoTracking();
        if (status is { } requestedStatus)
        {
            query = query.Where(item => item.Status == requestedStatus);
        }
        if (targetKind is not null)
        {
            query = targetKind switch
            {
                "Course" => query.Where(item => item.CourseId != null),
                "Review" => query.Where(item => item.ReviewId != null),
                "Comment" => query.Where(item => item.CommentId != null),
                "ReportedUser" => query.Where(item => item.ReportedUserId != null),
                _ => query.Where(_ => false),
            };
        }
        if (afterCreatedAt is { } timestamp && afterId is { } id)
        {
            query = query.Where(item => item.CreatedAt < timestamp ||
                item.CreatedAt == timestamp && item.Id.CompareTo(id) < 0);
        }

        List<ContentReport> reports = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = reports.Count > request.Limit;
        ContentReport[] page = reports.Take(request.Limit).ToArray();
        Guid[] reportIds = page.Select(item => item.Id).ToArray();
        Dictionary<Guid, ModerationCase> cases = await dbContext.ModerationCases.AsNoTracking()
            .Where(item => reportIds.Contains(item.ReportId))
            .ToDictionaryAsync(item => item.ReportId, cancellationToken);
        Guid[] userIds = page
            .SelectMany(item => new[] { item.ReporterUserId, cases.GetValueOrDefault(item.Id)?.AssignedToUserId })
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, string> names = await GetDisplayNamesAsync(userIds, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create("moderation-reports", canonicalQuery, page[^1].CreatedAt, page[^1].Id)
            : null;
        return Result.Success(new ContentReportPageResponse(
            page.Select(item => MapAdminReport(
                item,
                cases[item.Id],
                names.GetValueOrDefault(item.ReporterUserId, "User"))).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<ModerationCasePageResponse>> GetModerationCasesAsync(
        GetModerationCasesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanModerateAsync(request.ActorUserId, cancellationToken))
        {
            return Forbidden<ModerationCasePageResponse>();
        }

        ModerationCaseStatus? status = ParseStatus<ModerationCaseStatus>(request.Status);
        string canonicalQuery = $"moderation-cases|{request.ActorUserId:D}|{status}|{request.Limit}";
        if (!cursorCodec.TryRead(
                request.Cursor,
                "moderation-cases",
                canonicalQuery,
                out DateTimeOffset? afterCreatedAt,
                out Guid? afterId))
        {
            return CursorInvalid<ModerationCasePageResponse>();
        }

        IQueryable<ModerationCase> query = dbContext.ModerationCases.AsNoTracking();
        if (status is { } requestedStatus)
        {
            query = query.Where(item => item.Status == requestedStatus);
        }
        if (afterCreatedAt is { } timestamp && afterId is { } id)
        {
            query = query.Where(item => item.CreatedAt < timestamp ||
                item.CreatedAt == timestamp && item.Id.CompareTo(id) < 0);
        }

        List<ModerationCase> cases = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = cases.Count > request.Limit;
        ModerationCase[] page = cases.Take(request.Limit).ToArray();
        Guid[] reportIds = page.Select(item => item.ReportId).ToArray();
        ContentReport[] reports = await dbContext.ContentReports.AsNoTracking()
            .Where(item => reportIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, ContentReport> reportsById = reports.ToDictionary(item => item.Id);
        Guid[] userIds = page
            .SelectMany(item => new[] { reportsById[item.ReportId].ReporterUserId, item.AssignedToUserId })
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, string> names = await GetDisplayNamesAsync(userIds, cancellationToken);
        string? nextCursor = hasMore && page.Length > 0
            ? cursorCodec.Create("moderation-cases", canonicalQuery, page[^1].CreatedAt, page[^1].Id)
            : null;
        return Result.Success(new ModerationCasePageResponse(
            page.Select(item => MapCaseSummary(
                item,
                reportsById[item.ReportId],
                names.GetValueOrDefault(reportsById[item.ReportId].ReporterUserId, "User"),
                item.AssignedToUserId is { } assignedId ? names.GetValueOrDefault(assignedId) : null)).ToArray(),
            nextCursor,
            hasMore));
    }

    public async Task<Result<ModerationCaseResponse>> GetModerationCaseAsync(
        GetModerationCaseQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanModerateAsync(request.ActorUserId, cancellationToken))
        {
            return Forbidden<ModerationCaseResponse>();
        }

        ModerationCase? moderationCase = await dbContext.ModerationCases.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.CaseId,
            cancellationToken);
        if (moderationCase is null)
        {
            return NotFound<ModerationCaseResponse>();
        }

        ContentReport? report = await dbContext.ContentReports.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == moderationCase.ReportId,
            cancellationToken);
        return report is null
            ? NotFound<ModerationCaseResponse>()
            : Result.Success(await MapCaseAsync(moderationCase, report, cancellationToken));
    }

    public async Task<Result<ModerationCaseResponse>> ApplyModerationActionAsync(
        ApplyModerationActionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await CanModerateAsync(request.ActorUserId, cancellationToken))
        {
            return Forbidden<ModerationCaseResponse>();
        }
        if (!TryParseNamedEnum(request.Action, out ModerationActionType actionType))
        {
            return Result.Failure<ModerationCaseResponse>(ResultError.BusinessRule(
                "MODERATION.ACTION_INVALID", "The moderation action is not supported."));
        }

        await LockAsync($"moderation-case:{request.CaseId:D}", cancellationToken);
        ModerationCase? moderationCase = await dbContext.ModerationCases.SingleOrDefaultAsync(
            item => item.Id == request.CaseId,
            cancellationToken);
        if (moderationCase is null)
        {
            return NotFound<ModerationCaseResponse>();
        }
        ContentReport? report = await dbContext.ContentReports.SingleOrDefaultAsync(
            item => item.Id == moderationCase.ReportId,
            cancellationToken);
        if (report is null)
        {
            return NotFound<ModerationCaseResponse>();
        }
        if (moderationCase.Version != request.ExpectedVersion)
        {
            return Result.Failure<ModerationCaseResponse>(ResultError.Conflict(
                "MODERATION.VERSION_CONFLICT",
                "The moderation case changed after it was loaded. Refresh before applying an action."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ModerationAction pendingAction;
        try
        {
            pendingAction = ModerationAction.Create(
                moderationCase.Id,
                request.ActorUserId,
                actionType,
                request.Reason,
                now);
            switch (actionType)
            {
                case ModerationActionType.StartReview:
                    StartReview(moderationCase, report, request.ActorUserId, now);
                    break;
                case ModerationActionType.HideContent:
                    await ApplyContentVisibilityAsync(moderationCase, report, hide: true, now, cancellationToken);
                    break;
                case ModerationActionType.RestoreContent:
                    await ApplyContentVisibilityAsync(moderationCase, report, hide: false, now, cancellationToken);
                    break;
                case ModerationActionType.Resolve:
                    Resolve(moderationCase, report, dismissed: false, now);
                    break;
                case ModerationActionType.Dismiss:
                    Resolve(moderationCase, report, dismissed: true, now);
                    break;
                default:
                    throw new DomainRuleException("MODERATION.ACTION_INVALID", "The moderation action is not supported.");
            }

            dbContext.ModerationActions.Add(pendingAction);
            AddAudit(
                request.ActorUserId,
                $"moderation.action-{actionType}",
                "ModerationCase",
                moderationCase.Id,
                request.AuditReason,
                now);
        }
        catch (DomainRuleException exception)
        {
            return Result.Failure<ModerationCaseResponse>(ResultError.BusinessRule(exception.Code, exception.Message));
        }

        return Result.Success(await MapCaseAsync(moderationCase, report, cancellationToken, pendingAction));
    }

    private static void StartReview(
        ModerationCase moderationCase,
        ContentReport report,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (moderationCase.Status != ModerationCaseStatus.Open || report.Status != ContentReportStatus.Open)
        {
            throw new DomainRuleException("MODERATION.CASE_NOT_OPEN", "Only an open moderation case can enter review.");
        }

        moderationCase.StartReview(actorUserId, now);
        report.StartReview(now);
    }

    private static void Resolve(
        ModerationCase moderationCase,
        ContentReport report,
        bool dismissed,
        DateTimeOffset now)
    {
        moderationCase.EnsureInReview();
        if (report.Status != ContentReportStatus.InReview)
        {
            throw new DomainRuleException("REPORT.NOT_IN_REVIEW", "The report must be in review before it can be closed.");
        }

        moderationCase.Close(dismissed, now);
        report.Resolve(dismissed, now);
    }

    private async Task ApplyContentVisibilityAsync(
        ModerationCase moderationCase,
        ContentReport report,
        bool hide,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        moderationCase.EnsureInReview();
        if (report.Status != ContentReportStatus.InReview)
        {
            throw new DomainRuleException("MODERATION.CASE_NOT_IN_REVIEW", "The report must be in review before changing content visibility.");
        }
        if (report.ReviewId is { } reviewId)
        {
            var identity = await dbContext.CourseReviews.AsNoTracking()
                .Where(item => item.Id == reviewId)
                .Select(item => new { item.UserId, item.CourseId })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new DomainRuleException("MODERATION.TARGET_NOT_FOUND", "The reported review was not found.");
            await LockAsync($"course-review:{identity.UserId:D}:{identity.CourseId:D}", cancellationToken);
            CourseReview review = await dbContext.CourseReviews.SingleOrDefaultAsync(
                item => item.Id == reviewId,
                cancellationToken)
                ?? throw new DomainRuleException("MODERATION.TARGET_NOT_FOUND", "The reported review was not found.");
            _ = hide ? review.Hide(now) : review.Restore(now);
            moderationCase.RecordDecision(now);
            return;
        }
        if (report.CommentId is { } commentId)
        {
            await LockAsync($"discussion-comment:{commentId:D}", cancellationToken);
            DiscussionComment comment = await dbContext.DiscussionComments.SingleOrDefaultAsync(
                item => item.Id == commentId,
                cancellationToken)
                ?? throw new DomainRuleException("MODERATION.TARGET_NOT_FOUND", "The reported comment was not found.");
            _ = hide ? comment.Hide(now) : comment.Restore(now);
            moderationCase.RecordDecision(now);
            return;
        }

        throw new DomainRuleException(
            "MODERATION.TARGET_ACTION_UNSUPPORTED",
            "Visibility actions are only supported for reviews and comments.");
    }

    private async Task<bool> HasReportTargetAccessAsync(
        Guid userId,
        CreateContentReportCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CourseId is { } courseId)
        {
            return await dbContext.Courses.AsNoTracking().AnyAsync(
                course => course.Id == courseId && course.Status == CourseStatus.Published &&
                    course.ActiveReleaseId != null && course.DeletedAt == null,
                cancellationToken);
        }
        if (request.ReviewId is { } reviewId)
        {
            return await (
                from review in dbContext.CourseReviews.AsNoTracking()
                join course in dbContext.Courses.AsNoTracking() on review.CourseId equals course.Id
                where review.Id == reviewId && review.Status == CourseReviewStatus.Published &&
                    course.Status == CourseStatus.Published && course.ActiveReleaseId != null && course.DeletedAt == null
                select review.Id).AnyAsync(cancellationToken);
        }
        if (request.CommentId is { } commentId)
        {
            var target = await (
                from comment in dbContext.DiscussionComments.AsNoTracking()
                join thread in dbContext.DiscussionThreads.AsNoTracking() on comment.ThreadId equals thread.Id
                where comment.Id == commentId && comment.Status == DiscussionCommentStatus.Published &&
                    thread.Status == DiscussionStatus.Published
                select new { thread.CourseId, thread.ReleaseId }).SingleOrDefaultAsync(cancellationToken);
            return target is not null &&
                (await HasLearningScopeAsync(userId, target.CourseId, target.ReleaseId, cancellationToken) ||
                    await CanManageCourseAsync(userId, target.CourseId, cancellationToken));
        }
        if (request.ReportedUserId is not { } reportedUserId || request.ContextCommentId is not { } contextCommentId ||
            reportedUserId == userId)
        {
            return false;
        }

        var context = await (
            from comment in dbContext.DiscussionComments.AsNoTracking()
            join thread in dbContext.DiscussionThreads.AsNoTracking() on comment.ThreadId equals thread.Id
            join user in dbContext.Users.AsNoTracking() on comment.AuthorUserId equals user.Id
            where comment.Id == contextCommentId && comment.AuthorUserId == reportedUserId &&
                comment.Status == DiscussionCommentStatus.Published && thread.Status == DiscussionStatus.Published &&
                user.IsActive
            select new { thread.CourseId, thread.ReleaseId }).SingleOrDefaultAsync(cancellationToken);
        return context is not null &&
            (await HasLearningScopeAsync(userId, context.CourseId, context.ReleaseId, cancellationToken) ||
                await CanManageCourseAsync(userId, context.CourseId, cancellationToken));
    }

    private async Task<bool> HasOpenDuplicateAsync(
        CreateContentReportCommand request,
        CancellationToken cancellationToken)
    {
        IQueryable<ContentReport> query = dbContext.ContentReports.AsNoTracking().Where(item =>
            item.ReporterUserId == request.UserId &&
            (item.Status == ContentReportStatus.Open || item.Status == ContentReportStatus.InReview));
        if (request.CourseId is { } courseId) return await query.AnyAsync(item => item.CourseId == courseId, cancellationToken);
        if (request.ReviewId is { } reviewId) return await query.AnyAsync(item => item.ReviewId == reviewId, cancellationToken);
        if (request.CommentId is { } commentId) return await query.AnyAsync(item => item.CommentId == commentId, cancellationToken);
        return await query.AnyAsync(item => item.ReportedUserId == request.ReportedUserId, cancellationToken);
    }

    private async Task<bool> HasLearningScopeAsync(
        Guid userId,
        Guid courseId,
        Guid releaseId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join entitlement in dbContext.Entitlements.AsNoTracking() on enrollment.EntitlementId equals entitlement.Id
            where enrollment.UserId == userId && enrollment.CourseId == courseId && enrollment.ReleaseId == releaseId &&
                (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Completed) &&
                entitlement.Status == EntitlementStatus.Active &&
                (entitlement.ExpiresAt == null || entitlement.ExpiresAt > now)
            select enrollment.Id).AnyAsync(cancellationToken);
    }

    private async Task<bool> CanManageCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking().AnyAsync(
            course => course.Id == courseId && course.DeletedAt == null &&
                (course.OwnerUserId == userId || dbContext.CourseInstructors.Any(instructor =>
                    instructor.CourseId == courseId && instructor.UserId == userId &&
                    instructor.Role != CourseCollaboratorRole.Reviewer)),
            cancellationToken) || await HasPermissionAsync(userId, Permissions.CourseManageAny, cancellationToken);

    private Task<bool> CanModerateAsync(Guid userId, CancellationToken cancellationToken) =>
        HasPermissionAsync(userId, Permissions.ModerationReviewAny, cancellationToken);

    private async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        await dbContext.UserClaims.AsNoTracking().AnyAsync(
            claim => claim.UserId == userId && claim.ClaimType == IdentityConstants.PermissionClaimType &&
                claim.ClaimValue == permission,
            cancellationToken) || await dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.RoleClaims.AsNoTracking(), role => role.RoleId, claim => claim.RoleId, (role, claim) => new { role, claim })
            .AnyAsync(item => item.role.UserId == userId &&
                item.claim.ClaimType == IdentityConstants.PermissionClaimType && item.claim.ClaimValue == permission,
                cancellationToken);

    private async Task<ModerationCaseResponse> MapCaseAsync(
        ModerationCase moderationCase,
        ContentReport report,
        CancellationToken cancellationToken,
        ModerationAction? pendingAction = null)
    {
        ModerationAction[] actions = await dbContext.ModerationActions.AsNoTracking()
            .Where(item => item.CaseId == moderationCase.Id)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (pendingAction is not null)
        {
            actions = [.. actions, pendingAction];
        }
        Guid[] userIds = [
            report.ReporterUserId,
            moderationCase.AssignedToUserId ?? Guid.Empty,
            .. actions.Select(action => action.ActorUserId),
        ];
        Dictionary<Guid, string> names = await GetDisplayNamesAsync(
            userIds.Where(id => id != Guid.Empty).Distinct().ToArray(),
            cancellationToken);
        return new ModerationCaseResponse(
            MapCaseSummary(
                moderationCase,
                report,
                names.GetValueOrDefault(report.ReporterUserId, "User"),
                moderationCase.AssignedToUserId is { } assignedId ? names.GetValueOrDefault(assignedId) : null),
            actions.Select(action => new ModerationActionResponse(
                action.Id,
                action.CaseId,
                action.ActorUserId,
                names.GetValueOrDefault(action.ActorUserId, "Admin"),
                action.ActionType.ToString(),
                action.Reason,
                action.CreatedAt)).ToArray(),
            await GetTargetPreviewAsync(report, cancellationToken));
    }

    private async Task<ModerationTargetPreviewResponse> GetTargetPreviewAsync(
        ContentReport report,
        CancellationToken cancellationToken)
    {
        if (report.ReviewId is { } reviewId)
        {
            var review = await (
                from item in dbContext.CourseReviews.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on item.UserId equals user.Id
                where item.Id == reviewId
                select new { item.Status, item.Text, user.DisplayName }).SingleOrDefaultAsync(cancellationToken);
            return review is null
                ? UnavailablePreview()
                : new(review.Status.ToString(), "Course review", review.Text, review.DisplayName);
        }
        if (report.CommentId is { } commentId)
        {
            var comment = await (
                from item in dbContext.DiscussionComments.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on item.AuthorUserId equals user.Id
                where item.Id == commentId
                select new { item.Status, item.Body, user.DisplayName }).SingleOrDefaultAsync(cancellationToken);
            return comment is null
                ? UnavailablePreview()
                : new(comment.Status.ToString(), "Discussion comment", comment.Body, comment.DisplayName);
        }
        if (report.CourseId is { } courseId)
        {
            Course? course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == courseId,
                cancellationToken);
            if (course is null)
            {
                return UnavailablePreview();
            }

            string title = await dbContext.CourseLocalizations.AsNoTracking()
                .Where(item => item.CourseId == course.Id && item.Locale == course.DefaultLocale)
                .Select(item => item.Title)
                .SingleOrDefaultAsync(cancellationToken) ?? "Course";
            string author = await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == course.OwnerUserId)
                .Select(user => user.DisplayName)
                .SingleOrDefaultAsync(cancellationToken) ?? "Instructor";
            return new(course.Status.ToString(), title, string.Empty, author);
        }

        string? accountName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == report.ReportedUserId)
            .Select(user => user.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        return accountName is null
            ? UnavailablePreview()
            : new("Active", accountName, string.Empty, accountName);
    }

    private static ModerationTargetPreviewResponse UnavailablePreview() =>
        new("Unavailable", "Unavailable target", string.Empty, string.Empty);

    private static ModerationCaseSummaryResponse MapCaseSummary(
        ModerationCase moderationCase,
        ContentReport report,
        string reporterName,
        string? assignedName) => new(
        moderationCase.Id,
        report.Id,
        moderationCase.Status.ToString(),
        moderationCase.AssignedToUserId,
        assignedName,
        moderationCase.Version,
        moderationCase.CreatedAt,
        moderationCase.UpdatedAt,
        moderationCase.ClosedAt,
        MapAdminReport(report, moderationCase, reporterName));

    private static AdminContentReportResponse MapAdminReport(
        ContentReport report,
        ModerationCase moderationCase,
        string reporterName) => new(
        MapReport(report),
        report.ReporterUserId,
        reporterName,
        moderationCase.Id,
        moderationCase.Status.ToString());

    private static ContentReportResponse MapReport(ContentReport report)
    {
        (string kind, Guid id) = report.CourseId is { } courseId
            ? ("Course", courseId)
            : report.ReviewId is { } reviewId
                ? ("Review", reviewId)
                : report.CommentId is { } commentId
                    ? ("Comment", commentId)
                    : ("ReportedUser", report.ReportedUserId!.Value);
        return new ContentReportResponse(
            report.Id,
            kind,
            id,
            report.Reason.ToString(),
            report.Details,
            report.Status.ToString(),
            report.CreatedAt,
            report.UpdatedAt,
            report.ClosedAt);
    }

    private Task<Dictionary<Guid, string>> GetDisplayNamesAsync(
        Guid[] userIds,
        CancellationToken cancellationToken) => dbContext.Users.AsNoTracking()
        .Where(user => userIds.Contains(user.Id))
        .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

    private Task<int> LockAsync(string identity, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))",
            cancellationToken);

    private void AddAudit(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string? reason,
        DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", reason, now));

    private static string? NormalizeTargetKind(string? value) => value?.Trim() switch
    {
        null or "" => null,
        "course" or "Course" => "Course",
        "review" or "Review" => "Review",
        "comment" or "Comment" => "Comment",
        "reportedUser" or "ReportedUser" => "ReportedUser",
        _ => value.Trim(),
    };

    private static string TargetIdentity(Guid? courseId, Guid? reviewId, Guid? commentId, Guid? reportedUserId) =>
        courseId is { } course ? $"course:{course:D}" :
        reviewId is { } review ? $"review:{review:D}" :
        commentId is { } comment ? $"comment:{comment:D}" : $"user:{reportedUserId!.Value:D}";

    private static TEnum? ParseStatus<TEnum>(string? value)
        where TEnum : struct, Enum => string.IsNullOrWhiteSpace(value)
        ? null
        : TryParseNamedEnum(value, out TEnum parsed) ? parsed : null;

    private static bool TryParseNamedEnum<TEnum>(string value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        string normalized = value.Trim();
        if (!Enum.TryParse(normalized, true, out parsed) || !Enum.IsDefined(parsed))
        {
            return false;
        }

        return string.Equals(Enum.GetName(parsed), normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static Result<T> NotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "MODERATION.NOT_FOUND", "The moderation resource was not found."));

    private static Result<T> Forbidden<T>() => Result.Failure<T>(ResultError.Forbidden(
        "MODERATION.PERMISSION_REQUIRED", "Moderation permission is required."));

    private static Result<T> CursorInvalid<T>() => Result.Failure<T>(ResultError.BusinessRule(
        "CURSOR.INVALID", "The moderation cursor is invalid or does not match this query."));
}
