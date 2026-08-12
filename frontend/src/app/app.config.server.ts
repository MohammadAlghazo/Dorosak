import type { ApplicationConfig } from '@angular/core';
import { CSP_NONCE, inject, mergeApplicationConfig, REQUEST_CONTEXT } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: CSP_NONCE,
      useFactory: () => {
        const context = inject(REQUEST_CONTEXT) as { cspNonce?: unknown } | null;
        return typeof context?.cspNonce === 'string' ? context.cspNonce : null;
      },
    },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
