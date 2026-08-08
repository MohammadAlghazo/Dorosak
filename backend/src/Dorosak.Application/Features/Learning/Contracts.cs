namespace Dorosak.Application.Features.Learning;

public sealed record EnrollmentResponse(
    Guid Id,
    Guid CourseId,
    Guid ReleaseId,
    string Status,
    DateTimeOffset EnrolledAt,
    string Title,
    string Slug);

public sealed record LearningManifestResponse(
    Guid EnrollmentId,
    Guid CourseId,
    Guid ReleaseId,
    string Status,
    string Locale,
    string Title,
    string Slug,
    IReadOnlyList<LearningSectionResponse> Sections,
    Guid? NextLessonId);

public sealed record LearningSectionResponse(
    Guid Id,
    int Position,
    string Title,
    IReadOnlyList<LearningLessonSummaryResponse> Lessons);

public sealed record LearningLessonSummaryResponse(
    Guid Id,
    int Position,
    string Title,
    string LessonType,
    decimal CompletionRequirement,
    bool IsCompleted,
    decimal PositionSeconds,
    Guid? QuizVersionId,
    Guid? AssignmentVersionId);

public sealed record LearningLessonResponse(
    Guid EnrollmentId,
    Guid ReleaseId,
    Guid Id,
    Guid SectionId,
    int Position,
    string Title,
    string LessonType,
    string Content,
    decimal CompletionRequirement,
    bool IsCompleted,
    decimal PositionSeconds,
    IReadOnlyList<LearningMediaVariantResponse> MediaVariants,
    IReadOnlyList<LearningCaptionResponse> Captions,
    Guid? QuizVersionId,
    Guid? AssignmentVersionId);

public sealed record LearningMediaVariantResponse(
    Guid AssetId,
    Guid VariantId,
    string Kind,
    string ContentType,
    long Bytes,
    int? Width,
    int? Height,
    decimal? DurationSeconds);

public sealed record LearningCaptionResponse(
    Guid AssetId,
    Guid CaptionId,
    string Locale,
    string Label);

public sealed record WatchedIntervalInput(decimal StartSeconds, decimal EndSeconds);

public sealed record ProgressResponse(
    Guid EnrollmentId,
    Guid LessonId,
    long LastSequence,
    decimal PositionSeconds,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    bool Applied);

public sealed record LearningNoteResponse(
    Guid Id,
    Guid EnrollmentId,
    Guid LessonId,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BookmarkResponse(
    Guid EnrollmentId,
    Guid LessonId,
    DateTimeOffset CreatedAt);

public sealed record QuizOptionInput(int Position, string Text, bool IsCorrect);

public sealed record QuizQuestionInput(
    int Position,
    string Type,
    string Prompt,
    decimal Points,
    string? AcceptedAnswer,
    IReadOnlyList<QuizOptionInput> Options);

public sealed record QuizVersionResponse(
    Guid QuizId,
    Guid VersionId,
    Guid CourseId,
    Guid LessonId,
    int VersionNumber,
    string Title,
    string Status,
    int AttemptLimit,
    int? DurationMinutes,
    DateTimeOffset? Deadline,
    decimal PassScore);

public sealed record QuizAnswerInput(
    Guid QuestionId,
    string? TextAnswer,
    IReadOnlyList<Guid> SelectedOptionIds);

public sealed record QuizAttemptQuestionResponse(
    Guid Id,
    int Position,
    string Type,
    string Prompt,
    decimal Points,
    IReadOnlyList<QuizAttemptOptionResponse> Options);

public sealed record QuizAttemptOptionResponse(Guid Id, int Position, string Text);

public sealed record QuizAttemptResponse(
    Guid Id,
    Guid EnrollmentId,
    Guid QuizVersionId,
    int AttemptNumber,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? SubmittedAt,
    decimal? Score,
    bool? Passed,
    IReadOnlyList<QuizAttemptQuestionResponse> Questions);

public sealed record AssignmentVersionResponse(
    Guid AssignmentId,
    Guid VersionId,
    Guid CourseId,
    Guid LessonId,
    int VersionNumber,
    string Title,
    string Instructions,
    string Status,
    DateTimeOffset? Deadline,
    bool AllowMultipleSubmissions);

public sealed record AssignmentSubmissionResponse(
    Guid Id,
    Guid EnrollmentId,
    Guid AssignmentVersionId,
    int SubmissionNumber,
    string Text,
    DateTimeOffset SubmittedAt,
    decimal? Score,
    string? Feedback,
    int GradeRevisionNumber);

public sealed record GradeResponse(
    Guid ResourceId,
    decimal Score,
    string Feedback,
    int RevisionNumber,
    DateTimeOffset GradedAt);

public sealed record LearningOperationResponse(bool Completed);
