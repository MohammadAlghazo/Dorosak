import { inject } from '@angular/core';
import { type CanMatchFn, Router } from '@angular/router';
import { isLocale } from './locale';
import { LocaleService } from './locale.service';

export const localeGuard: CanMatchFn = (_route, segments) => {
  const locale = segments[0]?.path;
  if (isLocale(locale)) {
    inject(LocaleService).setLocale(locale);
    return true;
  }

  return inject(Router).createUrlTree(['/ar/not-found']);
};
