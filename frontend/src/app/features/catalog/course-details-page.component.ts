import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { CommerceApiClient } from '../../core/api/commerce-api.client';
import { EngagementApiClient } from '../../core/api/engagement-api.client';
import type { CourseReview, CourseReviewPage } from '../../core/api/engagement-api.types';
import type { PublicCoursePrice } from '../../core/api/discovery-api.types';
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
  protected readonly reviews = signal<CourseReviewPage | null>(null);
  protected readonly reviewRating = signal(5);
  protected readonly reviewText = signal('');
  protected readonly reviewBusy = signal(false);
  protected readonly reviewError = signal<string | null>(null);
  private readonly seo = inject(SeoService);
  private readonly commerceApi = inject(CommerceApiClient);
  private readonly engagementApi = inject(EngagementApiClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    effect(() => {
      const state = this.store.state();
      if (state.status === 'success' && state.course) {
        this.seo.setCourseMetadata(state.course);
        this.engagementApi
          .getCourseReviews(state.course.courseId)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: (reviews) => {
              this.reviews.set(reviews);
            },
          });
        if (this.session.isAuthenticated()) {
          this.engagementApi
            .getMyCourseReview(state.course.courseId)
            .pipe(
              catchError(() => of(null)),
              takeUntilDestroyed(this.destroyRef),
            )
            .subscribe({
              next: (review) => {
                if (!review) return;
                const current = this.reviews();
                if (current) this.reviews.set(mergePrivateReview(current, review));
                this.reviewRating.set(review.rating);
                this.reviewText.set(review.text);
              },
            });
        }
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

  protected demoCheckout(courseId: string, outcome: 'success' | 'failure'): void {
    if (!this.session.isAuthenticated()) {
      void this.router.navigate(['/', this.locale.locale(), 'auth', 'sign-in'], {
        queryParams: { returnUrl: this.router.url },
      });
      return;
    }
    if (this.enrolling()) return;
    this.enrolling.set(true);
    this.enrollmentError.set(null);
    this.commerceApi
      .demoCheckout(courseId, outcome)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (checkout) => {
          this.enrolling.set(false);
          if (checkout.paymentStatus === 'Succeeded' && checkout.enrollmentId) {
            void this.router.navigate(['/', this.locale.locale(), 'learn', checkout.enrollmentId]);
            return;
          }
          this.enrollmentError.set('COMMERCE.DEMO_PAYMENT_FAILED');
        },
        error: (error: unknown) => {
          this.enrolling.set(false);
          this.enrollmentError.set(
            error instanceof ApiProblem ? error.code : 'COMMERCE.DEMO_CHECKOUT_FAILED',
          );
        },
      });
  }

  protected setReviewRating(rating: number): void {
    this.reviewRating.set(rating);
  }

  protected setReviewText(event: Event): void {
    this.reviewText.set((event.target as HTMLTextAreaElement).value);
  }

  protected submitReview(courseId: string): void {
    if (!this.session.isAuthenticated() || this.reviewBusy()) return;
    this.reviewBusy.set(true);
    this.reviewError.set(null);
    const existing = this.currentUserReview(courseId);
    const request = existing
      ? this.engagementApi.updateCourseReview(
          courseId,
          existing.id,
          this.reviewRating(),
          this.reviewText(),
        )
      : this.engagementApi.createCourseReview(courseId, this.reviewRating(), this.reviewText());
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (review) => {
        const current = this.reviews();
        this.reviews.set(current ? replaceReview(current, review) : null);
        this.reviewText.set('');
        this.reviewBusy.set(false);
      },
      error: (error: unknown) => {
        this.reviewBusy.set(false);
        this.reviewError.set(error instanceof ApiProblem ? error.code : 'REVIEW.CREATE_FAILED');
      },
    });
  }

  protected currentUserReview(courseId: string): CourseReview | null {
    const userId = this.session.identity()?.userId;
    return (
      this.reviews()?.items.find(
        (review) => review.courseId === courseId && review.userId === userId,
      ) ?? null
    );
  }

  protected editReview(review: CourseReview): void {
    this.reviewRating.set(review.rating);
    this.reviewText.set(review.text);
  }

  protected deleteReview(courseId: string, reviewId: string): void {
    if (this.reviewBusy()) return;
    this.reviewBusy.set(true);
    this.engagementApi
      .deleteCourseReview(courseId, reviewId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const current = this.reviews();
          if (current) this.reviews.set(removeReview(current, reviewId));
          this.reviewText.set('');
          this.reviewRating.set(5);
          this.reviewBusy.set(false);
        },
        error: (error: unknown) => {
          this.reviewBusy.set(false);
          this.reviewError.set(error instanceof ApiProblem ? error.code : 'REVIEW.DELETE_FAILED');
        },
      });
  }
}

const replaceReview = (page: CourseReviewPage, review: CourseReview): CourseReviewPage => {
  const exists = page.items.some((item) => item.id === review.id);
  return {
    ...page,
    items: exists
      ? page.items.map((item) => (item.id === review.id ? review : item))
      : [review, ...page.items],
    averageRating: exists
      ? page.averageRating
      : (page.averageRating * page.totalCount + review.rating) / (page.totalCount + 1),
    totalCount: exists ? page.totalCount : page.totalCount + 1,
  };
};

const removeReview = (page: CourseReviewPage, reviewId: string): CourseReviewPage => {
  const removed = page.items.find((review) => review.id === reviewId);
  if (!removed) return page;
  const totalCount = Math.max(0, page.totalCount - 1);
  return {
    ...page,
    items: page.items.filter((review) => review.id !== reviewId),
    totalCount,
    averageRating:
      totalCount === 0 ? 0 : (page.averageRating * page.totalCount - removed.rating) / totalCount,
  };
};

const mergePrivateReview = (
  page: CourseReviewPage,
  privateReview: CourseReview,
): CourseReviewPage => ({
  ...page,
  items: page.items.map((review) =>
    review.id === privateReview.id ? { ...review, userId: privateReview.userId } : review,
  ),
});
