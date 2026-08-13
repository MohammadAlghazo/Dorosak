import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { CmsApiClient } from '../../core/api/cms-api.client';
import type { PublicCmsPage } from '../../core/api/cms-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-cms-page',
  imports: [RouterLink],
  template: `
    <article class="editorial-page" aria-labelledby="cms-title">
      @if (loading()) {
        <p class="editorial-state" role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الصفحة…' : 'Loading page…' }}
        </p>
      } @else if (error() === 'notFound') {
        <div class="editorial-state" role="alert">
          <span>404</span>
          <h1 id="cms-title">
            {{
              locale.locale() === 'ar' ? 'الصفحة غير منشورة بعد' : 'This page is not published yet'
            }}
          </h1>
          <a [routerLink]="['/', locale.locale()]">{{
            locale.locale() === 'ar' ? 'العودة للبداية' : 'Return home'
          }}</a>
        </div>
      } @else if (error()) {
        <div class="editorial-state" role="alert">
          <span>!</span>
          <h1 id="cms-title">
            {{ locale.locale() === 'ar' ? 'تعذر تحميل الصفحة' : 'The page could not be loaded' }}
          </h1>
          <button type="button" (click)="retry()">
            {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
          </button>
        </div>
      } @else if (page(); as item) {
        <header>
          <p>EDITORIAL / {{ item.slug.toUpperCase() }}</p>
          <h1 id="cms-title">{{ item.title }}</h1>
          <span>{{ locale.locale() === 'ar' ? 'نسخة' : 'Revision' }} {{ item.version }}</span>
        </header>
        <div class="editorial-body" dir="auto">{{ item.body }}</div>
        <footer>
          <span>{{ locale.locale() === 'ar' ? 'آخر نشر' : 'Published' }}</span>
          <time [attr.datetime]="item.publishedAt">{{ formatDate(item.publishedAt) }}</time>
        </footer>
      }
    </article>
  `,
  styles: `
    .editorial-page {
      max-inline-size: 72rem;
      min-block-size: 65dvh;
      margin-inline: auto;
      padding: clamp(var(--space-6), 8vw, var(--space-10)) var(--page-gutter);
    }
    header {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: var(--space-3);
      padding-block-end: var(--space-6);
      border-block-end: 1px solid var(--color-border);
    }
    header p {
      grid-column: 1 / -1;
      margin: 0;
      color: var(--color-brand);
      font: 750 0.75rem/1 monospace;
      letter-spacing: 0.14em;
    }
    h1 {
      max-inline-size: 17ch;
      margin: 0;
      font-size: clamp(2.8rem, 8vw, 6rem);
      line-height: 0.92;
      letter-spacing: -0.045em;
    }
    header span,
    footer {
      color: var(--color-muted);
      font-size: 0.85rem;
    }
    .editorial-body {
      max-inline-size: 68ch;
      margin-block: var(--space-7);
      font-size: clamp(1.05rem, 2vw, 1.3rem);
      line-height: 1.9;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    footer {
      display: flex;
      gap: var(--space-3);
      padding-block-start: var(--space-4);
      border-block-start: 1px solid var(--color-border);
    }
    .editorial-state {
      display: grid;
      place-content: center;
      gap: var(--space-3);
      min-block-size: 45dvh;
      text-align: center;
    }
    .editorial-state span {
      color: var(--color-brand);
      font: 700 4rem/1 monospace;
    }
    .editorial-state button {
      min-block-size: 44px;
      padding-inline: var(--space-4);
      color: var(--color-on-brand);
      background: var(--color-brand);
      border: 0;
      border-radius: var(--radius-2);
    }
    @media (max-width: 520px) {
      header {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CmsPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(CmsApiClient);
  protected readonly page = signal<PublicCmsPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<'notFound' | 'offline' | 'error' | null>(null);
  private readonly retryVersion = signal(0);

  constructor() {
    effect((onCleanup) => {
      this.locale.locale();
      this.retryVersion();
      const data = this.route.snapshot.data as Record<string, unknown>;
      const slug: unknown = data['cmsSlug'];
      if (typeof slug !== 'string') {
        this.error.set('notFound');
        this.loading.set(false);
        return;
      }
      this.loading.set(true);
      this.error.set(null);
      const subscription = this.api.getPublicPage(slug).subscribe({
        next: (page) => {
          this.page.set(page);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.page.set(null);
          this.error.set(
            error instanceof ApiProblem && error.status === 404
              ? 'notFound'
              : error instanceof ApiProblem && error.status === 0
                ? 'offline'
                : 'error',
          );
          this.loading.set(false);
        },
      });
      onCleanup(() => {
        subscription.unsubscribe();
      });
    });
  }

  protected retry(): void {
    this.retryVersion.update((value) => value + 1);
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'long' }).format(
      new Date(value),
    );
  }
}
