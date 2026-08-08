import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { LearningApiClient } from '../../core/api/learning-api.client';
import type { Enrollment } from '../../core/api/learning-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';

type LibraryState =
  | { status: 'loading' | 'offline'; items: readonly Enrollment[]; errorCode: null }
  | { status: 'success' | 'empty'; items: readonly Enrollment[]; errorCode: null }
  | { status: 'error'; items: readonly Enrollment[]; errorCode: string | null };

@Component({
  selector: 'drs-my-learning-page',
  imports: [RouterLink],
  template: `
    <section class="library" aria-labelledby="library-title" aria-live="polite">
      <header>
        <div>
          <p class="kicker">LEARNING LOG / 08</p>
          <h1 id="library-title">{{ locale.locale() === 'ar' ? 'مساراتي' : 'My learning' }}</h1>
        </div>
        <a class="browse" [routerLink]="['/', locale.locale(), 'courses']">
          {{ locale.locale() === 'ar' ? 'استكشف مساراً جديداً' : 'Explore a new pathway' }}
        </a>
      </header>

      @switch (state().status) {
        @case ('loading') {
          <div class="loading-grid" role="status"><i></i><i></i><i></i></div>
        }
        @case ('offline') {
          <div class="state-panel" role="alert">
            <span>OFFLINE</span>
            <h2>
              {{
                locale.locale() === 'ar'
                  ? 'مكتبتك تحتاج اتصالاً'
                  : 'Your library needs a connection'
              }}
            </h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'الوسائط المحمية لا تُخزن محلياً.'
                  : 'Protected course media is never cached locally.'
              }}
            </p>
            <button type="button" (click)="load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="state-panel" role="alert">
            <span>{{ state().errorCode ?? 'LEARNING.LOAD_FAILED' }}</span>
            <h2>
              {{
                locale.locale() === 'ar' ? 'تعذر فتح مكتبتك' : 'Your library could not be opened'
              }}
            </h2>
            <button type="button" (click)="load()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('empty') {
          <div class="state-panel empty">
            <span>00</span>
            <h2>
              {{ locale.locale() === 'ar' ? 'لم تبدأ مساراً بعد' : 'No pathways started yet' }}
            </h2>
            <a [routerLink]="['/', locale.locale(), 'courses']">{{
              locale.locale() === 'ar' ? 'افتح الكتالوج' : 'Open catalog'
            }}</a>
          </div>
        }
        @case ('success') {
          <div class="course-grid">
            @for (enrollment of state().items; track enrollment.id; let index = $index) {
              <article>
                <div class="course-index">{{ String(index + 1).padStart(2, '0') }}</div>
                <div class="course-copy">
                  <p>{{ enrollment.status }}</p>
                  <h2>{{ enrollment.title }}</h2>
                  <small
                    >{{ locale.locale() === 'ar' ? 'الإصدار المثبت' : 'Pinned release' }} ·
                    {{ enrollment.releaseId.slice(0, 8) }}</small
                  >
                </div>
                <a [routerLink]="['/', locale.locale(), 'learn', enrollment.id]">
                  {{
                    enrollment.status === 'Completed'
                      ? locale.locale() === 'ar'
                        ? 'راجع المسار'
                        : 'Review pathway'
                      : locale.locale() === 'ar'
                        ? 'تابع التعلم'
                        : 'Continue learning'
                  }}
                </a>
              </article>
            }
          </div>
        }
      }
    </section>
  `,
  styles: `
    .library {
      max-inline-size: var(--content-wide);
      margin-inline: auto;
      padding-block: var(--space-6) var(--space-10);
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-5);
      margin-block-end: var(--space-8);
      border-block-end: 1px solid var(--color-border);
      padding-block-end: var(--space-6);
    }
    .kicker,
    article p {
      margin: 0;
      color: var(--color-brand);
      font-size: 0.75rem;
      font-weight: 750;
      letter-spacing: 0.13em;
    }
    h1 {
      margin: var(--space-2) 0 0;
      font-size: clamp(3rem, 8vw, 7rem);
      line-height: 0.9;
    }
    .browse,
    article > a,
    .state-panel a {
      min-block-size: 46px;
      display: inline-flex;
      align-items: center;
      padding-inline: var(--space-4);
      background: var(--color-ink);
      color: var(--color-surface);
      text-decoration: none;
    }
    .course-grid {
      display: grid;
      gap: 1px;
      background: var(--color-border);
      border: 1px solid var(--color-border);
    }
    article {
      display: grid;
      grid-template-columns: 5rem minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--space-5);
      padding: var(--space-6);
      background: var(--color-surface);
    }
    .course-index {
      color: var(--color-muted);
      font: 700 2rem/1 monospace;
    }
    article h2 {
      margin: var(--space-2) 0;
      font-size: clamp(1.4rem, 3vw, 2.4rem);
    }
    article small {
      color: var(--color-muted);
    }
    .state-panel {
      display: grid;
      justify-items: start;
      gap: var(--space-4);
      min-block-size: 22rem;
      align-content: center;
      padding: var(--space-7);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .state-panel > span {
      color: var(--color-brand);
      font: 700 2.5rem/1 monospace;
    }
    .state-panel h2 {
      max-inline-size: 18ch;
      font-size: clamp(2rem, 5vw, 4rem);
    }
    .state-panel button {
      min-block-size: 46px;
      padding-inline: var(--space-5);
      border: 0;
      background: var(--color-brand);
      color: var(--color-on-brand);
    }
    .loading-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: var(--space-4);
    }
    .loading-grid i {
      min-block-size: 18rem;
      background: var(--color-subtle);
    }
    @media (max-width: 720px) {
      header {
        align-items: start;
        flex-direction: column;
      }
      article {
        grid-template-columns: 3rem 1fr;
      }
      article > a {
        grid-column: 2;
        justify-self: start;
      }
      .loading-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyLearningPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly String = String;
  private readonly api = inject(LearningApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = signal<LibraryState>({
    status: 'loading',
    items: [],
    errorCode: null,
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    if (!this.connectivity.isOnline()) {
      this.state.set({ status: 'offline', items: [], errorCode: null });
      return;
    }
    this.state.set({ status: 'loading', items: [], errorCode: null });
    this.api
      .getEnrollments()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.state.set({
            status: items.length === 0 ? 'empty' : 'success',
            items,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          this.state.set({
            status: 'error',
            items: [],
            errorCode: error instanceof ApiProblem ? error.code : null,
          });
        },
      });
  }
}
