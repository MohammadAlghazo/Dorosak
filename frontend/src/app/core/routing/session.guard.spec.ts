import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  type RouterStateSnapshot,
} from '@angular/router';
import { SessionStore } from '../auth/session.store';
import { localReturnUrl, permissionGuard } from './session.guard';

describe('localReturnUrl', () => {
  it('keeps only local locale-prefixed non-authentication routes', () => {
    expect(localReturnUrl('/ar/settings/security?tab=mfa#setup', 'ar')).toBe(
      '/ar/settings/security?tab=mfa#setup',
    );
    expect(localReturnUrl('https://attacker.example', 'ar')).toBe('/ar/dashboard');
    expect(localReturnUrl('//attacker.example/path', 'en')).toBe('/en/dashboard');
    expect(localReturnUrl('/ar/auth/sign-in?returnUrl=//attacker.example', 'ar')).toBe(
      '/ar/dashboard',
    );
  });
});

describe('permissionGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    TestBed.inject(SessionStore).establish({
      accessToken: 'access-token',
      accessTokenExpiresAt: '2030-01-01T00:00:00Z',
      identity: {
        userId: 'user-1',
        sessionId: 'session-1',
        displayName: 'Reviewer',
        email: 'reviewer@example.test',
        emailVerified: true,
        mfaEnabled: true,
        authenticatedAt: '2029-12-31T23:55:00Z',
        recentAuthenticationExpiresAt: '2030-01-01T00:10:00Z',
        authorizationVersion: 1,
        roles: ['Admin'],
        permissions: ['TeacherApplication.ReviewAny', 'Course.ReviewAny', 'Catalog.ManageTaxonomy'],
        authenticationMethods: ['pwd', 'otp'],
      },
    });
  });

  it('allows each exact Phase 6 admin permission', () => {
    for (const permission of [
      'TeacherApplication.ReviewAny',
      'Course.ReviewAny',
      'Catalog.ManageTaxonomy',
    ]) {
      const result = TestBed.runInInjectionContext(() =>
        permissionGuard(routeWithPermission(permission), routerState()),
      );
      expect(result).toBe(true);
    }
  });

  it('does not treat another admin permission as equivalent', () => {
    const result = TestBed.runInInjectionContext(() =>
      permissionGuard(routeWithPermission('User.ReadAny'), routerState()),
    );
    expect(result).not.toBe(true);
  });
});

const routeWithPermission = (permission: string): ActivatedRouteSnapshot =>
  ({ data: { permission } }) as unknown as ActivatedRouteSnapshot;

const routerState = (): RouterStateSnapshot => ({ url: '/en/admin' }) as RouterStateSnapshot;
