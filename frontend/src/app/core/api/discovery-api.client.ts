import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
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
      .get<ApiEnvelope<readonly PublicCategory[]>>('catalog/categories', {
        context: publicReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  getTags(): Observable<readonly PublicTaxonomyTerm[]> {
    return this.http
      .get<ApiEnvelope<readonly PublicTaxonomyTerm[]>>('catalog/tags', {
        context: publicReadContext(),
      })
      .pipe(map((response) => response.data));
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
      .get<ApiEnvelope<PublicSearchPage>>('search', {
        context: publicReadContext(),
        params: discoveryParams(request).set('q', request.query),
      })
      .pipe(
        map((response) => ({
          ...response.data,
          items: releaseBackedItems(response.data.items).map(normalizeSearchResult),
        })),
      );
  }

  getSuggestions(query: string): Observable<readonly PublicSearchSuggestion[]> {
    return this.http
      .get<ApiEnvelope<readonly PublicSearchSuggestion[]>>('search/suggestions', {
        context: publicReadContext(8_000),
        params: new HttpParams().set('q', query).set('limit', 8),
      })
      .pipe(
        map((response) =>
          response.data.slice(0, 8).map((suggestion) => ({
            ...suggestion,
            segments: normalizeHighlightSegments(suggestion.segments),
          })),
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
  const filters: ReadonlyArray<readonly [string, string | null]> = [
    ['category', request.category],
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
  items.filter((item) => item.releaseId.trim().length > 0);

const clampLimit = (limit: number): number => Math.min(Math.max(Math.trunc(limit), 1), 100);
