import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
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
import {
  apiMetadataInterceptor,
  bearerInterceptor,
  deadlineInterceptor,
  problemDetailsInterceptor,
  refreshInterceptor,
  retryInterceptor,
  telemetryInterceptor,
} from './api.interceptors';
import { SystemApiClient } from './system-api.client';

const refresh = vi.fn(() => of('refreshed-token'));
const recordRequest = vi.fn();

describe('API interceptors', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    refresh.mockClear();
    recordRequest.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(
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
        provideHttpClientTesting(),
        { provide: AuthRefreshService, useValue: { refresh } },
        { provide: TelemetryService, useValue: { recordRequest } },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    TestBed.inject(LocaleService).setLocale('en');
    TestBed.inject(SessionStore).establish({
      accessToken: 'memory-token',
      accessTokenExpiresAt: '2030-01-01T00:00:00Z',
      identity: {
        userId: 'user-1',
        sessionId: 'session-1',
        displayName: 'Test user',
        email: 'test@example.test',
        emailVerified: true,
        mfaEnabled: false,
        authenticatedAt: '2029-12-31T23:50:00Z',
        recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
        authorizationVersion: 1,
        roles: ['Student'],
        permissions: ['Profile.ReadOwn'],
        authenticationMethods: ['pwd'],
      },
    });
  });

  afterEach(() => {
    controller.verify();
    vi.useRealTimers();
  });

  it('adds metadata and bearer credentials to private same-origin API requests', async () => {
    const responsePromise = firstValueFrom(
      http.get<{ ok: boolean }>('private/profile', {
        context: new HttpContext().set(API_REQUEST, true),
      }),
    );
    const request = controller.expectOne('/api/v1/private/profile');

    expect(request.request.credentials).toBeUndefined();
    expect(request.request.headers.get('Accept-Language')).toBe('en');
    expect(request.request.headers.get('X-Client-Timezone')).toBeTruthy();
    expect(request.request.headers.get('X-Correlation-ID')).toBeTruthy();
    expect(request.request.headers.get('Authorization')).toBe('Bearer memory-token');
    request.flush({ ok: true });

    await expect(responsePromise).resolves.toEqual({ ok: true });
  });

  it('keeps public API reads credential-free and transfer-cache eligible', async () => {
    const responsePromise = firstValueFrom(TestBed.inject(SystemApiClient).getStatus());
    const request = controller.expectOne('/api/v1/system/status');

    expect(request.request.context.get(PUBLIC_API_REQUEST)).toBe(true);
    expect(request.request.credentials).toBe('omit');
    expect(request.request.withCredentials).toBe(false);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({
      data: { service: 'Dorosak.Api', version: 'Test', utcTime: '2026-08-06T00:00:00Z' },
    });

    await expect(responsePromise).resolves.toEqual({ service: 'Dorosak.Api', available: true });
  });

  it('keeps anonymous auth requests cookie-enabled without adding bearer credentials', async () => {
    const responsePromise = firstValueFrom(
      http.post<{ ok: boolean }>('auth/sign-in', {}, {
        context: new HttpContext()
          .set(API_REQUEST, true)
          .set(SKIP_AUTH, true)
          .set(SKIP_REFRESH, true),
      }),
    );
    const request = controller.expectOne('/api/v1/auth/sign-in');

    expect(request.request.credentials).toBeUndefined();
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ ok: true });

    await expect(responsePromise).resolves.toEqual({ ok: true });
  });

  it('refreshes a private 401 before normalizing the response error', async () => {
    const responsePromise = firstValueFrom(
      http.get<{ ok: boolean }>('private/profile', {
        context: new HttpContext().set(API_REQUEST, true),
      }),
    );
    const initialRequest = controller.expectOne('/api/v1/private/profile');

    initialRequest.flush(
      { code: 'AUTH.UNAUTHORIZED', detail: 'Expired access token.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(refresh).toHaveBeenCalledOnce();
    const retriedRequest = controller.expectOne('/api/v1/private/profile');
    expect(retriedRequest.request.headers.get('Authorization')).toBe('Bearer refreshed-token');
    retriedRequest.flush({ ok: true });

    await expect(responsePromise).resolves.toEqual({ ok: true });
  });

  it('retries an idempotent GET before ProblemDetails normalization', async () => {
    vi.useFakeTimers();
    const responsePromise = firstValueFrom(
      http.get<{ ok: boolean }>('private/profile', {
        context: new HttpContext().set(API_REQUEST, true),
      }),
    );
    const initialRequest = controller.expectOne('/api/v1/private/profile');

    initialRequest.flush(
      { code: 'SYSTEM.UNAVAILABLE', detail: 'Unavailable' },
      {
        status: 503,
        statusText: 'Service Unavailable',
        headers: { 'Retry-After': '0' },
      },
    );
    await vi.advanceTimersByTimeAsync(0);

    const retriedRequest = controller.expectOne('/api/v1/private/profile');
    retriedRequest.flush({ ok: true });

    await expect(responsePromise).resolves.toEqual({ ok: true });
  });

  it('normalizes response deadlines into ApiProblem', async () => {
    vi.useFakeTimers();
    const responsePromise = firstValueFrom(
      http.get('private/profile', {
        context: new HttpContext()
          .set(API_REQUEST, true)
          .set(DEADLINE_MS, 25)
          .set(RETRY_IDEMPOTENT_GET, false),
      }),
    );
    const errorPromise = responsePromise.catch((error: unknown) => error);
    const request = controller.expectOne('/api/v1/private/profile');

    await vi.advanceTimersByTimeAsync(25);

    await expect(errorPromise).resolves.toMatchObject({
      name: 'ApiProblem',
      code: 'HTTP.408',
      status: 408,
    });
    expect(request.cancelled).toBe(true);
    expect(recordRequest).toHaveBeenCalledWith('GET', 408, expect.any(Number));
  });

  it('normalizes final API failures into ApiProblem', async () => {
    const responsePromise = firstValueFrom(
      http.get('system/status', {
        context: new HttpContext()
          .set(API_REQUEST, true)
          .set(RETRY_IDEMPOTENT_GET, false),
      }),
    );
    controller
      .expectOne('/api/v1/system/status')
      .flush(
        { code: 'SYSTEM.UNAVAILABLE', detail: 'Unavailable', traceId: 'trace-1' },
        { status: 503, statusText: 'Service Unavailable' },
      );

    await expect(responsePromise).rejects.toMatchObject({
      code: 'SYSTEM.UNAVAILABLE',
      status: 503,
      traceId: 'trace-1',
    });
  });
});
