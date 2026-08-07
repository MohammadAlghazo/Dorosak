import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import type { AuthSession, SignInResult } from '../api/identity-api.types';
import { IdentityApiClient } from '../api/identity-api.client';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { IndexedDbService } from '../pwa/indexed-db.service';
import { AuthRefreshService } from './auth-refresh.service';
import { SessionCoordinator } from './session-coordinator.service';
import { SessionStore } from './session.store';

const resetCsrf = vi.fn();
const signOut = vi.fn(() => of(undefined));
const completeMfaChallenge = vi.fn(() => of(authSession()));
const signIn = vi.fn();
const purgeUser = vi.fn(() => Promise.resolve());

describe('SessionCoordinator', () => {
  beforeEach(() => {
    resetCsrf.mockClear();
    signOut.mockClear();
    completeMfaChallenge.mockClear();
    signIn.mockReset();
    purgeUser.mockClear();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: IdentityApiClient,
          useValue: { signIn, completeMfaChallenge, signOut, resetCsrf },
        },
        { provide: AuthRefreshService, useValue: { refresh: () => of('access-token') } },
        { provide: IndexedDbService, useValue: { purgeUser } },
        {
          provide: RuntimeConfigService,
          useValue: { value: () => ({ capabilities: { identity: true } }) },
        },
      ],
    });
  });

  it('keeps MFA pending until the second factor establishes the session', async () => {
    const mfaResult: SignInResult = {
      outcome: 'mfaRequired',
      session: null,
      challengeToken: 'challenge-token',
      challengeExpiresAt: '2030-01-01T00:05:00Z',
    };
    signIn.mockReturnValue(of(mfaResult));
    const coordinator = TestBed.inject(SessionCoordinator);
    const store = TestBed.inject(SessionStore);

    await firstValueFrom(coordinator.signIn({ email: 'learner@example.test', password: 'password' }));

    expect(store.isAuthenticated()).toBe(false);
    expect(coordinator.pendingMfaChallenge()?.challengeToken).toBe('challenge-token');

    await firstValueFrom(coordinator.completeMfa('123456'));

    expect(completeMfaChallenge).toHaveBeenCalledWith('challenge-token', '123456');
    expect(store.isAuthenticated()).toBe(true);
    expect(coordinator.pendingMfaChallenge()).toBeNull();
    expect(resetCsrf).toHaveBeenCalledOnce();
  });

  it('purges user-scoped offline data before ending the local session', async () => {
    const store = TestBed.inject(SessionStore);
    store.establish(authSession());
    const coordinator = TestBed.inject(SessionCoordinator);

    await firstValueFrom(coordinator.logout());

    expect(signOut).toHaveBeenCalledOnce();
    expect(purgeUser).toHaveBeenCalledWith('user-1');
    expect(store.status()).toBe('anonymous');
  });
});

const authSession = (): AuthSession => ({
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  identity: {
    userId: 'user-1',
    sessionId: 'session-1',
    displayName: 'Learner',
    email: 'learner@example.test',
    emailVerified: true,
    mfaEnabled: true,
    authenticatedAt: '2029-12-31T23:50:00Z',
    recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
    authorizationVersion: 1,
    roles: ['Student'],
    permissions: ['Security.ManageOwn'],
    authenticationMethods: ['pwd', 'otp'],
  },
});
