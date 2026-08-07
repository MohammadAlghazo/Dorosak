import { A11yModule } from '@angular/cdk/a11y';
import { isPlatformBrowser } from '@angular/common';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  PLATFORM_ID,
  signal,
  viewChild,
} from '@angular/core';
import type { ElementRef } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type {
  CatalogFilters,
  CatalogSort,
  CourseLevel,
  DurationBand,
  PriceType,
} from '../../core/api/discovery-api.types';
import type { Locale } from '../../core/i18n/locale';
import { LocaleService } from '../../core/i18n/locale.service';
import { CatalogPageStore } from './catalog-page.store';

@Component({
  selector: 'drs-catalog-page',
  imports: [A11yModule, ReactiveFormsModule, RouterLink],
  providers: [CatalogPageStore],
  templateUrl: './catalog-page.component.html',
  styleUrl: './catalog-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(CatalogPageStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly closeFilterButton = viewChild<ElementRef<HTMLButtonElement>>('closeFilterButton');
  private filterTrigger: HTMLButtonElement | null = null;

  protected readonly filtersOpen = signal(false);
  protected readonly mobileFilters = signal(false);
  protected readonly filterPanelHidden = computed(
    () => this.mobileFilters() && !this.filtersOpen(),
  );
  protected readonly filterForm = new FormGroup({
    category: new FormControl('', { nonNullable: true }),
    tag: new FormControl('', { nonNullable: true }),
    language: new FormControl('', { nonNullable: true }),
    level: new FormControl('', { nonNullable: true }),
    price: new FormControl('', { nonNullable: true }),
    duration: new FormControl('', { nonNullable: true }),
    instructor: new FormControl('', { nonNullable: true }),
    sort: new FormControl<CatalogSort>('newest', { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const filters = this.store.filters();
      this.filterForm.setValue(
        {
          category: filters.category ?? '',
          tag: filters.tag ?? '',
          language: filters.language ?? '',
          level: filters.level ?? '',
          price: filters.price ?? '',
          duration: filters.duration ?? '',
          instructor: filters.instructor ?? '',
          sort: filters.sort,
        },
        { emitEvent: false },
      );
    });

    afterNextRender(() => {
      if (!isPlatformBrowser(this.platformId)) return;
      const media = matchMedia('(max-width: 760px)');
      const update = () => {
        this.mobileFilters.set(media.matches);
        if (!media.matches) this.filtersOpen.set(false);
      };
      update();
      media.addEventListener('change', update);
      this.destroyRef.onDestroy(() => {
        media.removeEventListener('change', update);
      });
    });
  }

  protected applyFilters(): void {
    const value = this.filterForm.getRawValue();
    this.store.setFilters({
      category: nullable(value.category),
      tag: nullable(value.tag),
      language: nullable(value.language) as Locale | null,
      level: nullable(value.level) as CourseLevel | null,
      price: nullable(value.price) as PriceType | null,
      duration: nullable(value.duration) as DurationBand | null,
      instructor: nullable(value.instructor),
      sort: value.sort,
    });
    this.closeFilters();
  }

  protected clearFilters(): void {
    this.store.clearFilters();
    this.closeFilters();
  }

  protected openFilters(trigger: HTMLButtonElement): void {
    this.filterTrigger = trigger;
    this.filtersOpen.set(true);
    queueMicrotask(() => this.closeFilterButton()?.nativeElement.focus());
  }

  protected closeFilters(): void {
    if (!this.filtersOpen()) return;
    this.filtersOpen.set(false);
    queueMicrotask(() => this.filterTrigger?.focus());
  }

  protected levelLabel(level: CourseLevel): string {
    const labels =
      this.locale.locale() === 'ar'
        ? { beginner: 'مبتدئ', intermediate: 'متوسط', advanced: 'متقدم' }
        : { beginner: 'Beginner', intermediate: 'Intermediate', advanced: 'Advanced' };
    return labels[level];
  }

  protected durationLabel(minutes: number): string {
    const hours = Math.max(1, Math.round(minutes / 60));
    return this.locale.locale() === 'ar' ? `${String(hours)} ساعة` : `${String(hours)} hours`;
  }

  protected courseNumber(index: number): string {
    return String(index + 1).padStart(2, '0');
  }

  protected priceLabel(filters: CatalogFilters['price']): string {
    if (filters === 'free') return this.locale.locale() === 'ar' ? 'مجاني' : 'Free';
    return this.locale.locale() === 'ar' ? 'مدفوع' : 'Paid';
  }
}

const nullable = (value: string): string | null => value.trim() || null;
