import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import {
  catchError,
  finalize,
  retry,
  switchMap,
  throwError,
  TimeoutError,
  timeout,
  timer,
} from 'rxjs';
import { AuthRefreshService } from '../auth/auth-refresh.service';
import { SessionStore } from '../auth/session.store';
import { LocaleService } from '../i18n/locale.service';
import { TelemetryService } from '../telemetry/telemetry.service';
import {
  API_REQUEST,
  DEADLINE_MS,
  PUBLIC_API_REQUEST,
  RETRY_IDEMPOTENT_GET,
  SKIP_AUTH,
  SKIP_REFRESH,
} from './api-context';
import { normalizeApiProblem } from './api-problem';
import { RuntimeConfigService } from './runtime-config.service';

export const apiMetadataInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.context.get(API_REQUEST)) return next(request);
  if (/^https?:\/\//iu.test(request.url) || request.url.startsWith('//')) {
    return throwError(() => new Error('API clients must use a relative allow-listed path.'));
  }

  const locale = inject(LocaleService).locale();
  const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  const correlationId = globalThis.crypto.randomUUID();
  const url = inject(RuntimeConfigService).apiUrl(request.url);
  const credentials = request.context.get(PUBLIC_API_REQUEST)
    ? 'omit'
    : request.credentials;
  return next(
    request.clone({
      url,
      credentials,
      setHeaders: {
        'Accept-Language': locale,
        'X-Client-Timezone': timezone,
        'X-Correlation-ID': correlationId,
      },
    }),
  );
};

export const deadlineInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    timeout({ each: request.context.get(DEADLINE_MS) }),
    catchError((error: unknown) =>
      error instanceof TimeoutError
        ? throwError(
            () =>
              new HttpErrorResponse({
                status: 408,
                statusText: 'Request Timeout',
                url: request.url,
              }),
          )
        : throwError(() => error),
    ),
  );

export const bearerInterceptor: HttpInterceptorFn = (request, next) => {
  if (
    !request.context.get(API_REQUEST) ||
    request.context.get(PUBLIC_API_REQUEST) ||
    request.context.get(SKIP_AUTH)
  ) {
    return next(request);
  }
  const accessToken = inject(SessionStore).accessToken();
  return next(
    accessToken
      ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : request,
  );
};

export const refreshInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionStore);
  const refresh = inject(AuthRefreshService);
  return next(request).pipe(
    catchError((error: unknown) => {
      const canRefresh =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.context.get(API_REQUEST) &&
        !request.context.get(PUBLIC_API_REQUEST) &&
        !request.context.get(SKIP_AUTH) &&
        !request.context.get(SKIP_REFRESH) &&
        session.accessToken() !== null;
      if (!canRefresh) return throwError(() => error);

      return refresh.refresh().pipe(
        switchMap((accessToken) =>
          next(request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })),
        ),
        catchError((refreshError: unknown) => {
          session.clear();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

export const retryInterceptor: HttpInterceptorFn = (request, next) => {
  const mayRetry = request.method === 'GET' && request.context.get(RETRY_IDEMPOTENT_GET);
  return next(request).pipe(
    retry({
      count: mayRetry ? 2 : 0,
      delay: (error: unknown, retryCount) => {
        if (
          !(error instanceof HttpErrorResponse) ||
          ![0, 408, 429, 502, 503, 504].includes(error.status)
        ) {
          throw error;
        }
        return timer(retryDelay(error, retryCount));
      },
    }),
  );
};

export const problemDetailsInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      throwError(() => (error instanceof HttpErrorResponse ? normalizeApiProblem(error) : error)),
    ),
  );

export const telemetryInterceptor: HttpInterceptorFn = (request, next) => {
  const telemetry = inject(TelemetryService);
  const started = performance.now();
  let status = 0;
  return next(request).pipe(
    catchError((error: unknown) => {
      status = error instanceof HttpErrorResponse ? error.status : 0;
      return throwError(() => error);
    }),
    finalize(() => {
      telemetry.recordRequest(request.method, status || 200, performance.now() - started);
    }),
  );
};

const retryDelay = (error: HttpErrorResponse, retryCount: number): number => {
  const retryAfter = error.headers.get('Retry-After');
  if (retryAfter) {
    const seconds = Number(retryAfter);
    if (Number.isFinite(seconds)) return Math.min(seconds * 1000, 30_000);
    const date = Date.parse(retryAfter);
    if (!Number.isNaN(date)) return Math.min(Math.max(date - Date.now(), 0), 30_000);
  }
  return Math.min(250 * 2 ** (retryCount - 1), 2000);
};
