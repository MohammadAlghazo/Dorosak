import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-dashboard-page',
  template: `<section class="workspace-page">
    <p>WORKSPACE / 01</p>
    <h1>{{ locale.copy().dashboard }}</h1>
    <div class="workspace-grid">
      <article><span>Next step</span><strong>Continue your active pathway</strong></article>
      <article>
        <span>Progress</span><strong>Visible after Identity and Learning phases</strong>
      </article>
    </div>
  </section>`,
  styles: `
    .workspace-page {
      max-inline-size: var(--content-wide);
      margin-inline: auto;
    }
    .workspace-page > p {
      color: var(--color-brand);
    }
    .workspace-page h1 {
      font-size: clamp(2.5rem, 6vw, 4.5rem);
    }
    .workspace-grid {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: var(--space-4);
    }
    .workspace-grid article {
      display: grid;
      gap: var(--space-5);
      min-block-size: 14rem;
      padding: var(--space-6);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
    }
    .workspace-grid span {
      color: var(--color-muted);
    }
    @media (max-width: 700px) {
      .workspace-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPageComponent {
  protected readonly locale = inject(LocaleService);
}
