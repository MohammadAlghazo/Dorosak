import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AnalyticsApiClient } from '../../core/api/analytics-api.client';
import type { AdminAnalyticsOverview } from '../../core/api/analytics-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { AnalyticsPageComponent } from './analytics-page.component';

describe('AnalyticsPageComponent', () => {
  let fixture: ComponentFixture<AnalyticsPageComponent>;
  let api: { getAdminOverview: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = { getAdminOverview: vi.fn(() => of(overview)) };
    await TestBed.configureTestingModule({
      imports: [AnalyticsPageComponent],
      providers: [
        { provide: AnalyticsApiClient, useValue: api },
        { provide: LocaleService, useValue: { locale: signal<'ar' | 'en'>('en') } },
      ],
    }).compileComponents();
  });

  it('renders aggregate indicators and refreshes without exposing personal data', () => {
    fixture = TestBed.createComponent(AnalyticsPageComponent);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('h1')?.textContent).toContain('Platform pulse');
    expect(root.textContent).toContain('42');
    expect(root.textContent).toContain('Completed DEMO orders');
    expect(root.textContent).not.toContain('learner@example.test');
    expect(root.querySelectorAll('.signal-group')).toHaveLength(4);

    root.querySelector<HTMLButtonElement>('button')?.click();
    fixture.detectChanges();
    expect(api.getAdminOverview).toHaveBeenCalledTimes(2);
  });

  it('shows a non-stale error state when the overview request fails', () => {
    api.getAdminOverview.mockReturnValue(throwError(() => new Error('offline')));
    fixture = TestBed.createComponent(AnalyticsPageComponent);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[role="alert"]')?.textContent).toContain(
      'Indicators could not be loaded',
    );
    expect(root.querySelector('.signal-grid')).toBeNull();
  });
});

const overview: AdminAnalyticsOverview = {
  generatedAt: '2030-01-02T03:04:05Z',
  totalUsers: 42,
  activeUsers: 40,
  totalCourses: 8,
  publishedCourses: 5,
  totalEnrollments: 27,
  completedEnrollments: 11,
  completedDemoOrders: 16,
  activeDemoSubscriptions: 9,
  issuedCertificates: 11,
  activeCertificates: 10,
  pendingPublicationReviews: 2,
  openModerationCases: 3,
  pendingOutboxMessages: 4,
  retryingOutboxMessages: 1,
};
