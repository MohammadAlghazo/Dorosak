import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { HttpBackend, HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { finalize, map, type Observable, shareReplay, tap, throwError } from 'rxjs';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { SessionStore } from './session.store';

interface RefreshTokenResponseDto {
  accessToken: string;
}

@Injectable({ providedIn: 'root' })
export class AuthRefreshService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly session = inject(SessionStore);
  private readonly rawHttp = new HttpClient(inject(HttpBackend));
  private inFlight: Observable<string> | undefined;

  refresh(): Observable<string> {
    if (!isPlatformBrowser(this.platformId) || !this.runtimeConfig.value().capabilities.identity) {
      return throwError(() => new Error('Identity refresh is not available in this release.'));
    }
    if (this.inFlight) return this.inFlight;

    const xsrfToken = this.readCookie('XSRF-TOKEN');
    const headers = xsrfToken ? new HttpHeaders({ 'X-XSRF-TOKEN': xsrfToken }) : undefined;
    this.inFlight = this.rawHttp
      .post<RefreshTokenResponseDto>(
        this.runtimeConfig.apiUrl('auth/refresh'),
        {},
        {
          credentials: 'include',
          ...(headers ? { headers } : {}),
        },
      )
      .pipe(
        map((response) => response.accessToken),
        tap((accessToken) => this.session.establish(accessToken, this.session.identity())),
        finalize(() => {
          this.inFlight = undefined;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.inFlight;
  }

  private readCookie(name: string): string | null {
    const prefix = `${name}=`;
    const cookie = this.document.cookie.split('; ').find((entry) => entry.startsWith(prefix));
    return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : null;
  }
}
