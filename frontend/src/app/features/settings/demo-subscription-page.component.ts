import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommerceApiClient } from '../../core/api/commerce-api.client';
import type { DemoSubscription } from '../../core/api/commerce-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage } from '../auth/auth-form.helpers';

@Component({
  selector: 'drs-demo-subscription-page',
  template: `
    <section class="subscription-page" aria-labelledby="subscription-title">
      <header>
        <p class="kicker">PORTFOLIO PLAN / DEMO</p>
        <h1 id="subscription-title">
          {{ locale.locale() === 'ar' ? 'اشتراك تجريبي بسيط' : 'Simple demo subscription' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'هذا الاشتراك محلي للعرض فقط. لا يتجدد، ولا يطلب بطاقة، ولا يخصم أي مبلغ.'
              : 'This is a local showcase subscription. It never renews, asks for a card, or charges money.'
          }}
        </p>
      </header>

      @if (error()) {
        <div class="alert" role="alert">{{ error() }}</div>
      }
      @if (loading()) {
        <p role="status">{{ locale.locale() === 'ar' ? 'جارٍ التحميل…' : 'Loading…' }}</p>
      } @else {
        <article>
          <div>
            <span class="status" [class.active]="subscription()?.status === 'Active'">
              {{ statusLabel() }}
            </span>
            <h2>Dorosak Portfolio</h2>
            <strong>0 DEMO</strong>
          </div>
          <ul>
            <li>{{ locale.locale() === 'ar' ? 'خطة محلية واحدة' : 'One local plan' }}</li>
            <li>{{ locale.locale() === 'ar' ? 'لا فوترة ولا تجديد' : 'No billing or renewal' }}</li>
            <li>
              {{ locale.locale() === 'ar' ? 'إلغاء وتفعيل فوري' : 'Instant activate and cancel' }}
            </li>
          </ul>
          @if (subscription()?.status === 'Active') {
            <button type="button" [disabled]="saving()" (click)="cancel()">
              {{
                locale.locale() === 'ar' ? 'إلغاء الاشتراك التجريبي' : 'Cancel demo subscription'
              }}
            </button>
          } @else {
            <button class="primary" type="button" [disabled]="saving()" (click)="activate()">
              {{
                locale.locale() === 'ar' ? 'تفعيل الاشتراك التجريبي' : 'Activate demo subscription'
              }}
            </button>
          }
        </article>
      }
    </section>
  `,
  styles: `
    .subscription-page {
      max-inline-size: 58rem;
      margin-inline: auto;
      padding-block: var(--space-6);
    }
    header {
      max-inline-size: 48rem;
      margin-block-end: var(--space-7);
    }
    .kicker {
      color: var(--color-brand);
      font: 750 0.75rem/1.2 monospace;
      letter-spacing: 0;
    }
    h1 {
      margin-block: var(--space-3);
      font-size: clamp(2.25rem, 6vw, 3.75rem);
      line-height: 0.95;
    }
    header > p:last-child,
    li {
      color: var(--color-muted);
    }
    article {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(14rem, 1fr);
      gap: var(--space-6);
      padding: var(--space-7);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      box-shadow: 0.7rem 0.7rem 0 var(--color-subtle);
    }
    article h2 {
      margin-block: var(--space-4) var(--space-2);
      font-size: 2rem;
    }
    article strong {
      font-size: 1.5rem;
    }
    .status {
      display: inline-block;
      padding: 0.35rem 0.65rem;
      border: 1px solid var(--color-border);
      font-size: 0.8rem;
      font-weight: 750;
      text-transform: uppercase;
    }
    .status.active {
      color: var(--color-success);
      border-color: currentColor;
    }
    ul {
      display: grid;
      align-content: center;
      gap: var(--space-3);
      margin: 0;
    }
    button {
      grid-column: 1 / -1;
      min-block-size: 48px;
      padding-inline: var(--space-5);
      justify-self: start;
      color: var(--color-text);
      background: transparent;
      border: 1px solid var(--color-border);
    }
    button.primary {
      color: var(--color-on-brand);
      background: var(--color-brand);
      border-color: var(--color-brand);
    }
    .alert {
      margin-block-end: var(--space-4);
      padding: var(--space-4);
      border-inline-start: 4px solid var(--color-danger);
      background: var(--color-surface);
    }
    @media (max-width: 680px) {
      article {
        grid-template-columns: 1fr;
        padding: var(--space-5);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DemoSubscriptionPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly commerce = inject(CommerceApiClient);
  protected readonly subscription = signal<DemoSubscription | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected statusLabel(): string {
    const status = this.subscription()?.status;
    if (status === 'Active') return this.locale.locale() === 'ar' ? 'مفعّل' : 'Active';
    if (status === 'Cancelled') return this.locale.locale() === 'ar' ? 'ملغي' : 'Cancelled';
    return this.locale.locale() === 'ar' ? 'غير مفعّل' : 'Inactive';
  }

  protected activate(): void {
    if (this.saving()) return;
    this.mutate(this.commerce.activateDemoSubscription());
  }

  protected cancel(): void {
    const subscription = this.subscription();
    if (!subscription || this.saving()) return;
    this.mutate(this.commerce.cancelDemoSubscription(subscription.id));
  }

  private load(): void {
    this.loading.set(true);
    this.commerce.getDemoSubscription().subscribe({
      next: (state) => {
        this.subscription.set(state.subscription);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(authErrorMessage(error, this.locale.locale()));
        this.loading.set(false);
      },
    });
  }

  private mutate(request: ReturnType<CommerceApiClient['activateDemoSubscription']>): void {
    this.error.set(null);
    this.saving.set(true);
    request.subscribe({
      next: (subscription) => {
        this.subscription.set(subscription);
        this.saving.set(false);
      },
      error: (error: unknown) => {
        this.error.set(authErrorMessage(error, this.locale.locale()));
        this.saving.set(false);
      },
    });
  }
}
