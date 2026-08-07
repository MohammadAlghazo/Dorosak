import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { finalize, map, type Observable, shareReplay, tap, throwError } from 'rxjs';
import { IdentityApiClient } from '../api/identity-api.client';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { SessionStore } from './session.store';

@Injectable({ providedIn: 'root' })
export class AuthRefreshService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly session = inject(SessionStore);
  private inFlight: Observable<string> | undefined;

  refresh(): Observable<string> {
    if (!isPlatformBrowser(this.platformId) || !this.runtimeConfig.value().capabilities.identity) {
      return throwError(() => new Error('Identity refresh is not available.'));
    }
    if (this.inFlight) return this.inFlight;

    this.inFlight = this.identityApi.refreshSession().pipe(
      tap((session) => {
        this.session.establish(session);
        this.identityApi.resetCsrf();
      }),
      map((session) => session.accessToken),
      finalize(() => {
        this.inFlight = undefined;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.inFlight;
  }
}
