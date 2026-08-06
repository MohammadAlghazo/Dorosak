import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express, { type NextFunction, type Request, type Response } from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');
const app = express();
const angularApp = new AngularNodeAppEngine();
const internalApiOrigin = validatedOrigin(
  process.env['DOROSAK_INTERNAL_API_ORIGIN'] ?? 'http://127.0.0.1:5053',
);
const publicApiPath = validatedApiPath(process.env['DOROSAK_PUBLIC_API_PATH'] ?? '/api/v1');
const allowedHosts = new Set(
  (process.env['DOROSAK_ALLOWED_HOSTS'] ?? 'localhost,127.0.0.1')
    .split(',')
    .map((host) => host.trim().toLowerCase())
    .filter(Boolean),
);

app.disable('x-powered-by');
app.set('trust proxy', false);

app.use((request, response, next) => {
  if (!allowedHosts.has(request.hostname.toLowerCase())) {
    response.status(421).json({ status: 421, code: 'HTTP.MISDIRECTED_REQUEST' });
    return;
  }

  response.setHeader(
    'Content-Security-Policy',
    [
      "default-src 'self'",
      "base-uri 'self'",
      "connect-src 'self'",
      "font-src 'self'",
      "form-action 'self'",
      "frame-ancestors 'none'",
      "img-src 'self' data:",
      "manifest-src 'self'",
      "object-src 'none'",
      "script-src 'self'",
      "style-src 'self'",
      "worker-src 'self'",
    ].join('; '),
  );
  response.setHeader('Cross-Origin-Resource-Policy', 'same-origin');
  response.setHeader('Permissions-Policy', 'camera=(), geolocation=(), microphone=(), payment=()');
  response.setHeader('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.setHeader('X-Content-Type-Options', 'nosniff');
  response.setHeader('X-Frame-Options', 'DENY');
  if (process.env['NODE_ENV'] === 'production') {
    response.setHeader('Strict-Transport-Security', 'max-age=31536000; includeSubDomains');
  }
  next();
});

app.get('/health', (_request, response) => {
  response.setHeader('Cache-Control', 'no-store');
  response.json({ status: 'healthy' });
});

app.get('/runtime-config.json', (_request, response) => {
  response.setHeader('Cache-Control', 'no-store');
  response.json({
    apiBasePath: publicApiPath,
    release: process.env['DOROSAK_RELEASE'] ?? 'development',
    defaultLocale: 'ar',
    supportedLocales: ['ar', 'en'],
    capabilities: {
      identity: process.env['DOROSAK_CAPABILITY_IDENTITY'] === 'true',
      learning: process.env['DOROSAK_CAPABILITY_LEARNING'] === 'true',
      offline: true,
    },
  });
});

app.use(
  createProxyMiddleware<Request, Response>({
    pathFilter: '/api/**',
    target: internalApiOrigin.origin,
    changeOrigin: false,
    proxyTimeout: 30_000,
    timeout: 35_000,
    ws: false,
    on: {
      error: (_error, _request, response) => {
        if ('writeHead' in response && !response.headersSent) {
          response.writeHead(502, { 'Content-Type': 'application/problem+json' });
        }
        response.end(JSON.stringify({ status: 502, code: 'PROXY.UPSTREAM_UNAVAILABLE' }));
      },
    },
  }),
);

app.use(
  express.static(browserDistFolder, {
    immutable: true,
    index: false,
    maxAge: '1y',
    redirect: false,
    setHeaders: (response, path) => {
      if (/(?:index\.html|manifest\.webmanifest|ngsw\.json|theme-init\.js)$/u.test(path)) {
        response.setHeader('Cache-Control', 'no-cache');
      }
    },
  }),
);

app.use((request, response, next) => {
  response.setHeader('Vary', 'Accept-Encoding');
  response.setHeader(
    'Cache-Control',
    isPrivateRoute(request.path)
      ? 'no-store'
      : 'public, max-age=0, s-maxage=60, stale-while-revalidate=300',
  );
  angularApp
    .handle(request)
    .then((angularResponse) =>
      angularResponse ? writeResponseToNodeResponse(angularResponse, response) : next(),
    )
    .catch(next);
});

app.use((_error: unknown, _request: Request, response: Response, _next: NextFunction) => {
  if (!response.headersSent) {
    response.status(500).type('application/problem+json').send({
      status: 500,
      code: 'WEB.UNEXPECTED',
      title: 'The web application could not render this request.',
    });
  }
});

if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = validatedPort(process.env['PORT'] ?? '4000');
  const server = app.listen(port, '0.0.0.0', (error) => {
    if (error) throw error;
    console.log(`Dorosak Web listening on http://0.0.0.0:${port}`);
  });

  const shutdown = () => {
    server.close((error) => {
      process.exitCode = error ? 1 : 0;
    });
  };
  process.once('SIGINT', shutdown);
  process.once('SIGTERM', shutdown);
}

export const reqHandler = createNodeRequestHandler(app);

function validatedOrigin(value: string): URL {
  const origin = new URL(value);
  if (!['http:', 'https:'].includes(origin.protocol) || origin.pathname !== '/') {
    throw new Error('DOROSAK_INTERNAL_API_ORIGIN must be an HTTP(S) origin without a path.');
  }
  return origin;
}

function validatedApiPath(value: string): string {
  if (!/^\/api\/v\d+$/u.test(value)) {
    throw new Error('DOROSAK_PUBLIC_API_PATH must be a relative versioned API path.');
  }
  return value;
}

function validatedPort(value: string): number {
  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > 65_535) throw new Error('PORT is invalid.');
  return port;
}

function isPrivateRoute(path: string): boolean {
  return /^\/(?:ar|en)\/(?:auth|dashboard|learn|instructor|admin)(?:\/|$)/u.test(path);
}
