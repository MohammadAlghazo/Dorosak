import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { SystemApiClient } from '../../core/api/system-api.client';
import { LocaleService } from '../../core/i18n/locale.service';

interface PathwayPreview {
  id: string;
  number: string;
  title: string;
  description: string;
  level: string;
}

@Component({
  selector: 'drs-home-page',
  imports: [RouterLink],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly systemApi = inject(SystemApiClient);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly apiStatus = signal<'checking' | 'online' | 'unavailable'>('checking');
  protected readonly pathways = computed<readonly PathwayPreview[]>(() =>
    this.locale.locale() === 'ar'
      ? [
          {
            id: 'web',
            number: '01',
            title: 'تطوير الويب',
            description: 'من الأساسيات إلى تطبيقات قابلة للإطلاق.',
            level: 'مسار متدرج',
          },
          {
            id: 'data',
            number: '02',
            title: 'البيانات والتحليل',
            description: 'افهم الأرقام وحوّلها إلى قرارات واضحة.',
            level: 'مشاريع عملية',
          },
          {
            id: 'business',
            number: '03',
            title: 'مهارات العمل',
            description: 'تواصل، خطط، وقدّم عملك بثقة.',
            level: 'تعلم مرن',
          },
        ]
      : [
          {
            id: 'web',
            number: '01',
            title: 'Web development',
            description: 'From foundations to software ready to ship.',
            level: 'Guided pathway',
          },
          {
            id: 'data',
            number: '02',
            title: 'Data and analysis',
            description: 'Turn numbers into decisions you can explain.',
            level: 'Practical projects',
          },
          {
            id: 'business',
            number: '03',
            title: 'Work skills',
            description: 'Communicate, plan, and present with confidence.',
            level: 'Flexible learning',
          },
        ],
  );

  constructor() {
    afterNextRender(() => {
      this.systemApi
        .getStatus()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => this.apiStatus.set('online'),
          error: () => this.apiStatus.set('unavailable'),
        });
    });
  }
}
