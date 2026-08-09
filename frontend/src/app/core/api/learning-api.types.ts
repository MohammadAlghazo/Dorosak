export interface Enrollment {
  id: string;
  courseId: string;
  releaseId: string;
  status: 'Active' | 'Completed' | 'Suspended';
  enrolledAt: string;
  title: string;
  slug: string;
}

export interface LearningManifest {
  enrollmentId: string;
  courseId: string;
  releaseId: string;
  status: string;
  locale: 'ar' | 'en';
  title: string;
  slug: string;
  sections: readonly LearningSection[];
  nextLessonId: string | null;
}

export interface LearningSection {
  id: string;
  position: number;
  title: string;
  lessons: readonly LearningLessonSummary[];
}

export interface LearningLessonSummary {
  id: string;
  position: number;
  title: string;
  lessonType: string;
  completionRequirement: number;
  isCompleted: boolean;
  positionSeconds: number;
  quizVersionId: string | null;
  assignmentVersionId: string | null;
}

export interface LearningLesson {
  enrollmentId: string;
  releaseId: string;
  id: string;
  sectionId: string;
  position: number;
  title: string;
  lessonType: string;
  content: string;
  completionRequirement: number;
  isCompleted: boolean;
  positionSeconds: number;
  mediaVariants: readonly LearningMediaVariant[];
  captions: readonly LearningCaption[];
  quizVersionId: string | null;
  assignmentVersionId: string | null;
}

export interface LearningMediaVariant {
  assetId: string;
  variantId: string;
  kind: string;
  contentType: string;
  bytes: number;
  width: number | null;
  height: number | null;
  durationSeconds: number | null;
}

export interface LearningCaption {
  assetId: string;
  captionId: string;
  locale: string;
  label: string;
}

export interface WatchedInterval {
  startSeconds: number;
  endSeconds: number;
}

export interface UpdateProgressRequest {
  clientCommandId: string;
  sequence: number;
  positionSeconds: number;
  watchedIntervals: readonly WatchedInterval[];
  completionIntent: boolean;
}

export interface Progress {
  enrollmentId: string;
  lessonId: string;
  lastSequence: number;
  positionSeconds: number;
  isCompleted: boolean;
  completedAt: string | null;
  applied: boolean;
}

export interface LearningNote {
  id: string;
  enrollmentId: string;
  lessonId: string;
  text: string;
  createdAt: string;
  updatedAt: string;
}

export interface QuizAnswerInput {
  questionId: string;
  textAnswer: string | null;
  selectedOptionIds: readonly string[];
}

export interface QuizAttempt {
  id: string;
  enrollmentId: string;
  quizVersionId: string;
  attemptNumber: number;
  status: 'InProgress' | 'PendingManualGrade' | 'Graded';
  startedAt: string;
  expiresAt: string | null;
  submittedAt: string | null;
  score: number | null;
  passed: boolean | null;
  questions: readonly QuizAttemptQuestion[];
}

export interface QuizAttemptQuestion {
  id: string;
  position: number;
  type: 'SingleChoice' | 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer';
  prompt: string;
  points: number;
  options: readonly QuizAttemptOption[];
}

export interface QuizAttemptOption {
  id: string;
  position: number;
  text: string;
}

export interface AssignmentSubmission {
  id: string;
  enrollmentId: string;
  assignmentVersionId: string;
  submissionNumber: number;
  text: string;
  submittedAt: string;
  score: number | null;
  feedback: string | null;
  gradeRevisionNumber: number;
  files: readonly AssignmentSubmissionFile[];
}

export interface AssignmentSubmissionFile {
  id: string;
  assetId: string;
  clientFileId: string;
  fileName: string;
  contentType: string;
  declaredBytes: number;
  state:
    | 'Initiated'
    | 'Uploaded'
    | 'Scanning'
    | 'Processing'
    | 'Ready'
    | 'Rejected'
    | 'RecoveryPending'
    | 'Deleted';
  rejectionCode: string | null;
  createdAt: string;
  readyAt: string | null;
}

export interface CourseLearner {
  userId: string;
  displayName: string;
  enrollments: readonly CourseLearnerEnrollment[];
}

export interface CourseLearnerEnrollment {
  enrollmentId: string;
  releaseId: string;
  status: string;
  enrolledAt: string;
}

export type AssessmentAudienceType = 'AllEnrolled' | 'SelectedLearners';

export interface QuizOptionInput {
  position: number;
  text: string;
  isCorrect: boolean;
}

export interface QuizQuestionInput {
  position: number;
  type: 'SingleChoice' | 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer';
  prompt: string;
  points: number;
  acceptedAnswer: string | null;
  options: readonly QuizOptionInput[];
}

export interface CreateQuizVersionRequest {
  title: string;
  attemptLimit: number;
  durationMinutes: number | null;
  deadline: string | null;
  passScore: number;
  questions: readonly QuizQuestionInput[];
  audienceType: AssessmentAudienceType;
  selectedLearnerUserIds: readonly string[];
}

export interface QuizVersion {
  quizId: string;
  versionId: string;
  courseId: string;
  lessonId: string;
  versionNumber: number;
  title: string;
  status: 'Draft' | 'Ready';
  attemptLimit: number;
  durationMinutes: number | null;
  deadline: string | null;
  passScore: number;
  audienceType: AssessmentAudienceType;
  selectedLearnerUserIds: readonly string[];
}

export interface CreateAssignmentVersionRequest {
  title: string;
  instructions: string;
  deadline: string | null;
  allowMultipleSubmissions: boolean;
  audienceType: AssessmentAudienceType;
  selectedLearnerUserIds: readonly string[];
}

export interface AssignmentVersion {
  assignmentId: string;
  versionId: string;
  courseId: string;
  lessonId: string;
  versionNumber: number;
  title: string;
  instructions: string;
  status: 'Draft' | 'Ready';
  deadline: string | null;
  allowMultipleSubmissions: boolean;
  audienceType: AssessmentAudienceType;
  selectedLearnerUserIds: readonly string[];
}

export interface CourseRelease {
  courseId: string;
  releaseId: string;
  releaseNumber: number;
  manifestHash: string;
  state: 'Active' | 'Superseded' | 'Unpublished';
  publishedAt: string;
  projectionGeneration: number;
}
