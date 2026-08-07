import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { SystemApiClient } from '../../core/api/system-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { FeaturedCoursesStore } from './featured-courses.store';

@Component({
  selector: 'drs-home-page',
  imports: [RouterLink],
  providers: [FeaturedCoursesStore],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly featured = inject(FeaturedCoursesStore);
  private readonly systemApi = inject(SystemApiClient);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly apiStatus = signal<'checking' | 'online' | 'unavailable'>('checking');
  constructor() {
    afterNextRender(() => {
      this.systemApi
        .getStatus()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.apiStatus.set('online');
          },
          error: () => {
            this.apiStatus.set('unavailable');
          },
        });
    });
  }
}
