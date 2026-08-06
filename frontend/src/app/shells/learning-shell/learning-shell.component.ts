import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-learning-shell',
  imports: [RouterLink, RouterOutlet],
  template: `
    <header>
      <a [routerLink]="['/', locale.locale(), 'dashboard']">← {{ locale.copy().dashboard }}</a
      ><strong>{{ locale.copy().brand }}</strong>
    </header>
    <main id="main-content" tabindex="-1"><router-outlet /></main>
  `,
  styles: `
    :host {
      display: grid;
      grid-template-rows: 60px 1fr;
      min-block-size: 100dvh;
      background: #08111f;
      color: #f8fafc;
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-inline: var(--page-gutter);
      border-block-end: 1px solid #253247;
    }
    header a {
      color: #99f6e4;
    }
    main {
      padding: var(--space-5) var(--page-gutter);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningShellComponent {
  protected readonly locale = inject(LocaleService);
}
