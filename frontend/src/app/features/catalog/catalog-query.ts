import type { ParamMap, Params } from '@angular/router';
import {
  catalogSorts,
  type CatalogFilters,
  courseLevels,
  durationBands,
  priceTypes,
} from '../../core/api/discovery-api.types';

export const defaultCatalogFilters: CatalogFilters = {
  category: null,
  tag: null,
  language: null,
  level: null,
  price: null,
  duration: null,
  instructor: null,
  sort: 'newest',
};

export const parseCatalogFilters = (params: ParamMap): CatalogFilters => ({
  category: safeToken(params.get('category')),
  tag: safeToken(params.get('tag')),
  language: oneOf(params.get('language'), ['ar', 'en'] as const),
  level: oneOf(params.get('level'), courseLevels),
  price: oneOf(params.get('price'), priceTypes),
  duration: oneOf(params.get('duration'), durationBands),
  instructor: safeToken(params.get('instructor')),
  sort: oneOf(params.get('sort'), catalogSorts) ?? defaultCatalogFilters.sort,
});

export const catalogFilterParams = (filters: CatalogFilters): Params => ({
  category: filters.category,
  tag: filters.tag,
  language: filters.language,
  level: filters.level,
  price: filters.price,
  duration: filters.duration,
  instructor: filters.instructor,
  sort: filters.sort === defaultCatalogFilters.sort ? null : filters.sort,
});

export const sameCatalogFilters = (left: CatalogFilters, right: CatalogFilters): boolean =>
  left.category === right.category &&
  left.tag === right.tag &&
  left.language === right.language &&
  left.level === right.level &&
  left.price === right.price &&
  left.duration === right.duration &&
  left.instructor === right.instructor &&
  left.sort === right.sort;

const safeToken = (value: string | null): string | null => {
  const normalized = value?.trim() ?? '';
  return normalized.length > 0 && normalized.length <= 100 && !/[\u0000-\u001f\u007f]/u.test(normalized)
    ? normalized
    : null;
};

const oneOf = <T extends string>(value: string | null, values: readonly T[]): T | null =>
  values.find((candidate) => candidate === value) ?? null;
