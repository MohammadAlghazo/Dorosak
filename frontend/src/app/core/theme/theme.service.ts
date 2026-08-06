import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { DestroyRef, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';
export type EffectiveTheme = Exclude<ThemePreference, 'system'>;

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly media = isPlatformBrowser(this.platformId)
    ? matchMedia('(prefers-color-scheme: dark)')
    : undefined;
  private readonly selectedPreference = signal<ThemePreference>('system');
  private readonly activeTheme = signal<EffectiveTheme>(
    this.document.documentElement.dataset['bsTheme'] === 'dark' ? 'dark' : 'light',
  );

  readonly preference = this.selectedPreference.asReadonly();
  readonly effectiveTheme = this.activeTheme.asReadonly();

  constructor() {
    if (this.media) {
      const listener = () => {
        if (this.selectedPreference() === 'system') this.apply('system');
      };
      this.media.addEventListener('change', listener);
      this.destroyRef.onDestroy(() => this.media?.removeEventListener('change', listener));
    }
  }

  cycle(): void {
    const preference = this.selectedPreference();
    this.setPreference(
      preference === 'system' ? 'light' : preference === 'light' ? 'dark' : 'system',
    );
  }

  setPreference(preference: ThemePreference): void {
    this.selectedPreference.set(preference);
    this.apply(preference);
    if (isPlatformBrowser(this.platformId)) {
      this.document.cookie = `drs-theme=${preference}; Path=/; Max-Age=31536000; SameSite=Lax`;
    }
  }

  private apply(preference: ThemePreference): void {
    const effective =
      preference === 'system' ? (this.media?.matches ? 'dark' : 'light') : preference;
    this.activeTheme.set(effective);
    this.document.documentElement.dataset['bsTheme'] = effective;
  }
}
