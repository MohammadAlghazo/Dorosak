using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Learning;

public interface ILearningService
{
    Task<Result<EnrollmentResponse>> EnrollAsync(EnrollCourseCommand request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<EnrollmentResponse>>> GetEnrollmentsAsync(GetEnrollmentsQuery request, CancellationToken cancellationToken);
    Task<Result<LearningManifestResponse>> GetManifestAsync(GetLearningManifestQuery request, CancellationToken cancellationToken);
    Task<Result<LearningLessonResponse>> GetLessonAsync(GetLearningLessonQuery request, CancellationToken cancellationToken);
    Task<Result<ProgressResponse>> UpdateProgressAsync(UpdateLessonProgressCommand request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<LearningNoteResponse>>> GetNotesAsync(GetLearningNotesQuery request, CancellationToken cancellationToken);
    Task<Result<LearningNoteResponse>> UpsertNoteAsync(UpsertLearningNoteCommand request, CancellationToken cancellationToken);
    Task<Result<LearningOperationResponse>> DeleteNoteAsync(DeleteLearningNoteCommand request, CancellationToken cancellationToken);
    Task<Result<BookmarkResponse>> AddBookmarkAsync(AddBookmarkCommand request, CancellationToken cancellationToken);
    Task<Result<LearningOperationResponse>> DeleteBookmarkAsync(DeleteBookmarkCommand request, CancellationToken cancellationToken);
    Task<Result<LearningOperationResponse>> MarkRecentlyViewedAsync(MarkRecentlyViewedCommand request, CancellationToken cancellationToken);
    Task<Result<QuizVersionResponse>> CreateQuizVersionAsync(CreateQuizVersionCommand request, CancellationToken cancellationToken);
    Task<Result<QuizVersionResponse>> MarkQuizVersionReadyAsync(MarkQuizVersionReadyCommand request, CancellationToken cancellationToken);
    Task<Result<QuizAttemptResponse>> StartQuizAttemptAsync(StartQuizAttemptCommand request, CancellationToken cancellationToken);
    Task<Result<QuizAttemptResponse>> GetQuizAttemptAsync(GetQuizAttemptQuery request, CancellationToken cancellationToken);
    Task<Result<QuizAttemptResponse>> SubmitQuizAttemptAsync(SubmitQuizAttemptCommand request, CancellationToken cancellationToken);
    Task<Result<GradeResponse>> GradeQuizAttemptAsync(GradeQuizAttemptCommand request, CancellationToken cancellationToken);
    Task<Result<AssignmentVersionResponse>> CreateAssignmentVersionAsync(CreateAssignmentVersionCommand request, CancellationToken cancellationToken);
    Task<Result<AssignmentVersionResponse>> MarkAssignmentVersionReadyAsync(MarkAssignmentVersionReadyCommand request, CancellationToken cancellationToken);
    Task<Result<AssignmentSubmissionResponse>> SubmitAssignmentAsync(SubmitAssignmentCommand request, CancellationToken cancellationToken);
    Task<Result<GradeResponse>> GradeAssignmentAsync(GradeAssignmentCommand request, CancellationToken cancellationToken);
}
