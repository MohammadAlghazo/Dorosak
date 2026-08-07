import {
  provideHttpClient,
  withFetch,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import type { ApplicationConfig } from '@angular/core';
import {
  ErrorHandler,
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  provideClientHydration,
  withEventReplay,
  withHttpTransferCacheOptions,
} from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import {
  apiMetadataInterceptor,
  bearerInterceptor,
  deadlineInterceptor,
  problemDetailsInterceptor,
  refreshInterceptor,
  retryInterceptor,
  telemetryInterceptor,
} from './core/api/api.interceptors';
import { RuntimeConfigService } from './core/api/runtime-config.service';
import { GlobalErrorHandler } from './core/error-handling/global-error-handler';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }),
    ),
    provideHttpClient(
      withFetch(),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
      withInterceptors([
        apiMetadataInterceptor,
        problemDetailsInterceptor,
        telemetryInterceptor,
        deadlineInterceptor,
        bearerInterceptor,
        refreshInterceptor,
        retryInterceptor,
      ]),
    ),
    provideClientHydration(
      withEventReplay(),
      withHttpTransferCacheOptions({
        includePostRequests: false,
        includeRequestsWithAuthHeaders: false,
        filter: (request) =>
          request.method === 'GET' &&
          !request.withCredentials &&
          request.credentials === 'omit' &&
          !request.headers.has('Authorization') &&
          /^\/api\/v1\/(?:system\/status|catalog\/(?:courses(?:\/[^/?]+)?|categories|tags|featured|popular)|search(?:\/suggestions)?|pages(?:\/[^/?]+)?|faqs)(?:\?|$)/u.test(
            request.url,
          ),
      }),
    ),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    provideAppInitializer(() => inject(RuntimeConfigService).load()),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
  ],
};
