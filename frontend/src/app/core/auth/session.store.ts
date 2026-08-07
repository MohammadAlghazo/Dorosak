import { computed, Injectable, signal } from '@angular/core';
import type { AuthSession, IdentitySnapshot } from '../api/identity-api.types';

export type SessionStatus = 'unknown' | 'restoring' | 'anonymous' | 'authenticated';

type SessionState =
  | { status: 'unknown'; accessToken: null; accessTokenExpiresAt: null; identity: null }
  | { status: 'restoring'; accessToken: null; accessTokenExpiresAt: null; identity: null }
  | { status: 'anonymous'; accessToken: null; accessTokenExpiresAt: null; identity: null }
  | {
      status: 'authenticated';
      accessToken: string;
      accessTokenExpiresAt: string;
      identity: IdentitySnapshot;
    };

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly state = signal<SessionState>({
    status: 'unknown',
    accessToken: null,
    accessTokenExpiresAt: null,
    identity: null,
  });

  readonly status = computed<SessionStatus>(() => this.state().status);
  readonly accessToken = computed(() => this.state().accessToken);
  readonly accessTokenExpiresAt = computed(() => this.state().accessTokenExpiresAt);
  readonly identity = computed(() => this.state().identity);
  readonly roles = computed<readonly string[]>(() => this.state().identity?.roles ?? []);
  readonly permissions = computed<readonly string[]>(() => this.state().identity?.permissions ?? []);
  readonly isAuthenticated = computed(() => this.state().status === 'authenticated');
  readonly isRestoring = computed(() => this.state().status === 'restoring');

  beginRestoration(): void {
    if (this.state().status === 'unknown') {
      this.state.set({
        status: 'restoring',
        accessToken: null,
        accessTokenExpiresAt: null,
        identity: null,
      });
    }
  }

  establish(session: AuthSession): void {
    this.state.set({
      status: 'authenticated',
      accessToken: session.accessToken,
      accessTokenExpiresAt: session.accessTokenExpiresAt,
      identity: cloneIdentity(session.identity),
    });
  }

  updateIdentity(identity: IdentitySnapshot): void {
    const current = this.state();
    if (current.status !== 'authenticated') return;
    this.state.set({ ...current, identity: cloneIdentity(identity) });
  }

  markAnonymous(): void {
    this.state.set({
      status: 'anonymous',
      accessToken: null,
      accessTokenExpiresAt: null,
      identity: null,
    });
  }

  clear(): void {
    this.markAnonymous();
  }

  isAccessTokenExpired(now = Date.now()): boolean {
    const expiresAt = this.accessTokenExpiresAt();
    return expiresAt !== null && (!Number.isFinite(Date.parse(expiresAt)) || Date.parse(expiresAt) <= now);
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }
}

const cloneIdentity = (identity: IdentitySnapshot): IdentitySnapshot => ({
  ...identity,
  roles: [...identity.roles],
  permissions: [...identity.permissions],
  authenticationMethods: [...identity.authenticationMethods],
});
