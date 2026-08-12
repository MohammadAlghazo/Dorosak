import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import type { AdminAnalyticsOverview } from './analytics-api.types';
import type { ApiEnvelope } from './api-envelope';
import { authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class AnalyticsApiClient {
  private readonly http = inject(HttpClient);

  getAdminOverview(): Observable<AdminAnalyticsOverview> {
    return this.http
      .get<ApiEnvelope<AdminAnalyticsOverview>>('admin/analytics/overview', {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }
}
