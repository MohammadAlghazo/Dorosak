import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { ReplaySubject } from 'rxjs';
import { CmsApiClient } from '../../core/api/cms-api.client';
import type { PublicPortfolioSettings } from '../../core/api/cms-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

type PublicSettingsStatus = 'loading' | 'success' | 'fallback';

interface PublicSettingsState {
  status: PublicSettingsStatus;
  settings: PublicPortfolioSettings;
}

@Injectable()
export class PublicPortfolioSettingsStore {
  private readonly api = inject(CmsApiClient);
  private readonly locale = inject(LocaleService);
  private readonly resolvedSettings = new ReplaySubject<PublicPortfolioSettings>(1);
  private readonly settingsState = signal<PublicSettingsState>({
    status: 'loading',
    settings: fallbackSettings('ar'),
  });

  readonly state = this.settingsState.asReadonly();
  readonly settings$ = this.resolvedSettings.asObservable();
  readonly notice = computed(() => {
    const { settings } = this.settingsState();
    const notice = settings.portfolioNotice.trim();
    return settings.showPortfolioNotice && notice.length > 0 ? notice : null;
  });

  constructor() {
    effect((onCleanup) => {
      const locale = this.locale.locale();
      this.settingsState.set({ status: 'loading', settings: fallbackSettings(locale) });
      const subscription = this.api.getPublicSettings().subscribe({
        next: (settings) => {
          const normalized = normalizeSettings(settings, locale);
          this.settingsState.set({ status: 'success', settings: normalized });
          this.resolvedSettings.next(normalized);
        },
        error: () => {
          const fallback = fallbackSettings(locale);
          this.settingsState.set({ status: 'fallback', settings: fallback });
          this.resolvedSettings.next(fallback);
        },
      });
      onCleanup(() => {
        subscription.unsubscribe();
      });
    });
  }
}

const fallbackSettings = (locale: 'ar' | 'en'): PublicPortfolioSettings => ({
  locale,
  featuredCourseLimit: 3,
  showPortfolioNotice: false,
  portfolioNotice: '',
});

const normalizeSettings = (
  settings: PublicPortfolioSettings,
  locale: 'ar' | 'en',
): PublicPortfolioSettings => ({
  ...settings,
  locale,
  featuredCourseLimit: Math.min(12, Math.max(1, Math.trunc(settings.featuredCourseLimit))),
});
