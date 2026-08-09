using Dorosak.Domain.Common;

namespace Dorosak.Domain.Assessment;

public enum AssessmentVersionStatus
{
    Draft,
    Ready,
}

public enum QuizQuestionType
{
    SingleChoice,
    MultipleChoice,
    TrueFalse,
    ShortAnswer,
}

public enum QuizAttemptStatus
{
    InProgress,
    Expired,
    Submitted,
    PendingManualGrade,
    Graded,
}

public sealed class Quiz
{
    private Quiz()
    {
    }

    private Quiz(Guid id, Guid courseId, Guid lessonId, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        LessonId = lessonId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Quiz Create(Guid courseId, Guid lessonId, Guid actorUserId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), courseId, lessonId, actorUserId, now);
}

public sealed class QuizVersion
{
    private QuizVersion()
    {
    }

    private QuizVersion(Guid id, Guid quizId, int versionNumber, string title, int attemptLimit, int? durationMinutes, DateTimeOffset? deadline, decimal passScore, DateTimeOffset now)
    {
        Id = id;
        QuizId = quizId;
        VersionNumber = versionNumber;
        Title = title;
        AttemptLimit = attemptLimit;
        DurationMinutes = durationMinutes;
        Deadline = deadline;
        PassScore = passScore;
        Status = AssessmentVersionStatus.Draft;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid QuizId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int AttemptLimit { get; private set; }
    public int? DurationMinutes { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public decimal PassScore { get; private set; }
    public AssessmentVersionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }

    public static QuizVersion Create(Guid quizId, int versionNumber, string title, int attemptLimit, int? durationMinutes, DateTimeOffset? deadline, decimal passScore, DateTimeOffset now)
    {
        if (versionNumber <= 0 || attemptLimit is < 1 or > 100 || durationMinutes is <= 0 or > 1440 || passScore is < 0 or > 100)
        {
            throw new DomainRuleException("QUIZ.VERSION_POLICY_INVALID", "The quiz version policy is invalid.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new QuizVersion(Guid.CreateVersion7(), quizId, versionNumber, title.Trim(), attemptLimit, durationMinutes, deadline, passScore, now);
    }

    public void MarkReady(int questionCount, DateTimeOffset now)
    {
        if (Status == AssessmentVersionStatus.Ready)
        {
            return;
        }
        if (questionCount <= 0)
        {
            throw new DomainRuleException("QUIZ.QUESTIONS_REQUIRED", "A quiz requires at least one question.");
        }
        Status = AssessmentVersionStatus.Ready;
        ReadyAt = now;
    }
}

public sealed class QuizQuestion
{
    private QuizQuestion()
    {
    }

    private QuizQuestion(Guid id, Guid quizVersionId, int position, QuizQuestionType type, string prompt, decimal points, string? acceptedAnswer)
    {
        Id = id;
        QuizVersionId = quizVersionId;
        Position = position;
        Type = type;
        Prompt = prompt;
        Points = points;
        AcceptedAnswer = string.IsNullOrWhiteSpace(acceptedAnswer) ? null : acceptedAnswer.Trim();
    }

    public Guid Id { get; private set; }
    public Guid QuizVersionId { get; private set; }
    public int Position { get; private set; }
    public QuizQuestionType Type { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public decimal Points { get; private set; }
    public string? AcceptedAnswer { get; private set; }

    public static QuizQuestion Create(Guid quizVersionId, int position, QuizQuestionType type, string prompt, decimal points, string? acceptedAnswer = null)
    {
        if (quizVersionId == Guid.Empty || position < 0 || points <= 0 || string.IsNullOrWhiteSpace(prompt))
        {
            throw new DomainRuleException("QUIZ.QUESTION_INVALID", "Question position and points are invalid.");
        }
        if (type != QuizQuestionType.ShortAnswer && !string.IsNullOrWhiteSpace(acceptedAnswer))
        {
            throw new DomainRuleException("QUIZ.ACCEPTED_ANSWER_INVALID", "Accepted text answers are only valid for short-answer questions.");
        }
        return new QuizQuestion(Guid.CreateVersion7(), quizVersionId, position, type, prompt.Trim(), points, acceptedAnswer);
    }
}

public sealed class QuizQuestionOption
{
    private QuizQuestionOption()
    {
    }

    private QuizQuestionOption(Guid id, Guid questionId, int position, string text, bool isCorrect)
    {
        Id = id;
        QuestionId = questionId;
        Position = position;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    public static QuizQuestionOption Create(Guid questionId, int position, string text, bool isCorrect)
    {
        if (questionId == Guid.Empty || position < 0 || string.IsNullOrWhiteSpace(text))
        {
            throw new DomainRuleException("QUIZ.OPTION_INVALID", "Question option data is invalid.");
        }

        return new QuizQuestionOption(Guid.CreateVersion7(), questionId, position, text.Trim(), isCorrect);
    }
}

public sealed class QuizAttempt
{
    private QuizAttempt()
    {
    }

    private QuizAttempt(Guid id, Guid enrollmentId, Guid quizVersionId, int attemptNumber, DateTimeOffset startedAt, DateTimeOffset? expiresAt)
    {
        Id = id;
        EnrollmentId = enrollmentId;
        QuizVersionId = quizVersionId;
        AttemptNumber = attemptNumber;
        Status = QuizAttemptStatus.InProgress;
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid QuizVersionId { get; private set; }
    public int AttemptNumber { get; private set; }
    public QuizAttemptStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public decimal? Score { get; private set; }
    public bool? Passed { get; private set; }

    public static QuizAttempt Start(Guid enrollmentId, Guid quizVersionId, int attemptNumber, DateTimeOffset startedAt, int? durationMinutes) =>
        new(Guid.CreateVersion7(), enrollmentId, quizVersionId, attemptNumber, startedAt, durationMinutes is null ? null : startedAt.AddMinutes(durationMinutes.Value));

    public void Submit(decimal objectiveScore, bool requiresManualGrade, decimal passScore, DateTimeOffset now)
    {
        if (Status != QuizAttemptStatus.InProgress)
        {
            throw new DomainRuleException("QUIZ.ATTEMPT_ALREADY_SUBMITTED", "The quiz attempt was already submitted.");
        }
        if (ExpiresAt is { } expiresAt && now > expiresAt)
        {
            throw new DomainRuleException("QUIZ.ATTEMPT_EXPIRED", "The quiz attempt duration has elapsed.");
        }

        Score = Math.Clamp(objectiveScore, 0, 100);
        SubmittedAt = now;
        Status = requiresManualGrade ? QuizAttemptStatus.PendingManualGrade : QuizAttemptStatus.Graded;
        Passed = requiresManualGrade ? null : Score >= passScore;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status != QuizAttemptStatus.InProgress)
        {
            return;
        }

        Status = QuizAttemptStatus.Expired;
        SubmittedAt = now;
        Passed = false;
    }

    public void ApplyManualGrade(decimal score, decimal passScore)
    {
        if (Status is not (QuizAttemptStatus.PendingManualGrade or QuizAttemptStatus.Graded))
        {
            throw new DomainRuleException("QUIZ.ATTEMPT_NOT_GRADABLE", "The quiz attempt is not ready for grading.");
        }
        Score = Math.Clamp(score, 0, 100);
        Passed = Score >= passScore;
        Status = QuizAttemptStatus.Graded;
    }
}

public sealed class QuizAnswer
{
    private QuizAnswer()
    {
    }

    private QuizAnswer(Guid attemptId, Guid questionId, string? textAnswer, string selectedOptionIds, decimal? awardedPoints)
    {
        AttemptId = attemptId;
        QuestionId = questionId;
        TextAnswer = textAnswer;
        SelectedOptionIds = selectedOptionIds;
        AwardedPoints = awardedPoints;
    }

    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string? TextAnswer { get; private set; }
    public string SelectedOptionIds { get; private set; } = string.Empty;
    public decimal? AwardedPoints { get; private set; }

    public static QuizAnswer Create(Guid attemptId, Guid questionId, string? textAnswer, IEnumerable<Guid> selectedOptionIds, decimal? awardedPoints) =>
        new(attemptId, questionId, string.IsNullOrWhiteSpace(textAnswer) ? null : textAnswer.Trim(), string.Join(',', selectedOptionIds.Order()), awardedPoints);
}

public sealed class Assignment
{
    private Assignment()
    {
    }

    private Assignment(Guid id, Guid courseId, Guid lessonId, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        LessonId = lessonId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Assignment Create(Guid courseId, Guid lessonId, Guid actorUserId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), courseId, lessonId, actorUserId, now);
}

public sealed class AssignmentVersion
{
    private AssignmentVersion()
    {
    }

    private AssignmentVersion(Guid id, Guid assignmentId, int versionNumber, string title, string instructions, DateTimeOffset? deadline, bool allowMultipleSubmissions, DateTimeOffset now)
    {
        Id = id;
        AssignmentId = assignmentId;
        VersionNumber = versionNumber;
        Title = title;
        Instructions = instructions;
        Deadline = deadline;
        AllowMultipleSubmissions = allowMultipleSubmissions;
        Status = AssessmentVersionStatus.Draft;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Instructions { get; private set; } = string.Empty;
    public DateTimeOffset? Deadline { get; private set; }
    public bool AllowMultipleSubmissions { get; private set; }
    public AssessmentVersionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }

    public static AssignmentVersion Create(Guid assignmentId, int versionNumber, string title, string instructions, DateTimeOffset? deadline, bool allowMultipleSubmissions, DateTimeOffset now)
    {
        if (versionNumber <= 0)
        {
            throw new DomainRuleException("ASSIGNMENT.VERSION_INVALID", "The assignment version number is invalid.");
        }
        return new AssignmentVersion(Guid.CreateVersion7(), assignmentId, versionNumber, title.Trim(), instructions.Trim(), deadline, allowMultipleSubmissions, now);
    }

    public void MarkReady(DateTimeOffset now)
    {
        Status = AssessmentVersionStatus.Ready;
        ReadyAt ??= now;
    }
}

public sealed class AssignmentSubmission
{
    private AssignmentSubmission()
    {
    }

    private AssignmentSubmission(Guid id, Guid enrollmentId, Guid assignmentVersionId, int submissionNumber, string text, DateTimeOffset submittedAt)
    {
        Id = id;
        EnrollmentId = enrollmentId;
        AssignmentVersionId = assignmentVersionId;
        SubmissionNumber = submissionNumber;
        Text = text;
        SubmittedAt = submittedAt;
    }

    public Guid Id { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid AssignmentVersionId { get; private set; }
    public int SubmissionNumber { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; private set; }

    public static AssignmentSubmission Submit(Guid enrollmentId, Guid assignmentVersionId, int submissionNumber, string text, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string value = text.Trim();
        if (value.Length > 100000)
        {
            throw new DomainRuleException("ASSIGNMENT.TEXT_TOO_LONG", "Assignment text cannot exceed 100000 characters.");
        }
        return new AssignmentSubmission(Guid.CreateVersion7(), enrollmentId, assignmentVersionId, submissionNumber, value, now);
    }
}

public sealed class AssignmentSubmissionFile
{
    private AssignmentSubmissionFile()
    {
    }

    private AssignmentSubmissionFile(
        Guid id,
        Guid submissionId,
        Guid assetId,
        Guid clientFileId,
        DateTimeOffset createdAt)
    {
        Id = id;
        SubmissionId = submissionId;
        AssetId = assetId;
        ClientFileId = clientFileId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid ClientFileId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AssignmentSubmissionFile Create(
        Guid submissionId,
        Guid assetId,
        Guid clientFileId,
        DateTimeOffset now)
    {
        if (submissionId == Guid.Empty || assetId == Guid.Empty || clientFileId == Guid.Empty)
        {
            throw new DomainRuleException("ASSIGNMENT.FILE_INVALID", "Assignment file identifiers are required.");
        }

        return new AssignmentSubmissionFile(Guid.CreateVersion7(), submissionId, assetId, clientFileId, now);
    }
}

public sealed class GradeRevision
{
    private GradeRevision()
    {
    }

    private GradeRevision(Guid id, Guid submissionId, int revisionNumber, decimal score, string feedback, Guid gradedByUserId, DateTimeOffset gradedAt)
    {
        Id = id;
        SubmissionId = submissionId;
        RevisionNumber = revisionNumber;
        Score = score;
        Feedback = feedback;
        GradedByUserId = gradedByUserId;
        GradedAt = gradedAt;
    }

    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public int RevisionNumber { get; private set; }
    public decimal Score { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public Guid GradedByUserId { get; private set; }
    public DateTimeOffset GradedAt { get; private set; }

    public static GradeRevision Create(Guid submissionId, int revisionNumber, decimal score, string feedback, Guid gradedByUserId, DateTimeOffset now)
    {
        if (revisionNumber <= 0 || score is < 0 or > 100)
        {
            throw new DomainRuleException("GRADE.INVALID", "The grade is invalid.");
        }
        ArgumentNullException.ThrowIfNull(feedback);
        return new GradeRevision(Guid.CreateVersion7(), submissionId, revisionNumber, score, feedback.Trim(), gradedByUserId, now);
    }
}

public sealed class QuizGradeRevision
{
    private QuizGradeRevision()
    {
    }

    private QuizGradeRevision(Guid id, Guid attemptId, int revisionNumber, decimal score, string feedback, Guid gradedByUserId, DateTimeOffset gradedAt)
    {
        Id = id;
        AttemptId = attemptId;
        RevisionNumber = revisionNumber;
        Score = score;
        Feedback = feedback;
        GradedByUserId = gradedByUserId;
        GradedAt = gradedAt;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public int RevisionNumber { get; private set; }
    public decimal Score { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public Guid GradedByUserId { get; private set; }
    public DateTimeOffset GradedAt { get; private set; }

    public static QuizGradeRevision Create(Guid attemptId, int revisionNumber, decimal score, string feedback, Guid gradedByUserId, DateTimeOffset now)
    {
        if (attemptId == Guid.Empty || revisionNumber <= 0 || score is < 0 or > 100)
        {
            throw new DomainRuleException("GRADE.INVALID", "The grade is invalid.");
        }
        ArgumentNullException.ThrowIfNull(feedback);
        return new QuizGradeRevision(Guid.CreateVersion7(), attemptId, revisionNumber, score, feedback.Trim(), gradedByUserId, now);
    }
}
