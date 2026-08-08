import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import type { PublicCoursePrice } from '../../core/api/discovery-api.types';
import { LearningApiClient } from '../../core/api/learning-api.client';
import { SessionStore } from '../../core/auth/session.store';
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
  protected readonly session = inject(SessionStore);
  protected readonly enrolling = signal(false);
  protected readonly enrollmentError = signal<string | null>(null);
  private readonly seo = inject(SeoService);
  private readonly learningApi = inject(LearningApiClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

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

  protected enroll(courseId: string): void {
    if (!this.session.isAuthenticated()) {
      void this.router.navigate(['/', this.locale.locale(), 'auth', 'sign-in'], {
        queryParams: { returnUrl: this.router.url },
      });
      return;
    }
    if (this.enrolling()) return;
    this.enrolling.set(true);
    this.enrollmentError.set(null);
    this.learningApi
      .enroll(courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (enrollment) => {
          this.enrolling.set(false);
          void this.router.navigate(['/', this.locale.locale(), 'learn', enrollment.id]);
        },
        error: (error: unknown) => {
          this.enrolling.set(false);
          this.enrollmentError.set(
            error instanceof ApiProblem ? error.code : 'LEARNING.ENROLL_FAILED',
          );
        },
      });
  }
}
