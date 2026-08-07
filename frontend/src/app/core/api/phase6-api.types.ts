export type ContentLocale = 'ar' | 'en';
export type CourseLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'AllLevels';
export type LessonType = 'Video' | 'Article' | 'Document' | 'Quiz' | 'Assignment';
export type TeacherApplicationStatus =
  'Pending' | 'InReview' | 'Approved' | 'Rejected' | 'Withdrawn';
export type CourseStatus =
  'Draft' | 'InReview' | 'ChangesRequested' | 'ReadyToPublish' | 'Archived';

export interface CursorPage<T> {
  items: readonly T[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface VersionedResult<T> {
  value: T;
  etag: string;
}

export interface TeacherApplicationRequest {
  headline: string;
  biography: string;
  expertise: string;
  motivation: string;
}

export interface TeacherApplication {
  id: string;
  userId: string;
  headline: string;
  biography: string;
  expertise: string;
  motivation: string;
  status: TeacherApplicationStatus;
  reviewerReason: string | null;
  submittedAt: string;
  updatedAt: string;
}

export interface CourseLocalizationInput {
  locale: ContentLocale;
  title: string;
  subtitle: string;
  description: string;
  slug?: string;
}

export interface CourseMetadataRequest {
  defaultLocale: ContentLocale;
  level: CourseLevel;
  localizations: readonly CourseLocalizationInput[];
  categoryCodes: readonly string[];
  tagCodes: readonly string[];
}

export type CourseCreateRequest = CourseMetadataRequest;

export interface CourseSummary {
  id: string;
  defaultLocale: ContentLocale;
  status: CourseStatus;
  draftVersion: number;
  createdAt: string;
  updatedAt: string;
  title: string | null;
  slug: string | null;
}

export interface CourseLocalization extends Omit<CourseLocalizationInput, 'slug'> {
  slug: string;
}

export interface CourseCollaborator {
  userId: string;
  role: 'Editor' | 'CoInstructor' | 'Reviewer';
  addedAt: string;
}

export interface CourseDetails {
  id: string;
  ownerUserId: string;
  defaultLocale: ContentLocale;
  status: CourseStatus;
  draftVersion: number;
  level: CourseLevel;
  categoryCodes: readonly string[];
  tagCodes: readonly string[];
  localizations: readonly CourseLocalization[];
  collaborators: readonly CourseCollaborator[];
  createdAt: string;
  updatedAt: string;
}

export interface CourseMutation {
  courseId: string;
  status: CourseStatus;
  draftVersion: number;
}

export interface LessonInput {
  id: string | null;
  position: number;
  title: string;
  lessonType: LessonType;
  content: string;
}

export interface SectionInput {
  id: string | null;
  position: number;
  title: string;
  lessons: readonly LessonInput[];
}

export interface Lesson extends Omit<LessonInput, 'id'> {
  id: string;
}

export interface CourseSection extends Omit<SectionInput, 'id' | 'lessons'> {
  id: string;
  lessons: readonly Lesson[];
}

export interface Curriculum {
  draftVersion: number;
  sections: readonly CourseSection[];
}

export interface PublicationStatus {
  courseId: string;
  courseStatus: CourseStatus;
  reviewId: string | null;
  reviewStatus: string | null;
  reviewerReason: string | null;
  draftVersion: number;
}

export interface PublicationReview {
  id: string;
  courseId: string;
  draftId: string;
  draftVersion: number;
  requestedByUserId: string;
  status: string;
  reviewerReason: string | null;
  requestedAt: string;
  updatedAt: string;
}

export interface TaxonomyLocalization {
  locale: ContentLocale;
  name: string;
}

export interface Category {
  id: string;
  code: string;
  parentId: string | null;
  displayOrder: number;
  isActive: boolean;
  localizations: readonly TaxonomyLocalization[];
}

export interface Tag {
  id: string;
  code: string;
  isActive: boolean;
  localizations: readonly TaxonomyLocalization[];
}

export interface CategoryUpsertRequest {
  code: string;
  parentId: string | null;
  displayOrder: number;
  isActive: boolean;
  localizations: readonly TaxonomyLocalization[];
}

export interface TagUpsertRequest {
  code: string;
  isActive: boolean;
  localizations: readonly TaxonomyLocalization[];
}
