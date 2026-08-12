import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AnalyticsApiClient } from '../../core/api/analytics-api.client';
import type { AdminAnalyticsOverview } from '../../core/api/analytics-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-analytics-page',
  template: `
    <section class="operations-overview" aria-labelledby="analytics-title">
      <header class="operations-heading">
        <div>
          <p>LOCAL SIGNAL / 01</p>
          <h1 id="analytics-title">
            {{ locale.locale() === 'ar' ? 'نبض المنصة' : 'Platform pulse' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'لقطة تشغيلية مجمعة من بيانات الديمو، بلا أسماء أو رسائل أو بيانات دفع.'
                : 'An aggregate operational snapshot of demo data, without names, messages, or payment data.'
            }}
          </p>
        </div>
        <button type="button" [disabled]="loading()" (click)="load()">
          {{ locale.locale() === 'ar' ? 'تحديث اللقطة' : 'Refresh snapshot' }}
        </button>
      </header>

      @if (loading() && overview() === null) {
        <div class="overview-state" role="status">
          <span aria-hidden="true">···</span>
          {{ locale.locale() === 'ar' ? 'جارٍ جمع المؤشرات…' : 'Collecting indicators…' }}
        </div>
      } @else if (error() && overview() === null) {
        <div class="overview-state overview-error" role="alert">
          <strong>
            {{
              locale.locale() === 'ar' ? 'تعذر تحميل المؤشرات' : 'Indicators could not be loaded'
            }}
          </strong>
          <span>
            {{
              locale.locale() === 'ar'
                ? 'لم تُعرض بيانات قديمة. تحقق من الخدمة ثم حاول مجددًا.'
                : 'No stale data is shown. Check the service and try again.'
            }}
          </span>
        </div>
      } @else if (overview(); as data) {
        <div class="snapshot-bar" [attr.aria-busy]="loading()">
          <span>{{ locale.locale() === 'ar' ? 'وقت اللقطة' : 'Snapshot time' }}</span>
          <time [attr.datetime]="data.generatedAt">{{ formatDateTime(data.generatedAt) }}</time>
          @if (loading()) {
            <small role="status">{{
              locale.locale() === 'ar' ? 'جارٍ التحديث…' : 'Refreshing…'
            }}</small>
          }
        </div>

        <div class="signal-grid">
          <section class="signal-group signal-primary" aria-labelledby="audience-title">
            <header>
              <span>01</span>
              <h2 id="audience-title">{{ locale.locale() === 'ar' ? 'الوصول' : 'Reach' }}</h2>
            </header>
            <dl>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الحسابات' : 'Accounts' }}</dt>
                <dd>{{ formatNumber(data.totalUsers) }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الحسابات النشطة' : 'Active accounts' }}</dt>
                <dd>{{ formatNumber(data.activeUsers) }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'المقررات المنشورة' : 'Published courses' }}</dt>
                <dd>
                  {{ formatNumber(data.publishedCourses)
                  }}<small>/ {{ formatNumber(data.totalCourses) }}</small>
                </dd>
              </div>
            </dl>
          </section>

          <section class="signal-group" aria-labelledby="learning-title">
            <header>
              <span>02</span>
              <h2 id="learning-title">{{ locale.locale() === 'ar' ? 'التعلّم' : 'Learning' }}</h2>
            </header>
            <dl>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الالتحاقات' : 'Enrollments' }}</dt>
                <dd>{{ formatNumber(data.totalEnrollments) }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الإكمالات' : 'Completions' }}</dt>
                <dd>{{ formatNumber(data.completedEnrollments) }}</dd>
              </div>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'الشهادات النشطة' : 'Active certificates' }}</dt>
                <dd>
                  {{ formatNumber(data.activeCertificates)
                  }}<small>/ {{ formatNumber(data.issuedCertificates) }}</small>
                </dd>
              </div>
            </dl>
          </section>

          <section class="signal-group" aria-labelledby="demo-title">
            <header>
              <span>03</span>
              <h2 id="demo-title">
                {{ locale.locale() === 'ar' ? 'الديمو التجاري' : 'Demo commerce' }}
              </h2>
            </header>
            <dl>
              <div>
                <dt>
                  {{ locale.locale() === 'ar' ? 'طلبات DEMO المكتملة' : 'Completed DEMO orders' }}
                </dt>
                <dd>{{ formatNumber(data.completedDemoOrders) }}</dd>
              </div>
              <div>
                <dt>
                  {{
                    locale.locale() === 'ar'
                      ? 'اشتراكات الديمو النشطة'
                      : 'Active demo subscriptions'
                  }}
                </dt>
                <dd>{{ formatNumber(data.activeDemoSubscriptions) }}</dd>
              </div>
            </dl>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'لا تمثل هذه الأرقام أموالًا حقيقية.'
                  : 'These figures do not represent real money.'
              }}
            </p>
          </section>

          <section class="signal-group signal-queue" aria-labelledby="queue-title">
            <header>
              <span>04</span>
              <h2 id="queue-title">
                {{ locale.locale() === 'ar' ? 'طوابير العمل' : 'Work queues' }}
              </h2>
            </header>
            <dl>
              <div>
                <dt>{{ locale.locale() === 'ar' ? 'مراجعات النشر' : 'Publication reviews' }}</dt>
                <dd>{{ formatNumber(data.pendingPublicationReviews) }}</dd>
              </div>
              <div>
                <dt>
                  {{
                    locale.locale() === 'ar' ? 'قضايا الإشراف المفتوحة' : 'Open moderation cases'
                  }}
                </dt>
                <dd>{{ formatNumber(data.openModerationCases) }}</dd>
              </div>
              <div>
                <dt>
                  {{
                    locale.locale() === 'ar' ? 'رسائل outbox المعلقة' : 'Pending outbox messages'
                  }}
                </dt>
                <dd>{{ formatNumber(data.pendingOutboxMessages) }}</dd>
              </div>
              <div>
                <dt>
                  {{ locale.locale() === 'ar' ? 'رسائل قيد إعادة المحاولة' : 'Retrying messages' }}
                </dt>
                <dd [class.attention]="data.retryingOutboxMessages > 0">
                  {{ formatNumber(data.retryingOutboxMessages) }}
                </dd>
              </div>
            </dl>
          </section>
        </div>
      }
    </section>
  `,
  styles: `
    .operations-overview {
      max-inline-size: 86rem;
      margin-inline: auto;
    }
    .operations-heading {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-6);
      padding-block-end: var(--space-6);
      border-block-end: 1px solid var(--color-border);
    }
    .operations-heading > div {
      max-inline-size: 48rem;
    }
    .operations-heading p:first-child {
      margin: 0;
      color: var(--color-brand);
      font: 750 0.75rem/1 monospace;
      letter-spacing: 0.14em;
    }
    h1 {
      margin: var(--space-3) 0;
      font-size: clamp(2.5rem, 7vw, 5.5rem);
      line-height: 0.9;
      letter-spacing: -0.05em;
    }
    .operations-heading p:last-child {
      margin: 0;
      color: var(--color-muted);
    }
    button {
      min-block-size: 46px;
      flex: none;
      padding-inline: var(--space-4);
      color: var(--color-on-brand);
      background: var(--color-brand);
      border: 0;
    }
    button:disabled {
      cursor: wait;
      opacity: 0.65;
    }
    .snapshot-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--space-2) var(--space-4);
      padding-block: var(--space-4);
      color: var(--color-muted);
      font-size: 0.9rem;
    }
    .snapshot-bar time {
      color: var(--color-text);
      font-variant-numeric: tabular-nums;
    }
    .snapshot-bar small {
      margin-inline-start: auto;
    }
    .signal-grid {
      display: grid;
      grid-template-columns: repeat(12, minmax(0, 1fr));
      border-block-start: 1px solid var(--color-border);
      border-inline-start: 1px solid var(--color-border);
    }
    .signal-group {
      grid-column: span 6;
      min-inline-size: 0;
      padding: clamp(var(--space-4), 4vw, var(--space-6));
      background: var(--color-surface);
      border-inline-end: 1px solid var(--color-border);
      border-block-end: 1px solid var(--color-border);
    }
    .signal-primary {
      grid-column: span 7;
    }
    .signal-queue {
      grid-column: span 5;
    }
    .signal-group > header {
      display: flex;
      align-items: baseline;
      gap: var(--space-3);
      margin-block-end: var(--space-5);
    }
    .signal-group > header span {
      color: var(--color-brand);
      font: 700 0.75rem/1 monospace;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }
    dl {
      display: grid;
      gap: var(--space-4);
      margin: 0;
    }
    dl > div {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-4);
      border-block-end: 1px solid var(--color-subtle);
    }
    dt {
      padding-block-end: var(--space-2);
      color: var(--color-muted);
    }
    dd {
      margin: 0;
      font: 650 clamp(2rem, 5vw, 3.8rem)/0.9 monospace;
      letter-spacing: -0.08em;
      font-variant-numeric: tabular-nums;
    }
    dd small {
      margin-inline-start: var(--space-2);
      color: var(--color-muted);
      font-size: 0.85rem;
      letter-spacing: 0;
    }
    dd.attention {
      color: var(--color-danger);
    }
    .signal-group > p {
      margin: var(--space-5) 0 0;
      color: var(--color-muted);
      font-size: 0.85rem;
    }
    .overview-state {
      display: grid;
      gap: var(--space-2);
      min-block-size: 20rem;
      place-content: center;
      margin-block-start: var(--space-5);
      padding: var(--space-6);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      text-align: center;
    }
    .overview-state > span:first-child {
      color: var(--color-brand);
      font: 700 3rem/1 monospace;
    }
    .overview-error {
      border-inline-start: 5px solid var(--color-danger);
    }
    @media (max-width: 760px) {
      .operations-heading {
        align-items: start;
        flex-direction: column;
      }
      .signal-group,
      .signal-primary,
      .signal-queue {
        grid-column: 1 / -1;
      }
      .snapshot-bar small {
        inline-size: 100%;
        margin-inline-start: 0;
      }
    }
    @media (max-width: 420px) {
      dl > div {
        display: grid;
        gap: var(--space-2);
      }
      dd {
        padding-block-end: var(--space-3);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(AnalyticsApiClient);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly overview = signal<AdminAnalyticsOverview | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    if (this.loading() && this.overview() !== null) return;
    this.loading.set(true);
    this.error.set(false);
    this.api
      .getAdminOverview()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (overview) => {
          this.overview.set(overview);
          this.loading.set(false);
        },
        error: () => {
          this.overview.set(null);
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  protected formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale.locale()).format(value);
  }

  protected formatDateTime(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }
}
