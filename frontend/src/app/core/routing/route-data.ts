import type { Data } from '@angular/router';

export interface DorosakRouteData extends Data {
  titleAr: string;
  titleEn: string;
  indexing: 'index' | 'noindex';
  breadcrumb?: string;
  permission?: string;
  renderMode: 'server' | 'client';
  preload?: boolean;
}
