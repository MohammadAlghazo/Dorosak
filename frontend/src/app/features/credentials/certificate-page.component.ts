import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CredentialsApiClient } from '../../core/api/credentials-api.client';
import type { Certificate, PublicCertificate } from '../../core/api/credentials-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-certificate-page',
  imports: [RouterLink],
  template: `
    <section class="certificate-page" aria-live="polite">
      @if (loading()) {
        <p role="status">{{ locale.locale() === 'ar' ? 'جارٍ التحقق…' : 'Verifying…' }}</p>
      } @else if (error()) {
        <div class="error" role="alert">
          <strong>NOT VERIFIED</strong>
          <h1>{{ locale.locale() === 'ar' ? 'لم نجد هذه الشهادة' : 'Certificate not found' }}</h1>
        </div>
      } @else if (certificate(); as item) {
        <div class="actions">
          @if (!publicView()) {
            <a [routerLink]="['/', locale.locale(), 'certificates']">{{
              locale.locale() === 'ar' ? 'كل الشهادات' : 'All certificates'
            }}</a>
          }
          <button type="button" (click)="print()">
            {{ locale.locale() === 'ar' ? 'طباعة / حفظ PDF' : 'Print / save PDF' }}
          </button>
        </div>
        <article [class.revoked]="item.status === 'Revoked'" aria-labelledby="certificate-title">
          <div class="seal">D</div>
          <p class="eyebrow">DOROSAK · PORTFOLIO DEMO</p>
          <p>{{ locale.locale() === 'ar' ? 'تشهد منصة دروسك بأن' : 'Dorosak certifies that' }}</p>
          <h1 id="certificate-title">{{ item.learnerName }}</h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'قد أكمل المسار التعليمي'
                : 'has completed the learning pathway'
            }}
          </p>
          <h2>{{ item.courseTitle }}</h2>
          <dl>
            <div>
              <dt>{{ locale.locale() === 'ar' ? 'تاريخ الإكمال' : 'Completed' }}</dt>
              <dd>{{ formatDate(item.completedAt) }}</dd>
            </div>
            <div>
              <dt>{{ locale.locale() === 'ar' ? 'تاريخ الإصدار' : 'Issued' }}</dt>
              <dd>{{ formatDate(item.issuedAt) }}</dd>
            </div>
            <div>
              <dt>{{ locale.locale() === 'ar' ? 'الحالة' : 'Status' }}</dt>
              <dd>{{ item.status }}</dd>
            </div>
          </dl>
          <footer>
            <span>{{ locale.locale() === 'ar' ? 'رمز التحقق' : 'Verification code' }}</span>
            <code>{{ item.verificationCode }}</code>
          </footer>
          @if (item.status === 'Revoked') {
            <div class="revoked-mark">REVOKED</div>
          }
        </article>
      }
    </section>
  `,
  styles: `
    .certificate-page {
      max-inline-size: 72rem;
      margin-inline: auto;
      padding-block: var(--space-5);
    }
    .actions {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
      margin-block-end: var(--space-4);
    }
    .actions a,
    .actions button {
      min-block-size: 44px;
      display: inline-flex;
      align-items: center;
      padding-inline: var(--space-4);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      text-decoration: none;
    }
    article {
      position: relative;
      isolation: isolate;
      display: grid;
      justify-items: center;
      min-block-size: 43rem;
      align-content: center;
      gap: var(--space-4);
      padding: clamp(2rem, 8vw, 6rem);
      overflow: hidden;
      text-align: center;
      border: 0.65rem double var(--color-ink);
      background: var(--color-surface);
    }
    article::before,
    article::after {
      content: '';
      position: absolute;
      z-index: -1;
      inline-size: 17rem;
      block-size: 17rem;
      border: 1px solid var(--color-border);
      transform: rotate(45deg);
    }
    article::before {
      inset-block-start: -9rem;
      inset-inline-start: -9rem;
    }
    article::after {
      inset-block-end: -9rem;
      inset-inline-end: -9rem;
    }
    .seal {
      display: grid;
      place-items: center;
      inline-size: 5rem;
      block-size: 5rem;
      color: var(--color-on-brand);
      background: var(--color-brand);
      border-radius: 50%;
      font: 800 2rem/1 serif;
    }
    .eyebrow {
      color: var(--color-brand);
      font: 750 0.75rem/1 monospace;
      letter-spacing: 0;
    }
    article h1 {
      margin: 0;
      font-size: clamp(3rem, 9vw, 7rem);
      line-height: 0.95;
    }
    article h2 {
      max-inline-size: 24ch;
      margin: 0;
      font-size: clamp(1.6rem, 4vw, 3rem);
    }
    article p {
      margin: 0;
      color: var(--color-muted);
    }
    dl {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--space-6);
      margin-block: var(--space-4);
    }
    dt {
      color: var(--color-muted);
      font-size: 0.8rem;
    }
    dd {
      margin: var(--space-1) 0 0;
      font-weight: 700;
    }
    footer {
      display: grid;
      gap: var(--space-2);
      max-inline-size: 100%;
      color: var(--color-muted);
    }
    code {
      overflow-wrap: anywhere;
      color: var(--color-text);
    }
    .revoked-mark {
      position: absolute;
      color: var(--color-danger);
      border: 0.35rem solid currentColor;
      padding: var(--space-3) var(--space-5);
      font: 900 clamp(2rem, 7vw, 5rem)/1 monospace;
      transform: rotate(-13deg);
      opacity: 0.8;
    }
    .error {
      min-block-size: 25rem;
      display: grid;
      place-content: center;
      text-align: center;
    }
    @media print {
      :host {
        position: fixed;
        inset: 0;
        z-index: 10000;
        overflow: visible;
        background: #fff;
      }
      .actions {
        display: none;
      }
      .certificate-page {
        padding: 0;
      }
      article {
        min-block-size: 95vh;
        color: #111;
        background: #fff;
        border-color: #111;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificatePageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(CredentialsApiClient);
  protected readonly certificate = signal<Certificate | PublicCertificate | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly publicView = signal(false);

  constructor() {
    const verificationCode = this.route.snapshot.paramMap.get('verificationCode');
    const certificateId = this.route.snapshot.paramMap.get('certificateId');
    this.publicView.set(verificationCode !== null);
    const request = verificationCode
      ? this.api.verifyCertificate(verificationCode)
      : this.api.getMyCertificate(certificateId ?? '');
    request.subscribe({
      next: (certificate) => {
        this.certificate.set(certificate);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'long' }).format(
      new Date(value),
    );
  }

  protected print(): void {
    globalThis.print();
  }
}
