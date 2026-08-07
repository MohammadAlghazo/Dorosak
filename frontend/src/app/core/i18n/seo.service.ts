import { DOCUMENT } from '@angular/common';
import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import type { PublicCourseDetail } from '../api/discovery-api.types';
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

  setCourseMetadata(course: PublicCourseDetail): void {
    this.title.setTitle(`${course.title} | ${this.locale.copy().brand}`);
    this.meta.updateTag({ name: 'description', content: course.description });
    this.meta.updateTag({ name: 'robots', content: 'index,follow' });

    const origin = this.document.location.origin;
    const pathFor = (locale: 'ar' | 'en', slug: string) =>
      `${origin}/${locale}/courses/${encodeURIComponent(slug)}`;
    this.setLink('canonical', undefined, pathFor(course.locale, course.slug));
    for (const locale of ['ar', 'en'] as const) {
      const localization = course.localizations.find((item) => item.locale === locale);
      this.setLink(
        'alternate',
        locale,
        localization ? pathFor(localization.locale, localization.slug) : null,
      );
    }
    const fallback =
      course.localizations.find((item) => item.locale === course.defaultLocale) ??
      course.localizations.find((item) => item.locale === course.locale);
    this.setLink(
      'alternate',
      'x-default',
      fallback ? pathFor(fallback.locale, fallback.slug) : pathFor(course.locale, course.slug),
    );
  }

  setCourseNotFoundMetadata(): void {
    const title = this.locale.locale() === 'ar' ? 'المقرر غير موجود' : 'Course not found';
    this.title.setTitle(`${title} | ${this.locale.copy().brand}`);
    this.meta.updateTag({ name: 'robots', content: 'noindex,follow' });
    this.meta.removeTag('name="description"');
    this.setLink('canonical', undefined, null);
    this.setLink('alternate', 'ar', null);
    this.setLink('alternate', 'en', null);
    this.setLink('alternate', 'x-default', null);
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
    this.meta.removeTag('name="description"');

    const currentUrl = this.router.url.split('?')[0] ?? `/${this.locale.locale()}`;
    const origin = this.document.location.origin;
    this.setLink('canonical', undefined, `${origin}${currentUrl}`);
    this.setLink('alternate', 'ar', `${origin}${swapLocale(currentUrl, 'ar')}`);
    this.setLink('alternate', 'en', `${origin}${swapLocale(currentUrl, 'en')}`);
    this.setLink('alternate', 'x-default', `${origin}${swapLocale(currentUrl, 'ar')}`);
  }

  private setLink(rel: string, language: string | undefined, href: string | null): void {
    const selector = language
      ? `link[rel="${rel}"][hreflang="${language}"]`
      : `link[rel="${rel}"]:not([hreflang])`;
    let link = this.document.head.querySelector<HTMLLinkElement>(selector);
    if (href === null) {
      link?.remove();
      return;
    }
    if (!link) {
      link = this.document.createElement('link');
      link.rel = rel;
      if (language) link.hreflang = language;
      this.document.head.appendChild(link);
    }
    link.href = href;
  }
}

const swapLocale = (path: string, locale: 'ar' | 'en'): string =>
  path.replace(/^\/(?:ar|en)(?=\/|$)/u, `/${locale}`);
