using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Commerce;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Commerce;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Commerce;

internal sealed class CommerceService(DorosakDbContext dbContext, TimeProvider timeProvider) : ICommerceService
{
    private const decimal DemoPriceCredits = 100m;

    public async Task<Result<DemoCheckoutResponse>> CreateDemoCheckoutAsync(
        CreateDemoCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"demo-checkout:{request.UserId:D}:{request.CourseId:D}"}, 0))",
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        Course? course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.CourseId && candidate.Status == CourseStatus.Published &&
                candidate.ActiveReleaseId != null && candidate.DeletedAt == null,
            cancellationToken);
        if (course?.ActiveReleaseId is not { } releaseId)
        {
            return Result.Failure<DemoCheckoutResponse>(ResultError.NotFound(
                "COMMERCE.COURSE_NOT_AVAILABLE", "The course is not available for demo checkout."));
        }

        Enrollment? enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            candidate => candidate.UserId == request.UserId && candidate.CourseId == request.CourseId &&
                (candidate.Status == EnrollmentStatus.Active || candidate.Status == EnrollmentStatus.Completed ||
                 candidate.Status == EnrollmentStatus.Suspended), cancellationToken);
        if (enrollment is not null && request.Outcome == "success")
        {
            return Result.Failure<DemoCheckoutResponse>(ResultError.Conflict(
                "COMMERCE.ALREADY_ENROLLED", "The learner already has access to this course."));
        }

        DemoOrder order = DemoOrder.Create(request.UserId, request.CourseId, DemoPriceCredits, now);
        bool succeeds = request.Outcome == "success";
        DemoPayment payment = DemoPayment.Create(order.Id, DemoPriceCredits,
            succeeds ? DemoPaymentStatus.Succeeded : DemoPaymentStatus.Failed, now);
        dbContext.DemoOrders.Add(order);
        dbContext.DemoPayments.Add(payment);
        if (!succeeds)
        {
            order.Fail(now);
            AddAudit(request.UserId, "commerce.demo-payment-failed", order.Id, "Simulated failure", now);
            return Result.Success(Map(order, payment, null));
        }

        if (enrollment is null)
        {
            Entitlement? entitlement = await dbContext.Entitlements.SingleOrDefaultAsync(
                candidate => candidate.UserId == request.UserId && candidate.CourseId == request.CourseId &&
                    candidate.Status == EntitlementStatus.Active &&
                    (candidate.ExpiresAt == null || candidate.ExpiresAt > now), cancellationToken);
            if (entitlement is null)
            {
                entitlement = Entitlement.GrantDemo(request.UserId, request.CourseId, now);
                dbContext.Entitlements.Add(entitlement);
            }
            enrollment = Enrollment.Create(request.UserId, request.CourseId, releaseId, entitlement.Id, now);
            dbContext.Enrollments.Add(enrollment);
        }
        order.Complete(now);
        AddAudit(request.UserId, "commerce.demo-payment-succeeded", order.Id, request.CourseId.ToString("D"), now);
        return Result.Success(Map(order, payment, enrollment.Id));
    }

    public async Task<Result<DemoSubscriptionStateResponse>> GetDemoSubscriptionAsync(
        GetDemoSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        DemoSubscription? subscription = await dbContext.DemoSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        return Result.Success(new DemoSubscriptionStateResponse(subscription is null ? null : Map(subscription)));
    }

    public async Task<Result<DemoSubscriptionResponse>> ActivateDemoSubscriptionAsync(
        ActivateDemoSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"demo-subscription:{request.UserId:D}"}, 0))",
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DemoSubscription? subscription = await dbContext.DemoSubscriptions
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (subscription is null)
        {
            subscription = DemoSubscription.Create(request.UserId, now);
            dbContext.DemoSubscriptions.Add(subscription);
        }
        else
        {
            subscription.Activate(now);
        }

        AddAudit(request.UserId, "commerce.demo-subscription-activated", "DemoSubscription", subscription.Id, subscription.PlanCode, now);
        return Result.Success(Map(subscription));
    }

    public async Task<Result<DemoSubscriptionResponse>> CancelDemoSubscriptionAsync(
        CancelDemoSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        DemoSubscription? subscription = await dbContext.DemoSubscriptions.SingleOrDefaultAsync(
            item => item.Id == request.SubscriptionId && item.UserId == request.UserId,
            cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<DemoSubscriptionResponse>(ResultError.NotFound(
                "COMMERCE.SUBSCRIPTION_NOT_FOUND", "The demo subscription was not found."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        subscription.Cancel(now);
        AddAudit(request.UserId, "commerce.demo-subscription-cancelled", "DemoSubscription", subscription.Id, subscription.PlanCode, now);
        return Result.Success(Map(subscription));
    }

    private static DemoCheckoutResponse Map(DemoOrder order, DemoPayment payment, Guid? enrollmentId) => new(
        order.Id, payment.Id, order.CourseId, enrollmentId, order.Status.ToString(), payment.Status.ToString(),
        order.TotalCredits, order.Currency);

    private static DemoSubscriptionResponse Map(DemoSubscription subscription) => new(
        subscription.Id,
        subscription.PlanCode,
        subscription.Status.ToString(),
        subscription.ActivatedAt,
        subscription.UpdatedAt,
        subscription.CancelledAt);

    private void AddAudit(Guid actorUserId, string action, Guid orderId, string reason, DateTimeOffset now) =>
        AddAudit(actorUserId, action, "DemoOrder", orderId, reason, now);

    private void AddAudit(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string reason,
        DateTimeOffset now) =>
        dbContext.AuditLogs.Add(AuditLog.Create(actorUserId, action, targetType, targetId, "Succeeded", reason, now));
}
