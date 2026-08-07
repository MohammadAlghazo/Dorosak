import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, shareReplay, switchMap, tap } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import type { ApiEnvelope } from './api-envelope';
import {
  API_REQUEST,
  DEADLINE_MS,
  RETRY_IDEMPOTENT_GET,
  SKIP_AUTH,
  SKIP_REFRESH,
} from './api-context';
import {
  type AcceptedResult,
  type AuthSession,
  type CompletedResult,
  type CredentialsChangeRequest,
  type EmailVerificationConfirmRequest,
  type EmailChangeRequest,
  type IdentitySnapshot,
  type MfaConfirmResult,
  type MfaSetupResult,
  type PasswordResetRequest,
  type RegisterRequest,
  type SessionSummary,
  type SessionsResult,
  type SignInRequest,
  type SignInResult,
} from './identity-api.types';

@Injectable({ providedIn: 'root' })
export class IdentityApiClient {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private csrfBootstrapRequest: Observable<undefined> | undefined;

  bootstrapCsrf(): Observable<undefined> {
    this.csrfBootstrapRequest ??= this.http
        .get<unknown>('auth/csrf', {
          observe: 'response',
          context: csrfContext(this.session.isAuthenticated()),
        })
        .pipe(
          map(() => undefined),
          tap({
            error: () => {
              this.csrfBootstrapRequest = undefined;
            },
          }),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    return this.csrfBootstrapRequest;
  }

  resetCsrf(): void {
    this.csrfBootstrapRequest = undefined;
  }

  register(request: RegisterRequest): Observable<AcceptedResult> {
    return this.guestMutation('auth/register', request);
  }

  signIn(request: SignInRequest): Observable<SignInResult> {
    return this.guestMutation('auth/sign-in', request);
  }

  completeMfaChallenge(challengeToken: string, code: string): Observable<AuthSession> {
    return this.guestMutation('auth/mfa/challenge', { challengeToken, code });
  }

  completeMfaRecovery(challengeToken: string, recoveryCode: string): Observable<AuthSession> {
    return this.guestMutation('auth/mfa/recovery', { challengeToken, recoveryCode });
  }

  refreshSession(): Observable<AuthSession> {
    return this.unsafeMutation(
      () =>
        this.http.post<ApiEnvelope<AuthSession>>('auth/refresh', null, {
          context: refreshContext(10_000),
          credentials: 'include',
          withCredentials: true,
        }),
    ).pipe(map((response) => response.data));
  }

  signOut(): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .post<unknown>('auth/sign-out', null, {
            context: privateMutationContext(),
          })
          .pipe(map(() => undefined)),
    );
  }

  sendEmailVerification(email: string, locale: 'ar' | 'en'): Observable<AcceptedResult> {
    return this.guestMutation('auth/email-verification/send', { email, locale });
  }

  confirmEmailVerification(request: EmailVerificationConfirmRequest): Observable<CompletedResult> {
    return this.guestMutation('auth/email-verification/confirm', request);
  }

  requestEmailChange(request: EmailChangeRequest): Observable<AcceptedResult> {
    return this.unsafeMutation(() =>
      this.http
        .post<ApiEnvelope<AcceptedResult>>('auth/email/change/request', request, {
          context: privateMutationContext(),
        })
        .pipe(map((response) => response.data)),
    );
  }

  confirmEmailChange(request: EmailVerificationConfirmRequest): Observable<CompletedResult> {
    return this.guestMutation('auth/email/change/confirm', request);
  }

  requestPasswordReset(email: string, locale: 'ar' | 'en'): Observable<AcceptedResult> {
    return this.guestMutation('auth/password/forgot', { email, locale });
  }

  resetPassword(request: PasswordResetRequest): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .post<unknown>('auth/password/reset', request, { context: guestContext() })
          .pipe(map(() => undefined)),
    );
  }

  changePassword(request: CredentialsChangeRequest): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .post<unknown>('auth/password/change', request, {
            context: privateMutationContext(),
          })
          .pipe(map(() => undefined)),
    );
  }

  setupMfa(): Observable<MfaSetupResult> {
    return this.unsafeMutation(
      () => this.http.post<ApiEnvelope<MfaSetupResult>>('auth/mfa/setup', null, {
        context: privateMutationContext(),
      }),
    ).pipe(map((response) => response.data));
  }

  confirmMfa(code: string): Observable<MfaConfirmResult> {
    return this.unsafeMutation(
      () =>
        this.http.post<ApiEnvelope<MfaConfirmResult>>('auth/mfa/confirm', { code }, {
          context: privateMutationContext(),
        }),
    ).pipe(map((response) => response.data));
  }

  disableMfa(currentPassword: string): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .delete<unknown>('auth/mfa', {
            body: { currentPassword },
            context: privateMutationContext(),
          })
          .pipe(map(() => undefined)),
    );
  }

  getProfile(): Observable<IdentitySnapshot> {
    return this.http
      .get<ApiEnvelope<IdentitySnapshot>>('me/profile', {
        context: privateReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  getSessions(): Observable<readonly SessionSummary[]> {
    return this.http
      .get<ApiEnvelope<SessionsResult>>('me/sessions', {
        context: privateReadContext(),
      })
      .pipe(map((response) => response.data.sessions));
  }

  revokeSession(sessionId: string): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .delete<unknown>(`me/sessions/${encodeURIComponent(sessionId)}`, {
            context: privateMutationContext(),
          })
          .pipe(map(() => undefined)),
    );
  }

  revokeAllSessions(): Observable<undefined> {
    return this.unsafeMutation(
      () =>
        this.http
          .delete<unknown>('me/sessions', { context: privateMutationContext() })
          .pipe(map(() => undefined)),
    );
  }

  private guestMutation<T>(path: string, body: unknown): Observable<T> {
    return this.unsafeMutation(
      () =>
        this.http
          .post<ApiEnvelope<T>>(path, body, { context: guestContext() })
          .pipe(map((response) => response.data)),
    );
  }

  private unsafeMutation<T>(request: () => Observable<T>): Observable<T> {
    return this.bootstrapCsrf().pipe(switchMap(request));
  }
}

const baseContext = (deadlineMs: number): HttpContext =>
  new HttpContext()
    .set(API_REQUEST, true)
    .set(RETRY_IDEMPOTENT_GET, false)
    .set(DEADLINE_MS, deadlineMs);

const guestContext = (deadlineMs = 15_000): HttpContext =>
  baseContext(deadlineMs).set(SKIP_AUTH, true).set(SKIP_REFRESH, true);

const refreshContext = (deadlineMs: number): HttpContext =>
  baseContext(deadlineMs).set(SKIP_AUTH, true).set(SKIP_REFRESH, true);

const csrfContext = (authenticated: boolean): HttpContext =>
  authenticated
    ? baseContext(5_000).set(SKIP_REFRESH, true)
    : guestContext(5_000);

const privateReadContext = (): HttpContext => baseContext(15_000);

const privateMutationContext = (): HttpContext => baseContext(15_000);
