import { DOCUMENT } from '@angular/common';
import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import type { DorosakRouteData } from '../routing/route-data';
import { LocaleService } from './locale.service';

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly locale = inject(LocaleService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.update();
      });
  }

  private update(): void {
    let route = this.activatedRoute.snapshot;
    while (route.firstChild) route = route.firstChild;
    const data = route.data as DorosakRouteData;
    const localizedTitle = this.locale.locale() === 'ar' ? data.titleAr : data.titleEn;
    this.title.setTitle(`${localizedTitle} | ${this.locale.copy().brand}`);
    this.meta.updateTag({
      name: 'robots',
      content: data.indexing === 'index' ? 'index,follow' : 'noindex,follow',
    });

    const currentUrl = this.router.url.split('?')[0] ?? `/${this.locale.locale()}`;
    const origin = this.document.location.origin;
    this.setLink('canonical', undefined, `${origin}${currentUrl}`);
    this.setLink('alternate', 'ar', `${origin}${swapLocale(currentUrl, 'ar')}`);
    this.setLink('alternate', 'en', `${origin}${swapLocale(currentUrl, 'en')}`);
    this.setLink('alternate', 'x-default', `${origin}${swapLocale(currentUrl, 'ar')}`);
  }

  private setLink(rel: string, language: string | undefined, href: string): void {
    const selector = language
      ? `link[rel="${rel}"][hreflang="${language}"]`
      : `link[rel="${rel}"]:not([hreflang])`;
    let link = this.document.head.querySelector<HTMLLinkElement>(selector);
    if (!link) {
      link = this.document.createElement('link');
      link.rel = rel;
      if (language) link.hreflang = language;
      this.document.head.append(link);
    }
    link.href = href;
  }
}

const swapLocale = (path: string, locale: 'ar' | 'en'): string =>
  path.replace(/^\/(?:ar|en)(?=\/|$)/u, `/${locale}`);
