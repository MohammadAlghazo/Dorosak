import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { SessionCoordinator } from '../auth/session-coordinator.service';
import { LocaleService } from '../i18n/locale.service';
import { SessionStore } from '../auth/session.store';

export const sessionGuard: CanActivateFn = (_route, state) => {
  const coordinator = inject(SessionCoordinator);
  const locale = inject(LocaleService);
  const router = inject(Router);
  return coordinator.ensureAuthenticated().pipe(
    map((authenticated) =>
      authenticated
        ? true
        : router.createUrlTree([locale.locale(), 'auth', 'sign-in'], {
            queryParams: { returnUrl: localReturnUrl(state.url, locale.locale()) },
          }),
    ),
  );
};

export const permissionGuard: CanActivateFn = (route) => {
  const required: unknown = route.data['permission'];
  if (typeof required === 'string' && inject(SessionStore).hasPermission(required)) return true;
  return inject(Router).createUrlTree([inject(LocaleService).locale(), 'not-found']);
};

export const localReturnUrl = (value: string | null, locale: 'ar' | 'en'): string => {
  const fallback = `/${locale}/dashboard`;
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
    return fallback;
  }
  try {
    const parsed = new URL(value, 'https://dorosak.local');
    if (
      parsed.origin !== 'https://dorosak.local' ||
      !/^\/(?:ar|en)(?:\/|$)/u.test(parsed.pathname) ||
      /^\/(?:ar|en)\/auth(?:\/|$)/u.test(parsed.pathname)
    ) {
      return fallback;
    }
    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return fallback;
  }
};
