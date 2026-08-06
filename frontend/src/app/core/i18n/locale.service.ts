import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { Router } from '@angular/router';
import { applicationCopy } from './translations';
import type { Locale } from './locale';

@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly router = inject(Router);
  private readonly activeLocale = signal<Locale>('ar');

  readonly locale = this.activeLocale.asReadonly();
  readonly direction = computed(() => (this.activeLocale() === 'ar' ? 'rtl' : 'ltr'));
  readonly copy = computed(() => applicationCopy[this.activeLocale()]);

  setLocale(locale: Locale): void {
    this.activeLocale.set(locale);
    this.document.documentElement.lang = locale;
    this.document.documentElement.dir = this.direction();

    if (isPlatformBrowser(this.platformId)) {
      this.document.cookie = `drs-locale=${locale}; Path=/; Max-Age=31536000; SameSite=Lax`;
    }
  }

  async switchLocale(): Promise<boolean> {
    const target: Locale = this.activeLocale() === 'ar' ? 'en' : 'ar';
    const tree = this.router.parseUrl(this.router.url);
    const primary = tree.root.children['primary'];
    if (primary?.segments[0]) {
      primary.segments[0].path = target;
    } else {
      return this.router.navigate([target]);
    }

    return this.router.navigateByUrl(tree);
  }
}
