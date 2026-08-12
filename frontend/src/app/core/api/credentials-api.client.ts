import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import { API_REQUEST, DEADLINE_MS, PUBLIC_API_REQUEST } from './api-context';
import type { ApiEnvelope } from './api-envelope';
import type { Certificate, PublicCertificate } from './credentials-api.types';
import { authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class CredentialsApiClient {
  private readonly http = inject(HttpClient);

  getMyCertificates(): Observable<readonly Certificate[]> {
    return this.http
      .get<ApiEnvelope<readonly Certificate[]>>('me/certificates', {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  getMyCertificate(certificateId: string): Observable<Certificate> {
    return this.http
      .get<ApiEnvelope<Certificate>>(`me/certificates/${encodeURIComponent(certificateId)}`, {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  verifyCertificate(verificationCode: string): Observable<PublicCertificate> {
    return this.http
      .get<ApiEnvelope<PublicCertificate>>(
        `certificates/verify/${encodeURIComponent(verificationCode)}`,
        {
          context: new HttpContext()
            .set(API_REQUEST, true)
            .set(PUBLIC_API_REQUEST, true)
            .set(DEADLINE_MS, 15_000),
        },
      )
      .pipe(map((response) => response.data));
  }
}
