import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import type { DemoCheckout, DemoSubscription, DemoSubscriptionState } from './commerce-api.types';
import { IdentityApiClient } from './identity-api.client';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class CommerceApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  demoCheckout(courseId: string, outcome: 'success' | 'failure'): Observable<DemoCheckout> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<DemoCheckout>>(
          'commerce/demo-checkout',
          { courseId, outcome },
          {
            context: authenticatedMutationContext(),
            headers: new HttpHeaders({
              'Idempotency-Key': globalThis.crypto.randomUUID(),
            }),
          },
        ),
      ),
      map((response) => response.data),
    );
  }

  getDemoSubscription(): Observable<DemoSubscriptionState> {
    return this.http
      .get<ApiEnvelope<DemoSubscriptionState>>('me/subscription', {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  activateDemoSubscription(): Observable<DemoSubscription> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<DemoSubscription>>(
          'subscriptions',
          {},
          {
            context: authenticatedMutationContext(),
            headers: new HttpHeaders({ 'Idempotency-Key': globalThis.crypto.randomUUID() }),
          },
        ),
      ),
      map((response) => response.data),
    );
  }

  cancelDemoSubscription(subscriptionId: string): Observable<DemoSubscription> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<DemoSubscription>>(
          `subscriptions/${encodeURIComponent(subscriptionId)}/cancel`,
          {},
          {
            context: authenticatedMutationContext(),
            headers: new HttpHeaders({ 'Idempotency-Key': globalThis.crypto.randomUUID() }),
          },
        ),
      ),
      map((response) => response.data),
    );
  }
}
