import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { LocaleService } from '../i18n/locale.service';
import { SessionStore } from '../auth/session.store';

export const sessionGuard: CanActivateFn = (_route, state) => {
  if (inject(SessionStore).isAuthenticated()) return true;
  return inject(Router).createUrlTree([inject(LocaleService).locale(), 'auth', 'sign-in'], {
    queryParams: { returnUrl: state.url },
  });
};

export const permissionGuard: CanActivateFn = (route) => {
  const required = route.data['permission'];
  const identity = inject(SessionStore).identity();
  if (typeof required === 'string' && identity?.permissions.includes(required)) return true;
  return inject(Router).createUrlTree([inject(LocaleService).locale(), 'not-found']);
};
