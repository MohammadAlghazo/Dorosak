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
