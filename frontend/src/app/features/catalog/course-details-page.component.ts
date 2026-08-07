import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { PublicCoursePrice } from '../../core/api/discovery-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { SeoService } from '../../core/i18n/seo.service';
import { CourseDetailsStore } from './course-details.store';

@Component({
  selector: 'drs-course-details-page',
  imports: [RouterLink],
  providers: [CourseDetailsStore],
  templateUrl: './course-details-page.component.html',
  styleUrl: './course-details-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseDetailsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(CourseDetailsStore);
  private readonly seo = inject(SeoService);

  constructor() {
    effect(() => {
      const state = this.store.state();
      if (state.status === 'success' && state.course) {
        this.seo.setCourseMetadata(state.course);
      } else if (state.status === 'notFound') {
        this.seo.setCourseNotFoundMetadata();
      }
    });
  }

  protected durationLabel(minutes: number): string {
    const hours = Math.max(1, Math.round(minutes / 60));
    return this.locale.locale() === 'ar' ? `${String(hours)} ساعة` : `${String(hours)} hours`;
  }

  protected priceLabel(price: PublicCoursePrice | null): string {
    if (!price || price.type === 'free') return this.locale.locale() === 'ar' ? 'مجاني' : 'Free';
    return price.amount && price.currency
      ? `${price.amount} ${price.currency}`
      : this.locale.locale() === 'ar'
        ? 'مدفوع'
        : 'Paid';
  }
}
