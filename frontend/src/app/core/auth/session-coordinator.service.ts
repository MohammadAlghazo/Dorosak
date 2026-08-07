import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import {
  catchError,
  from,
  map,
  type Observable,
  of,
  shareReplay,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import { IdentityApiClient } from '../api/identity-api.client';
import type {
  AuthSession,
  SignInRequest,
  SignInResult,
} from '../api/identity-api.types';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { IndexedDbService } from '../pwa/indexed-db.service';
import { AuthRefreshService } from './auth-refresh.service';
import { SessionStore } from './session.store';

export interface PendingMfaChallenge {
  challengeToken: string;
  challengeExpiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class SessionCoordinator {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly indexedDb = inject(IndexedDbService);
  private readonly refreshService = inject(AuthRefreshService);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly session = inject(SessionStore);
  private readonly activeMfaChallenge = signal<PendingMfaChallenge | null>(null);
  private restorationAttempted = false;
  private restorationRequest: Observable<boolean> | undefined;

  readonly pendingMfaChallenge = this.activeMfaChallenge.asReadonly();

  ensureAuthenticated(): Observable<boolean> {
    if (this.session.isAuthenticated()) return of(true);
    if (this.session.status() === 'anonymous') return of(false);
    if (this.restorationRequest) return this.restorationRequest;
    if (
      !isPlatformBrowser(this.platformId) ||
      !this.runtimeConfig.value().capabilities.identity ||
      this.restorationAttempted
    ) {
      this.session.markAnonymous();
      return of(false);
    }
    this.restorationAttempted = true;
    this.session.beginRestoration();
    this.restorationRequest = this.refreshService.refresh().pipe(
      map(() => this.session.isAuthenticated()),
      catchError(() => {
        this.session.markAnonymous();
        return of(false);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.restorationRequest;
  }

  signIn(request: SignInRequest): Observable<SignInResult> {
    return this.identityApi.signIn(request).pipe(
      tap((result) => {
        if (result.outcome === 'authenticated') {
          this.establish(result.session);
          return;
        }
        this.activeMfaChallenge.set({
          challengeToken: result.challengeToken,
          challengeExpiresAt: result.challengeExpiresAt,
        });
      }),
    );
  }

  completeMfa(code: string, recovery = false): Observable<AuthSession> {
    const challenge = this.activeMfaChallenge();
    if (!challenge) {
      return throwError(() => new Error('An MFA challenge is not active.'));
    }
    const request = recovery
      ? this.identityApi.completeMfaRecovery(challenge.challengeToken, code)
      : this.identityApi.completeMfaChallenge(challenge.challengeToken, code);
    return request.pipe(
      tap((session) => {
        this.establish(session);
      }),
    );
  }

  establish(session: AuthSession): void {
    this.session.establish(session);
    this.identityApi.resetCsrf();
    this.activeMfaChallenge.set(null);
    this.restorationAttempted = true;
    this.restorationRequest = undefined;
  }

  logout(): Observable<void> {
    const signOutRequest = this.session.isAuthenticated()
      ? this.identityApi.signOut().pipe(catchError(() => of(undefined)))
      : of(undefined);
    return signOutRequest.pipe(switchMap(() => this.endLocalSession()));
  }

  endLocalSession(): Observable<void> {
    const userId = this.session.identity()?.userId;
    const purgeRequest = userId
      ? from(this.indexedDb.purgeUser(userId)).pipe(catchError(() => of(undefined)))
      : of(undefined);
    return purgeRequest.pipe(
      tap(() => {
        this.session.markAnonymous();
        this.identityApi.resetCsrf();
        this.activeMfaChallenge.set(null);
        this.restorationAttempted = true;
        this.restorationRequest = undefined;
      }),
      map(() => undefined),
    );
  }
}
