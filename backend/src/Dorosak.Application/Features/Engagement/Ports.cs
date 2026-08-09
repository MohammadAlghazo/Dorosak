using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Engagement;

public interface IEngagementService
{
    Task<Result<CourseReviewPageResponse>> GetCourseReviewsAsync(
        GetCourseReviewsQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> GetMyCourseReviewAsync(
        GetMyCourseReviewQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> CreateCourseReviewAsync(
        CreateCourseReviewCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseReviewResponse>> UpdateCourseReviewAsync(
        UpdateCourseReviewCommand request,
        CancellationToken cancellationToken);

    Task<Result<EngagementOperationResponse>> DeleteCourseReviewAsync(
        DeleteCourseReviewCommand request,
        CancellationToken cancellationToken);
}
