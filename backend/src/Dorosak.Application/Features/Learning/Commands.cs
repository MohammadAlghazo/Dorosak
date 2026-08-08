using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Learning;

public sealed record EnrollCourseCommand(
    Guid UserId,
    Guid CourseId,
    string Locale,
    string IdempotencyKey) : IIdempotentCommand<EnrollmentResponse>
{
    public string IdempotencyOperation => "learning.enroll-free.v1";
    public string IdempotencyScope => $"user:{UserId:D}";
    public object IdempotencyPayload => new { CourseId, Locale };
    public int ResponseSchemaVersion => 1;
    public TimeSpan Retention => TimeSpan.FromHours(24);
}

public sealed record GetEnrollmentsQuery(Guid UserId, string Locale) : IQuery<IReadOnlyList<EnrollmentResponse>>;

public sealed record GetLearningManifestQuery(Guid UserId, Guid EnrollmentId, string Locale)
    : IQuery<LearningManifestResponse>;

public sealed record GetLearningLessonQuery(Guid UserId, Guid EnrollmentId, Guid LessonId)
    : IQuery<LearningLessonResponse>;

public sealed record UpdateLessonProgressCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid LessonId,
    Guid ClientCommandId,
    long Sequence,
    decimal PositionSeconds,
    IReadOnlyList<WatchedIntervalInput> WatchedIntervals,
    bool CompletionIntent) : IIdempotentCommand<ProgressResponse>
{
    public string IdempotencyOperation => "learning.progress-update.v1";
    public string IdempotencyKey => ClientCommandId.ToString("D");
    public string IdempotencyScope => $"enrollment:{EnrollmentId:D}";
    public object IdempotencyPayload => new
    {
        LessonId,
        Sequence,
        PositionSeconds,
        WatchedIntervals,
        CompletionIntent,
    };
    public int ResponseSchemaVersion => 1;
    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record GetLearningNotesQuery(Guid UserId, Guid EnrollmentId, Guid LessonId)
    : IQuery<IReadOnlyList<LearningNoteResponse>>;

public sealed record UpsertLearningNoteCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid LessonId,
    Guid? NoteId,
    string Text) : ITransactionalCommand<LearningNoteResponse>;

public sealed record DeleteLearningNoteCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid LessonId,
    Guid NoteId) : ITransactionalCommand<LearningOperationResponse>;

public sealed record AddBookmarkCommand(Guid UserId, Guid EnrollmentId, Guid LessonId)
    : ITransactionalCommand<BookmarkResponse>;

public sealed record DeleteBookmarkCommand(Guid UserId, Guid EnrollmentId, Guid LessonId)
    : ITransactionalCommand<LearningOperationResponse>;

public sealed record MarkRecentlyViewedCommand(Guid UserId, Guid EnrollmentId, Guid LessonId)
    : ITransactionalCommand<LearningOperationResponse>;

public sealed record CreateQuizVersionCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid LessonId,
    string Title,
    int AttemptLimit,
    int? DurationMinutes,
    DateTimeOffset? Deadline,
    decimal PassScore,
    IReadOnlyList<QuizQuestionInput> Questions) : ITransactionalCommand<QuizVersionResponse>;

public sealed record MarkQuizVersionReadyCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid VersionId) : ITransactionalCommand<QuizVersionResponse>;

public sealed record StartQuizAttemptCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid QuizVersionId,
    string IdempotencyKey) : IIdempotentCommand<QuizAttemptResponse>
{
    public string IdempotencyOperation => "assessment.quiz-start.v1";
    public string IdempotencyScope => $"user:{UserId:D}";
    public object IdempotencyPayload => new { EnrollmentId, QuizVersionId };
    public int ResponseSchemaVersion => 1;
    public TimeSpan Retention => TimeSpan.FromHours(24);
}

public sealed record GetQuizAttemptQuery(
    Guid UserId,
    Guid EnrollmentId,
    Guid QuizVersionId,
    Guid AttemptId) : IQuery<QuizAttemptResponse>;

public sealed record SubmitQuizAttemptCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid QuizVersionId,
    Guid AttemptId,
    IReadOnlyList<QuizAnswerInput> Answers,
    string IdempotencyKey) : IIdempotentCommand<QuizAttemptResponse>
{
    public string IdempotencyOperation => "assessment.quiz-submit.v1";
    public string IdempotencyScope => $"user:{UserId:D}";
    public object IdempotencyPayload => new { EnrollmentId, QuizVersionId, AttemptId, Answers };
    public int ResponseSchemaVersion => 1;
    public TimeSpan Retention => TimeSpan.FromDays(7);
}

public sealed record GradeQuizAttemptCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid AttemptId,
    decimal Score,
    string Feedback,
    string AuditReason) : ITransactionalCommand<GradeResponse>;

public sealed record CreateAssignmentVersionCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid LessonId,
    string Title,
    string Instructions,
    DateTimeOffset? Deadline,
    bool AllowMultipleSubmissions) : ITransactionalCommand<AssignmentVersionResponse>;

public sealed record MarkAssignmentVersionReadyCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid VersionId) : ITransactionalCommand<AssignmentVersionResponse>;

public sealed record SubmitAssignmentCommand(
    Guid UserId,
    Guid EnrollmentId,
    Guid AssignmentVersionId,
    string Text,
    string IdempotencyKey) : IIdempotentCommand<AssignmentSubmissionResponse>
{
    public string IdempotencyOperation => "assessment.assignment-submit.v1";
    public string IdempotencyScope => $"user:{UserId:D}";
    public object IdempotencyPayload => new { EnrollmentId, AssignmentVersionId, Text };
    public int ResponseSchemaVersion => 1;
    public TimeSpan Retention => TimeSpan.FromDays(7);
}

public sealed record GradeAssignmentCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid SubmissionId,
    decimal Score,
    string Feedback,
    string AuditReason) : ITransactionalCommand<GradeResponse>;
