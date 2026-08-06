import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'drs-toast-region',
  template: `
    <section
      class="toast-region"
      aria-label="Notifications"
      aria-live="polite"
      aria-relevant="additions"
    >
      @for (toast of toastService.messages(); track toast.id) {
        <div class="toast-message">
          <span>{{ toast.message }}</span>
          <button
            type="button"
            aria-label="Dismiss notification"
            (click)="toastService.dismiss(toast.id)"
          >
            ×
          </button>
        </div>
      }
    </section>
  `,
  styles: `
    .toast-region {
      position: fixed;
      inset-block-end: var(--space-5);
      inset-inline-end: var(--space-5);
      z-index: var(--z-toast);
      display: grid;
      gap: var(--space-2);
    }
    .toast-message {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      max-inline-size: 24rem;
      padding: var(--space-3) var(--space-4);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-inline-start: 4px solid var(--color-brand);
      box-shadow: var(--shadow-2);
    }
    button {
      min-inline-size: 44px;
      min-block-size: 44px;
      color: inherit;
      background: transparent;
      border: 0;
      font-size: 1.5rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastRegionComponent {
  protected readonly toastService = inject(ToastService);
}
