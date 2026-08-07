import { localReturnUrl } from './session.guard';

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
