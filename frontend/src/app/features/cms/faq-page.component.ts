import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { CmsApiClient } from '../../core/api/cms-api.client';
import type { PublicCmsFaq } from '../../core/api/cms-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-faq-page',
  template: `
    <section class="faq-page" aria-labelledby="faq-title">
      <header>
        <p>FIELD NOTES / FAQ</p>
        <h1 id="faq-title">
          {{
            locale.locale() === 'ar'
              ? 'أسئلة واضحة، إجابات مباشرة.'
              : 'Clear questions. Direct answers.'
          }}
        </h1>
      </header>
      @if (loading()) {
        <p role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الأسئلة…' : 'Loading questions…' }}
        </p>
      } @else if (error()) {
        <div class="faq-state" role="alert">
          <p>
            {{
              locale.locale() === 'ar' ? 'تعذر تحميل الأسئلة.' : 'Questions could not be loaded.'
            }}
          </p>
          <button type="button" (click)="retry()">
            {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
          </button>
        </div>
      } @else if (faqs().length === 0) {
        <div class="faq-state">
          {{
            locale.locale() === 'ar'
              ? 'لا توجد أسئلة منشورة بعد.'
              : 'No questions have been published yet.'
          }}
        </div>
      } @else {
        <div class="faq-list">
          @for (faq of faqs(); track faq.id; let index = $index) {
            <details [open]="index === 0">
              <summary>
                <span>{{ number(index + 1) }}</span
                >{{ faq.question }}
              </summary>
              <p dir="auto">{{ faq.answer }}</p>
            </details>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .faq-page {
      max-inline-size: 72rem;
      min-block-size: 65dvh;
      margin-inline: auto;
      padding: clamp(var(--space-6), 8vw, var(--space-10)) var(--page-gutter);
    }
    header {
      max-inline-size: 60rem;
      margin-block-end: var(--space-7);
    }
    header p {
      color: var(--color-brand);
      font: 750 0.75rem/1 monospace;
      letter-spacing: 0.14em;
    }
    h1 {
      margin: var(--space-3) 0 0;
      font-size: clamp(2.6rem, 7vw, 5.4rem);
      line-height: 0.95;
      letter-spacing: -0.04em;
    }
    .faq-list {
      border-block-start: 1px solid var(--color-border);
    }
    details {
      padding-block: var(--space-5);
      border-block-end: 1px solid var(--color-border);
    }
    summary {
      display: flex;
      gap: var(--space-4);
      align-items: baseline;
      cursor: pointer;
      font-size: clamp(1.15rem, 2vw, 1.55rem);
      font-weight: 650;
    }
    summary span {
      flex: none;
      color: var(--color-brand);
      font: 700 0.75rem/1 monospace;
    }
    details p {
      max-inline-size: 68ch;
      margin: var(--space-4) 2.2rem 0;
      color: var(--color-muted);
      line-height: 1.8;
      white-space: pre-wrap;
    }
    .faq-state {
      padding: var(--space-6);
      border: 1px dashed var(--color-border);
    }
    .faq-state button {
      min-block-size: 44px;
      padding-inline: var(--space-4);
      color: var(--color-on-brand);
      background: var(--color-brand);
      border: 0;
      border-radius: var(--radius-2);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FaqPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(CmsApiClient);
  protected readonly faqs = signal<readonly PublicCmsFaq[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  private readonly retryVersion = signal(0);

  constructor() {
    effect((onCleanup) => {
      this.locale.locale();
      this.retryVersion();
      this.loading.set(true);
      this.error.set(false);
      const subscription = this.api.getFaqs().subscribe({
        next: (faqs) => {
          this.faqs.set(faqs);
          this.loading.set(false);
        },
        error: () => {
          this.faqs.set([]);
          this.error.set(true);
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

  protected number(value: number): string {
    return new Intl.NumberFormat(this.locale.locale(), {
      minimumIntegerDigits: 2,
      useGrouping: false,
    }).format(value);
  }
}
