import { convertToParamMap } from '@angular/router';
import { catalogFilterParams, parseCatalogFilters } from './catalog-query';

describe('catalog URL filters', () => {
  it('parses all supported filters and the sort allow-list', () => {
    expect(
      parseCatalogFilters(
        convertToParamMap({
          category: 'technology',
          tag: 'angular',
          language: 'en',
          level: 'advanced',
          price: 'paid',
          duration: 'long',
          instructor: 'teacher-1',
          sort: 'popular',
        }),
      ),
    ).toEqual({
      category: 'technology',
      tag: 'angular',
      language: 'en',
      level: 'advanced',
      price: 'paid',
      duration: 'long',
      instructor: 'teacher-1',
      sort: 'popular',
    });
  });

  it('ignores unsupported values and serializes the default sort without URL noise', () => {
    const parsed = parseCatalogFilters(
      convertToParamMap({ language: 'fr', level: 'expert', price: 'trial', sort: '-drop-table' }),
    );
    expect(parsed).toMatchObject({
      language: null,
      level: null,
      price: null,
      sort: 'newest',
    });
    expect(catalogFilterParams(parsed)['sort']).toBeNull();
  });
});
