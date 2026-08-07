import { SessionStore } from './session.store';
import type { AuthSession } from '../api/identity-api.types';

describe('SessionStore', () => {
  it('keeps the access token in memory and clears the complete session', () => {
    const store = new SessionStore();

    const session: AuthSession = {
      accessToken: 'access-token',
      accessTokenExpiresAt: '2030-01-01T00:00:00Z',
      identity: {
        userId: 'user-1',
        sessionId: 'session-1',
        displayName: 'Learner',
        email: 'learner@example.test',
        emailVerified: true,
        mfaEnabled: false,
        authenticatedAt: '2029-12-31T23:50:00Z',
        recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
        authorizationVersion: 1,
        roles: ['Student'],
        permissions: ['Profile.ReadOwn'],
        authenticationMethods: ['pwd'],
      },
    };
    store.establish(session);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.accessToken()).toBe('access-token');
    expect(store.identity()?.userId).toBe('user-1');

    store.clear();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken()).toBeNull();
    expect(store.identity()).toBeNull();
  });
});
