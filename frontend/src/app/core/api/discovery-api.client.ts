import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
import { LocaleService } from '../i18n/locale.service';
import {
  type CatalogCoursesRequest,
  type CursorPage,
  type HighlightSegment,
  type PublicCategory,
  type PublicCourseDetail,
  type PublicCourseSummary,
  type PublicSearchPage,
  type PublicSearchSuggestion,
  type PublicTaxonomyTerm,
  type SearchRequest,
} from './discovery-api.types';

@Injectable({ providedIn: 'root' })
export class DiscoveryApiClient {
  private readonly http = inject(HttpClient);
  private readonly locale = inject(LocaleService);

  getCourses(request: CatalogCoursesRequest): Observable<CursorPage<PublicCourseSummary>> {
    return this.http
      .get<ApiEnvelope<CursorPage<PublicCourseSummary>>>('catalog/courses', {
        context: publicReadContext(),
        params: discoveryParams(request),
      })
      .pipe(map((response) => releaseBackedPage(response.data)));
  }

  getCourse(slug: string): Observable<PublicCourseDetail> {
    return this.http
      .get<ApiEnvelope<PublicCourseDetail>>(`catalog/courses/${encodeURIComponent(slug)}`, {
        context: publicReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  getCategories(): Observable<readonly PublicCategory[]> {
    return this.http
      .get<ApiEnvelope<CursorPage<CategoryDto>>>('catalog/categories', {
        context: publicReadContext(),
        params: new HttpParams().set('limit', 100),
      })
      .pipe(
        map((response) =>
          response.data.items.map((category) => ({
            id: category.id,
            code: category.code,
            name: localizedName(category.localizations, this.locale.locale(), category.code),
            parentId: category.parentId,
          })),
        ),
      );
  }

  getTags(): Observable<readonly PublicTaxonomyTerm[]> {
    return this.http
      .get<ApiEnvelope<CursorPage<TagDto>>>('catalog/tags', {
        context: publicReadContext(),
        params: new HttpParams().set('limit', 100),
      })
      .pipe(
        map((response) =>
          response.data.items.map((tag) => ({
            id: tag.id,
            code: tag.code,
            name: localizedName(tag.localizations, this.locale.locale(), tag.code),
          })),
        ),
      );
  }

  getFeatured(limit = 3): Observable<readonly PublicCourseSummary[]> {
    return this.http
      .get<ApiEnvelope<CursorPage<PublicCourseSummary>>>('catalog/featured', {
        context: publicReadContext(),
        params: new HttpParams().set('limit', clampLimit(limit)),
      })
      .pipe(map((response) => releaseBackedItems(response.data.items)));
  }

  search(request: SearchRequest): Observable<PublicSearchPage> {
    return this.http
      .get<ApiEnvelope<SearchPageDto>>('search', {
        context: publicReadContext(),
        params: discoveryParams(request).set('q', request.query),
      })
      .pipe(
        map((response) => ({
          ...response.data,
          items: releaseBackedItems(response.data.items).map(normalizeSearchResult),
          correction: response.data.correction ?? null,
        })),
      );
  }

  getSuggestions(query: string): Observable<readonly PublicSearchSuggestion[]> {
    return this.http
      .get<ApiEnvelope<readonly (PublicSearchSuggestion | string)[]>>('search/suggestions', {
        context: publicReadContext(8_000),
        params: new HttpParams().set('q', query).set('limit', 8),
      })
      .pipe(
        map((response) =>
          response.data.slice(0, 8).map((suggestion) =>
            typeof suggestion === 'string'
              ? { slug: null, segments: highlightSuggestion(suggestion, query) }
              : {
                  ...suggestion,
                  segments: normalizeHighlightSegments(suggestion.segments),
                },
          ),
        ),
      );
  }
}

export const normalizeHighlightSegments = (
  segments: readonly HighlightSegment[],
  fallback = '',
): readonly HighlightSegment[] => {
  const safeSegments = segments.filter(
    (segment) => typeof segment.text === 'string' && typeof segment.matched === 'boolean',
  );
  return safeSegments.length > 0 || fallback.length === 0
    ? safeSegments
    : [{ text: fallback, matched: false }];
};

const normalizeSearchResult = (result: PublicSearchPage['items'][number]) => ({
  ...result,
  titleHighlight: normalizeHighlightSegments(result.titleHighlight, result.title),
  summaryHighlight: normalizeHighlightSegments(result.summaryHighlight, result.summary),
});

const publicReadContext = (deadlineMs = 15_000): HttpContext =>
  new HttpContext()
    .set(API_REQUEST, true)
    .set(PUBLIC_API_REQUEST, true)
    .set(DEADLINE_MS, deadlineMs);

const discoveryParams = (request: CatalogCoursesRequest | SearchRequest): HttpParams => {
  let params = new HttpParams().set('limit', clampLimit(request.limit)).set('sort', request.sort);
  const filters: readonly (readonly [string, string | null])[] = [
    ['categoryCode', request.category],
    ['tag', request.tag],
    ['language', request.language],
    ['level', request.level],
    ['price', request.price],
    ['duration', request.duration],
    ['instructor', request.instructor],
    ['cursor', request.cursor],
  ];
  for (const [name, value] of filters) {
    if (value) params = params.set(name, value);
  }
  return params;
};

const releaseBackedPage = <T extends PublicCourseSummary>(page: CursorPage<T>): CursorPage<T> => {
  const items = releaseBackedItems(page.items);
  return {
    items,
    nextCursor: page.nextCursor,
    hasMore: page.hasMore && page.nextCursor !== null,
  };
};

const releaseBackedItems = <T extends PublicCourseSummary>(items: readonly T[]): readonly T[] =>
  items.filter((item) => typeof item.releaseId === 'string' && item.releaseId.trim().length > 0);

const clampLimit = (limit: number): number => Math.min(Math.max(Math.trunc(limit), 1), 100);

interface TaxonomyLocalizationDto {
  locale: string;
  name: string;
}

interface CategoryDto {
  id: string;
  code: string;
  parentId: string | null;
  displayOrder: number;
  localizations: readonly TaxonomyLocalizationDto[];
}

interface TagDto {
  id: string;
  code: string;
  isActive: boolean;
  localizations: readonly TaxonomyLocalizationDto[];
}

interface SearchPageDto extends Omit<PublicSearchPage, 'correction'> {
  correction?: string | null;
}

const localizedName = (
  localizations: readonly TaxonomyLocalizationDto[],
  locale: string,
  fallback: string,
): string => localizations.find((item) => item.locale === locale)?.name ?? fallback;

const highlightSuggestion = (text: string, query: string): readonly HighlightSegment[] => {
  const index = text.toLocaleLowerCase().indexOf(query.toLocaleLowerCase());
  if (index < 0 || query.length === 0) return [{ text, matched: false }];
  return [
    { text: text.slice(0, index), matched: false },
    { text: text.slice(index, index + query.length), matched: true },
    { text: text.slice(index + query.length), matched: false },
  ].filter((segment) => segment.text.length > 0);
};
