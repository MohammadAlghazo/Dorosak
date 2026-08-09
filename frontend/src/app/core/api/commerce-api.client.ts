import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, switchMap, type Observable } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import type { DemoCheckout } from './commerce-api.types';
import { IdentityApiClient } from './identity-api.client';
import { authenticatedMutationContext } from './phase6-api.helpers';

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
}
