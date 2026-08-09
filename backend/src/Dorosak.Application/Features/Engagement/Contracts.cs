namespace Dorosak.Application.Features.Engagement;

public sealed record CourseReviewResponse(
    Guid Id,
    Guid CourseId,
    Guid UserId,
    string AuthorName,
    short Rating,
    string Text,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CourseReviewPageResponse(
    IReadOnlyList<CourseReviewResponse> Items,
    decimal AverageRating,
    int TotalCount,
    bool HasMore);

public sealed record EngagementOperationResponse(bool Completed);
