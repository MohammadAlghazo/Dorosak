export interface CourseReview {
  id: string;
  courseId: string;
  userId: string;
  authorName: string;
  rating: number;
  text: string;
  status: 'Published' | 'Hidden' | 'Removed';
  createdAt: string;
  updatedAt: string;
}

export interface CourseReviewPage {
  items: readonly CourseReview[];
  averageRating: number;
  totalCount: number;
  hasMore: boolean;
}

export type DiscussionStatus = 'Published' | 'Hidden' | 'Removed';

export interface DiscussionThreadPage {
  items: readonly DiscussionThreadSummary[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface DiscussionThreadSummary {
  id: string;
  lessonId: string | null;
  authorUserId: string;
  authorName: string;
  title: string;
  body: string;
  status: DiscussionStatus;
  isEdited: boolean;
  createdAt: string;
  updatedAt: string;
  commentCount: number;
  canEdit: boolean;
  canDelete: boolean;
}

export interface DiscussionThread {
  id: string;
  courseId: string;
  releaseId: string;
  lessonId: string | null;
  authorUserId: string;
  authorName: string;
  title: string;
  body: string;
  status: DiscussionStatus;
  isEdited: boolean;
  createdAt: string;
  updatedAt: string;
  commentCount: number;
  canEdit: boolean;
  canDelete: boolean;
  comments: DiscussionCommentPage;
}

export interface DiscussionCommentPage {
  items: readonly DiscussionComment[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface DiscussionComment {
  id: string;
  threadId: string;
  parentCommentId: string | null;
  authorUserId: string;
  authorName: string;
  body: string;
  depth: 0 | 1 | 2;
  status: DiscussionStatus;
  isEdited: boolean;
  likeCount: number;
  likedByViewer: boolean;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
}

export interface CommentLikeResult {
  commentId: string;
  liked: boolean;
  likeCount: number;
}
