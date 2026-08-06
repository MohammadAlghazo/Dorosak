import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-search-page',
  imports: [ReactiveFormsModule],
  template: `
    <section class="search-page">
      <p>{{ locale.locale() === 'ar' ? 'بحث عام' : 'Public search' }}</p>
      <h1>
        {{ locale.locale() === 'ar' ? 'ما الذي تريد إتقانه؟' : 'What do you want to master?' }}
      </h1>
      <label for="course-search">{{
        locale.locale() === 'ar' ? 'اكتب حرفين على الأقل' : 'Enter at least two characters'
      }}</label>
      <input id="course-search" type="search" [formControl]="query" autocomplete="off" />
      <div aria-live="polite">
        @if (term().length < 2) {
          <p class="hint">
            {{
              locale.locale() === 'ar'
                ? 'ابدأ بكلمة واضحة مثل ويب أو بيانات.'
                : 'Start with a clear term such as web or data.'
            }}
          </p>
        } @else if (results().length === 0) {
          <p>
            {{ locale.locale() === 'ar' ? 'لا توجد نتائج تمهيدية.' : 'No foundation results.' }}
          </p>
        } @else {
          <ul>
            @for (result of results(); track result) {
              <li>{{ result }}</li>
            }
          </ul>
        }
      </div>
    </section>
  `,
  styles: `
    .search-page {
      max-inline-size: 52rem;
      min-block-size: 70dvh;
      margin-inline: auto;
      padding: var(--space-12) var(--page-gutter);
    }
    .search-page > p {
      color: var(--color-brand);
    }
    .search-page h1 {
      font-size: clamp(2.5rem, 6vw, 5rem);
    }
    .search-page label {
      display: block;
      margin-block: var(--space-8) var(--space-2);
      font-weight: 650;
    }
    .search-page input {
      inline-size: 100%;
      min-block-size: 60px;
      padding-inline: var(--space-4);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      font-size: 1.2rem;
    }
    .hint {
      color: var(--color-muted);
    }
    .search-page ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .search-page li {
      padding-block: var(--space-4);
      border-block-end: 1px solid var(--color-border);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly query = new FormControl('', { nonNullable: true });
  protected readonly term = toSignal(
    this.query.valueChanges.pipe(startWith(''), debounceTime(250), distinctUntilChanged()),
    { initialValue: '' },
  );
  protected readonly results = computed(() => {
    const term = this.term().trim().toLocaleLowerCase();
    return term.length < 2
      ? []
      : ['Web systems', 'Data reasoning', 'Practical communication']
          .filter((item) => item.toLocaleLowerCase().includes(term))
          .slice(0, 8);
  });
}
