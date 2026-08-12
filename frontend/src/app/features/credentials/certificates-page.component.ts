import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CredentialsApiClient } from '../../core/api/credentials-api.client';
import type { Certificate } from '../../core/api/credentials-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-certificates-page',
  imports: [RouterLink],
  template: `
    <section class="certificates" aria-labelledby="certificates-title">
      <header>
        <p>CREDENTIAL ARCHIVE / DEMO</p>
        <h1 id="certificates-title">
          {{ locale.locale() === 'ar' ? 'شهاداتي' : 'My certificates' }}
        </h1>
      </header>

      @if (loading()) {
        <p role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الشهادات…' : 'Loading certificates…' }}
        </p>
      } @else if (error()) {
        <div class="state" role="alert">
          <h2>
            {{
              locale.locale() === 'ar' ? 'تعذر فتح الشهادات' : 'Certificates could not be opened'
            }}
          </h2>
          <button type="button" (click)="load()">
            {{ locale.locale() === 'ar' ? 'حاول مجددًا' : 'Try again' }}
          </button>
        </div>
      } @else if (certificates().length === 0) {
        <div class="state">
          <span>00</span>
          <h2>
            {{
              locale.locale() === 'ar'
                ? 'أكمل مسارًا لتحصل على شهادتك'
                : 'Complete a pathway to earn a certificate'
            }}
          </h2>
          <a [routerLink]="['/', locale.locale(), 'my-learning']">{{
            locale.locale() === 'ar' ? 'اذهب إلى مساراتي' : 'Open my learning'
          }}</a>
        </div>
      } @else {
        <div class="grid">
          @for (certificate of certificates(); track certificate.id) {
            <article>
              <span [class.revoked]="certificate.status === 'Revoked'">{{
                certificate.status
              }}</span>
              <h2>{{ certificate.courseTitle }}</h2>
              <p>{{ certificate.learnerName }}</p>
              <time [attr.datetime]="certificate.issuedAt">{{
                formatDate(certificate.issuedAt)
              }}</time>
              <a [routerLink]="['/', locale.locale(), 'certificates', certificate.id]">{{
                locale.locale() === 'ar' ? 'عرض الشهادة' : 'View certificate'
              }}</a>
            </article>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .certificates {
      max-inline-size: var(--content-wide);
      margin-inline: auto;
      padding-block: var(--space-6);
    }
    header {
      padding-block-end: var(--space-6);
      margin-block-end: var(--space-6);
      border-block-end: 1px solid var(--color-border);
    }
    header p,
    article > span {
      color: var(--color-brand);
      font: 750 0.75rem/1 monospace;
      letter-spacing: 0;
    }
    h1 {
      margin: var(--space-3) 0 0;
      font-size: clamp(2.5rem, 7vw, 4.5rem);
      line-height: 0.9;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr));
      gap: var(--space-4);
    }
    article {
      display: grid;
      align-content: start;
      gap: var(--space-3);
      min-block-size: 19rem;
      padding: var(--space-6);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    article h2 {
      margin: var(--space-4) 0 0;
      font-size: clamp(1.7rem, 4vw, 2.8rem);
    }
    article p,
    article time {
      color: var(--color-muted);
    }
    article > span.revoked {
      color: var(--color-danger);
    }
    article a,
    .state a,
    .state button {
      min-block-size: 46px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      align-self: end;
      justify-self: start;
      padding-inline: var(--space-4);
      color: var(--color-on-brand);
      background: var(--color-brand);
      border: 0;
      text-decoration: none;
    }
    .state {
      display: grid;
      justify-items: start;
      gap: var(--space-4);
      min-block-size: 22rem;
      align-content: center;
      padding: var(--space-7);
      border: 1px solid var(--color-border);
    }
    .state span {
      color: var(--color-brand);
      font: 700 3rem/1 monospace;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificatesPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(CredentialsApiClient);
  protected readonly certificates = signal<readonly Certificate[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api.getMyCertificates().subscribe({
      next: (certificates) => {
        this.certificates.set(certificates);
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
}
