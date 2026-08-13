import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, throwError } from 'rxjs';
import type { PublicPortfolioSettings } from '../../core/api/cms-api.types';
import { CmsApiClient } from '../../core/api/cms-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { PublicPortfolioSettingsStore } from './public-portfolio-settings.store';

describe('PublicPortfolioSettingsStore', () => {
  it('uses configured limits, hides blank notices, and falls back safely', () => {
    const locale = signalLocale('en');
    const response = new Subject<PublicPortfolioSettings>();
    const api = { getPublicSettings: vi.fn(() => response.asObservable()) };
    TestBed.configureTestingModule({
      providers: [
        PublicPortfolioSettingsStore,
        { provide: CmsApiClient, useValue: api },
        { provide: LocaleService, useValue: { locale: locale.asReadonly() } },
      ],
    });

    const store = TestBed.inject(PublicPortfolioSettingsStore);
    expect(store.state().status).toBe('loading');
    TestBed.tick();
    response.next({
      locale: 'en',
      featuredCourseLimit: 99,
      showPortfolioNotice: true,
      portfolioNotice: '   ',
    });
    expect(store.state().settings.featuredCourseLimit).toBe(12);
    expect(store.notice()).toBeNull();

    api.getPublicSettings.mockReturnValueOnce(throwError(() => new Error('offline')));
    locale.set('ar');
    TestBed.tick();
    expect(store.state().status).toBe('fallback');
    expect(store.state().settings.featuredCourseLimit).toBe(3);
    expect(store.notice()).toBeNull();
  });
});

const signalLocale = (initial: 'ar' | 'en') => {
  const value = signal(initial);
  return {
    asReadonly: () => value.asReadonly(),
    set: (locale: 'ar' | 'en') => {
      value.set(locale);
    },
  };
};
