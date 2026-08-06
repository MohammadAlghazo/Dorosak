import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { LocaleService } from '../i18n/locale.service';
import { API_REQUEST } from './api-context';
import {
  apiMetadataInterceptor,
  bearerInterceptor,
  problemDetailsInterceptor,
} from './api.interceptors';

describe('API interceptors', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(
          withInterceptors([apiMetadataInterceptor, bearerInterceptor, problemDetailsInterceptor]),
        ),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    TestBed.inject(LocaleService).setLocale('en');
    TestBed.inject(SessionStore).establish('memory-token');
  });

  afterEach(() => {
    controller.verify();
  });

  it('prefixes allow-listed API requests and adds locale, timezone, correlation, and bearer metadata', async () => {
    const responsePromise = firstValueFrom(
      http.get<{ ok: boolean }>('system/status', {
        context: new HttpContext().set(API_REQUEST, true),
      }),
    );
    const request = controller.expectOne('/api/v1/system/status');

    expect(request.request.headers.get('Accept-Language')).toBe('en');
    expect(request.request.headers.get('X-Client-Timezone')).toBeTruthy();
    expect(request.request.headers.get('X-Correlation-ID')).toBeTruthy();
    expect(request.request.headers.get('Authorization')).toBe('Bearer memory-token');
    request.flush({ ok: true });

    await expect(responsePromise).resolves.toEqual({ ok: true });
  });

  it('normalizes API failures into ApiProblem', async () => {
    const responsePromise = firstValueFrom(
      http.get('system/status', { context: new HttpContext().set(API_REQUEST, true) }),
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
