export type ContentReportReason =
  'Spam' | 'Harassment' | 'HateSpeech' | 'Misinformation' | 'Copyright' | 'PersonalData' | 'Other';

export type ModerationWorkflowStatus = 'Open' | 'InReview' | 'Resolved' | 'Dismissed';
export type ContentReportStatus = ModerationWorkflowStatus;
export type ModerationCaseStatus = ModerationWorkflowStatus;
export type ContentReportTargetKind = 'Course' | 'Review' | 'Comment' | 'ReportedUser';
export type ModerationActionType =
  'StartReview' | 'HideContent' | 'RestoreContent' | 'Resolve' | 'Dismiss';

type ContentReportTarget =
  | {
      courseId: string;
      reviewId?: never;
      commentId?: never;
      reportedUserId?: never;
    }
  | {
      courseId?: never;
      reviewId: string;
      commentId?: never;
      reportedUserId?: never;
    }
  | {
      courseId?: never;
      reviewId?: never;
      commentId: string;
      reportedUserId?: never;
    }
  | {
      courseId?: never;
      reviewId?: never;
      commentId?: never;
      reportedUserId: string;
      contextCommentId: string;
    };

export type CreateContentReportRequest = ContentReportTarget & {
  reason: ContentReportReason;
  details?: string;
};

export interface ContentReportResponse {
  id: string;
  targetKind: ContentReportTargetKind;
  targetId: string;
  reason: ContentReportReason;
  details: string;
  status: ContentReportStatus;
  createdAt: string;
  updatedAt: string;
  closedAt: string | null;
}

export interface AdminContentReportResponse {
  report: ContentReportResponse;
  reporterUserId: string;
  reporterName: string;
  caseId: string;
  caseStatus: ModerationCaseStatus;
}

export interface ContentReportPageResponse {
  items: readonly AdminContentReportResponse[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface ModerationCaseSummaryResponse {
  id: string;
  reportId: string;
  status: ModerationCaseStatus;
  assignedToUserId: string | null;
  assignedToName: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
  closedAt: string | null;
  report: AdminContentReportResponse;
}

export interface ModerationCasePageResponse {
  items: readonly ModerationCaseSummaryResponse[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface ModerationActionResponse {
  id: string;
  caseId: string;
  actorUserId: string;
  actorName: string;
  actionType: ModerationActionType;
  reason: string;
  createdAt: string;
}

export interface ModerationTargetPreviewResponse {
  status: string;
  title: string;
  body: string;
  authorName: string;
}

export interface ModerationCaseResponse {
  case: ModerationCaseSummaryResponse;
  actions: readonly ModerationActionResponse[];
  targetPreview: ModerationTargetPreviewResponse;
}

export interface AdminContentReportQuery {
  status?: ContentReportStatus | null;
  targetKind?: ContentReportTargetKind | null;
  limit?: number;
  cursor?: string | null;
}

export interface ModerationCaseQuery {
  status?: ModerationCaseStatus | null;
  limit?: number;
  cursor?: string | null;
}

export interface ModerationActionRequest {
  action: ModerationActionType;
  reason: string;
  expectedVersion: number;
}
