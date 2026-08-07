import type { Locale } from '../i18n/locale';

export type PublicLoadStatus =
  'idle' | 'loading' | 'refreshing' | 'success' | 'empty' | 'error' | 'offline';

export const catalogSorts = ['newest', 'title', 'popular'] as const;
export type CatalogSort = (typeof catalogSorts)[number];

export const courseLevels = ['beginner', 'intermediate', 'advanced'] as const;
export type CourseLevel = (typeof courseLevels)[number];

export const priceTypes = ['free', 'paid'] as const;
export type PriceType = (typeof priceTypes)[number];

export const durationBands = ['short', 'medium', 'long'] as const;
export type DurationBand = (typeof durationBands)[number];

export interface CatalogFilters {
  category: string | null;
  tag: string | null;
  language: Locale | null;
  level: CourseLevel | null;
  price: PriceType | null;
  duration: DurationBand | null;
  instructor: string | null;
  sort: CatalogSort;
}

export interface CatalogCoursesRequest extends CatalogFilters {
  cursor: string | null;
  limit: number;
}

export interface PublicTaxonomyTerm {
  id: string;
  code: string;
  name: string;
}

export interface PublicCategory extends PublicTaxonomyTerm {
  parentId: string | null;
}

export interface PublicInstructor {
  id: string;
  displayName: string;
}

export interface PublicCoursePrice {
  type: PriceType;
  amount: string | null;
  currency: string | null;
}

export interface PublicCourseSummary {
  courseId: string;
  releaseId: string;
  slug: string;
  title: string;
  summary: string;
  language: Locale;
  level: CourseLevel;
  durationMinutes: number;
  instructors: readonly PublicInstructor[];
  categories: readonly PublicTaxonomyTerm[];
  tags: readonly PublicTaxonomyTerm[];
  price: PublicCoursePrice | null;
}

export interface CursorPage<T> {
  items: readonly T[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface CourseLocalizationLink {
  locale: Locale;
  slug: string;
}

export interface PublicCourseDetail extends PublicCourseSummary {
  locale: Locale;
  defaultLocale: Locale;
  description: string;
  learningOutcomes: readonly string[];
  localizations: readonly CourseLocalizationLink[];
}

export interface HighlightSegment {
  text: string;
  matched: boolean;
}

export interface PublicSearchResult extends PublicCourseSummary {
  titleHighlight: readonly HighlightSegment[];
  summaryHighlight: readonly HighlightSegment[];
}

export interface PublicSearchPage extends CursorPage<PublicSearchResult> {
  correction: string | null;
}

export interface PublicSearchSuggestion {
  slug: string | null;
  segments: readonly HighlightSegment[];
}

export interface SearchRequest extends Omit<CatalogCoursesRequest, 'sort'> {
  query: string;
  sort: CatalogSort | 'relevance';
}
