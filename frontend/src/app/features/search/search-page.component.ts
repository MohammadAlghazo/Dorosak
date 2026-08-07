import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { HighlightSegment, PublicSearchSuggestion } from '../../core/api/discovery-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { SearchPageStore } from './search-page.store';

@Component({
  selector: 'drs-search-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [SearchPageStore],
  templateUrl: './search-page.component.html',
  styleUrl: './search-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(SearchPageStore);
  protected readonly query = new FormControl('', { nonNullable: true });

  constructor() {
    this.query.valueChanges.pipe(takeUntilDestroyed()).subscribe((query) => {
      this.store.updateDraftQuery(query);
    });
    effect(() => {
      const query = this.store.query();
      if (query !== this.query.value) this.query.setValue(query, { emitEvent: false });
    });
  }

  protected submit(): void {
    this.store.submitQuery(this.query.value);
  }

  protected useCorrection(correction: string): void {
    this.query.setValue(correction, { emitEvent: false });
    this.store.useCorrection(correction);
  }

  protected useSuggestion(suggestion: PublicSearchSuggestion): void {
    const query = this.suggestionText(suggestion);
    this.query.setValue(query, { emitEvent: false });
    this.store.submitQuery(query);
  }

  protected suggestionText(suggestion: PublicSearchSuggestion): string {
    return suggestion.segments.map((segment) => segment.text).join('');
  }

  protected trackSegment(index: number, segment: HighlightSegment): string {
    return `${String(index)}:${segment.matched ? '1' : '0'}:${segment.text}`;
  }
}
