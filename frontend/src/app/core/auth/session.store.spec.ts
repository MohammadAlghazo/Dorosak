import { SessionStore } from './session.store';

describe('SessionStore', () => {
  it('keeps the access token in memory and clears the complete session', () => {
    const store = new SessionStore();

    store.establish('access-token', {
      userId: 'user-1',
      displayName: 'Learner',
      permissions: ['Profile.ReadOwn'],
    });

    expect(store.isAuthenticated()).toBe(true);
    expect(store.accessToken()).toBe('access-token');
    expect(store.identity()?.userId).toBe('user-1');

    store.clear();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken()).toBeNull();
    expect(store.identity()).toBeNull();
  });
});
