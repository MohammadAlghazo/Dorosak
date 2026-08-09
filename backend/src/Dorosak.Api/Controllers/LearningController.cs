using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Learning;
using Dorosak.Application.Features.Media;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}")]
[EnableRateLimiting(ApiConstants.SensitiveRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LearningController(ISender sender) : ControllerBase
{
    [HttpPost("courses/{courseId:guid}/enroll")]
    [PermissionPolicy(Permissions.EnrollmentCreateOwn)]
    public async Task<IActionResult> Enroll(Guid courseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryGetIdempotencyKey(out string idempotencyKey))
        {
            return MissingIdempotencyKey<EnrollmentResponse>();
        }
        Result<EnrollmentResponse> result = await sender.Send(
            new EnrollCourseCommand(userId, courseId, GetLocale(), idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("me/enrollments")]
    [PermissionPolicy(Permissions.EnrollmentReadOwn)]
    public async Task<IActionResult> GetEnrollments(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<IReadOnlyList<EnrollmentResponse>> result = await sender.Send(
            new GetEnrollmentsQuery(userId, GetLocale()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/manifest")]
    [PermissionPolicy(Permissions.LearningAccessOwn)]
    public async Task<IActionResult> GetManifest(Guid enrollmentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningManifestResponse> result = await sender.Send(
            new GetLearningManifestQuery(userId, enrollmentId, GetLocale()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}")]
    [PermissionPolicy(Permissions.LearningAccessOwn)]
    public async Task<IActionResult> GetLesson(Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningLessonResponse> result = await sender.Send(
            new GetLearningLessonQuery(userId, enrollmentId, lessonId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/progress")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public async Task<IActionResult> UpdateProgress(
        Guid enrollmentId,
        Guid lessonId,
        UpdateProgressRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<ProgressResponse> result = await sender.Send(
            new UpdateLessonProgressCommand(
                userId,
                enrollmentId,
                lessonId,
                request.ClientCommandId,
                request.Sequence,
                request.PositionSeconds,
                request.WatchedIntervals,
                request.CompletionIntent),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/notes")]
    [PermissionPolicy(Permissions.LearningAccessOwn)]
    public async Task<IActionResult> GetNotes(Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<IReadOnlyList<LearningNoteResponse>> result = await sender.Send(
            new GetLearningNotesQuery(userId, enrollmentId, lessonId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/notes")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public Task<IActionResult> CreateNote(
        Guid enrollmentId,
        Guid lessonId,
        LearningNoteRequest request,
        CancellationToken cancellationToken) => UpsertNote(enrollmentId, lessonId, null, request, cancellationToken);

    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/notes/{noteId:guid}")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public Task<IActionResult> UpdateNote(
        Guid enrollmentId,
        Guid lessonId,
        Guid noteId,
        LearningNoteRequest request,
        CancellationToken cancellationToken) => UpsertNote(enrollmentId, lessonId, noteId, request, cancellationToken);

    [HttpDelete("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/notes/{noteId:guid}")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public async Task<IActionResult> DeleteNote(
        Guid enrollmentId,
        Guid lessonId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningOperationResponse> result = await sender.Send(
            new DeleteLearningNoteCommand(userId, enrollmentId, lessonId, noteId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/bookmark")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public async Task<IActionResult> AddBookmark(Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<BookmarkResponse> result = await sender.Send(
            new AddBookmarkCommand(userId, enrollmentId, lessonId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/bookmark")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public async Task<IActionResult> DeleteBookmark(Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningOperationResponse> result = await sender.Send(
            new DeleteBookmarkCommand(userId, enrollmentId, lessonId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/lessons/{lessonId:guid}/recently-viewed")]
    [PermissionPolicy(Permissions.ProgressUpdateOwn)]
    public async Task<IActionResult> MarkRecentlyViewed(Guid enrollmentId, Guid lessonId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningOperationResponse> result = await sender.Send(
            new MarkRecentlyViewedCommand(userId, enrollmentId, lessonId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/quizzes/{quizVersionId:guid}/attempts")]
    [PermissionPolicy(Permissions.QuizAttemptOwn)]
    public async Task<IActionResult> StartQuizAttempt(Guid enrollmentId, Guid quizVersionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryGetIdempotencyKey(out string idempotencyKey))
        {
            return MissingIdempotencyKey<QuizAttemptResponse>();
        }
        Result<QuizAttemptResponse> result = await sender.Send(
            new StartQuizAttemptCommand(userId, enrollmentId, quizVersionId, idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/quizzes/{quizVersionId:guid}/attempts/{attemptId:guid}")]
    [PermissionPolicy(Permissions.QuizAttemptOwn)]
    public async Task<IActionResult> GetQuizAttempt(
        Guid enrollmentId,
        Guid quizVersionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<QuizAttemptResponse> result = await sender.Send(
            new GetQuizAttemptQuery(userId, enrollmentId, quizVersionId, attemptId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/quizzes/{quizVersionId:guid}/attempts/{attemptId:guid}/submit")]
    [PermissionPolicy(Permissions.QuizAttemptOwn)]
    public async Task<IActionResult> SubmitQuizAttempt(
        Guid enrollmentId,
        Guid quizVersionId,
        Guid attemptId,
        SubmitQuizAttemptRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryGetIdempotencyKey(out string idempotencyKey))
        {
            return MissingIdempotencyKey<QuizAttemptResponse>();
        }
        Result<QuizAttemptResponse> result = await sender.Send(
            new SubmitQuizAttemptCommand(userId, enrollmentId, quizVersionId, attemptId, request.Answers, idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/assignments/{assignmentVersionId:guid}/submissions")]
    [PermissionPolicy(Permissions.AssignmentSubmitOwn)]
    public async Task<IActionResult> SubmitAssignment(
        Guid enrollmentId,
        Guid assignmentVersionId,
        SubmitAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryGetIdempotencyKey(out string idempotencyKey))
        {
            return MissingIdempotencyKey<AssignmentSubmissionResponse>();
        }
        Result<AssignmentSubmissionResponse> result = await sender.Send(
            new SubmitAssignmentCommand(userId, enrollmentId, assignmentVersionId, request.Text, idempotencyKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("learning/enrollments/{enrollmentId:guid}/assignments/{assignmentVersionId:guid}/submissions/{submissionId:guid}")]
    [PermissionPolicy(Permissions.AssignmentSubmitOwn)]
    public async Task<IActionResult> GetAssignmentSubmission(
        Guid enrollmentId,
        Guid assignmentVersionId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<AssignmentSubmissionResponse> result = await sender.Send(
            new GetAssignmentSubmissionQuery(userId, enrollmentId, assignmentVersionId, submissionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("learning/enrollments/{enrollmentId:guid}/assignments/{assignmentVersionId:guid}/files")]
    [PermissionPolicy(Permissions.AssignmentSubmitOwn)]
    [EnableRateLimiting(ApiConstants.UploadRateLimitPolicy)]
    public async Task<IActionResult> CreateAssignmentFile(
        Guid enrollmentId,
        Guid assignmentVersionId,
        CreateAssignmentFileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        if (!TryGetIdempotencyKey(out string idempotencyKey))
        {
            return MissingIdempotencyKey<UploadSessionResponse>();
        }
        Result<UploadSessionResponse> result = await sender.Send(
            new CreateUploadSessionCommand(
                userId,
                Dorosak.Domain.Media.MediaPurpose.AssignmentSubmission.ToString(),
                request.ExpectedBytes,
                request.FileName,
                request.ContentType,
                null,
                idempotencyKey,
                EnrollmentId: enrollmentId,
                AssignmentVersionId: assignmentVersionId,
                AssignmentSubmissionId: request.SubmissionId,
                ClientFileId: request.ClientFileId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/lessons/{lessonId:guid}/quizzes/versions")]
    [PermissionPolicy(Permissions.AssessmentManageCourse)]
    public async Task<IActionResult> CreateQuizVersion(
        Guid courseId,
        Guid lessonId,
        CreateQuizVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<QuizVersionResponse> result = await sender.Send(
            new CreateQuizVersionCommand(
                userId,
                courseId,
                lessonId,
                request.Title,
                request.AttemptLimit,
                request.DurationMinutes,
                request.Deadline,
                request.PassScore,
                request.Questions),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/quizzes/versions/{versionId:guid}/ready")]
    [PermissionPolicy(Permissions.AssessmentManageCourse)]
    public async Task<IActionResult> MarkQuizReady(Guid courseId, Guid versionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<QuizVersionResponse> result = await sender.Send(
            new MarkQuizVersionReadyCommand(userId, courseId, versionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/quiz-attempts/{attemptId:guid}/grade")]
    [RecentAuthenticationPolicy(Permissions.SubmissionGradeCourse)]
    public async Task<IActionResult> GradeQuiz(
        Guid courseId,
        Guid attemptId,
        GradeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<GradeResponse> result = await sender.Send(
            new GradeQuizAttemptCommand(userId, courseId, attemptId, request.Score, request.Feedback ?? string.Empty, GetAuditReason()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/lessons/{lessonId:guid}/assignments/versions")]
    [PermissionPolicy(Permissions.AssessmentManageCourse)]
    public async Task<IActionResult> CreateAssignmentVersion(
        Guid courseId,
        Guid lessonId,
        CreateAssignmentVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<AssignmentVersionResponse> result = await sender.Send(
            new CreateAssignmentVersionCommand(
                userId,
                courseId,
                lessonId,
                request.Title,
                request.Instructions,
                request.Deadline,
                request.AllowMultipleSubmissions),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/assignments/versions/{versionId:guid}/ready")]
    [PermissionPolicy(Permissions.AssessmentManageCourse)]
    public async Task<IActionResult> MarkAssignmentReady(Guid courseId, Guid versionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<AssignmentVersionResponse> result = await sender.Send(
            new MarkAssignmentVersionReadyCommand(userId, courseId, versionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("instructor/courses/{courseId:guid}/assignment-submissions/{submissionId:guid}/grade")]
    [RecentAuthenticationPolicy(Permissions.SubmissionGradeCourse)]
    public async Task<IActionResult> GradeAssignment(
        Guid courseId,
        Guid submissionId,
        GradeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<GradeResponse> result = await sender.Send(
            new GradeAssignmentCommand(userId, courseId, submissionId, request.Score, request.Feedback ?? string.Empty, GetAuditReason()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<IActionResult> UpsertNote(
        Guid enrollmentId,
        Guid lessonId,
        Guid? noteId,
        LearningNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }
        Result<LearningNoteResponse> result = await sender.Send(
            new UpsertLearningNoteCommand(userId, enrollmentId, lessonId, noteId, request.Text),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private bool TryGetIdempotencyKey(out string value)
    {
        value = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? string.Empty;
        return value.Length is > 0 and <= 200;
    }

    private IActionResult MissingIdempotencyKey<T>() => this.ToActionResult(
        Result.Failure<T>(ResultError.Validation(
            new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] })));

    private string GetLocale()
    {
        string value = Request.GetTypedHeaders().AcceptLanguage?.FirstOrDefault()?.Value.Value ?? "ar";
        return value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    }

    private string GetAuditReason() => Request.Headers["X-Audit-Reason"].FirstOrDefault()?.Trim() ?? string.Empty;
}

public sealed record UpdateProgressRequest(
    Guid ClientCommandId,
    long Sequence,
    decimal PositionSeconds,
    IReadOnlyList<WatchedIntervalInput> WatchedIntervals,
    bool CompletionIntent);

public sealed record LearningNoteRequest(string Text);

public sealed record SubmitQuizAttemptRequest(IReadOnlyList<QuizAnswerInput> Answers);

public sealed record SubmitAssignmentRequest(string Text);

public sealed record CreateAssignmentFileRequest(
    Guid SubmissionId,
    Guid ClientFileId,
    long ExpectedBytes,
    string FileName,
    string ContentType);

public sealed record CreateQuizVersionRequest(
    string Title,
    int AttemptLimit,
    int? DurationMinutes,
    DateTimeOffset? Deadline,
    decimal PassScore,
    IReadOnlyList<QuizQuestionInput> Questions);

public sealed record CreateAssignmentVersionRequest(
    string Title,
    string Instructions,
    DateTimeOffset? Deadline,
    bool AllowMultipleSubmissions);

public sealed record GradeRequest(decimal Score, string? Feedback);
