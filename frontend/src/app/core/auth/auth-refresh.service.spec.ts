import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { AuthRefreshService } from './auth-refresh.service';
import { SessionStore } from './session.store';

describe('AuthRefreshService', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: {
            value: () => ({ capabilities: { identity: true } }),
            apiUrl: (path: string) => `/api/v1/${path}`,
          },
        },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
    document.cookie = 'XSRF-TOKEN=xsrf%20token; Path=/';
  });

  afterEach(() => {
    controller.verify();
    document.cookie = 'XSRF-TOKEN=; Path=/; Max-Age=0';
  });

  it('sends the refresh cookie mode and XSRF header without bearer credentials', async () => {
    const responsePromise = firstValueFrom(TestBed.inject(AuthRefreshService).refresh());
    const request = controller.expectOne('/api/v1/auth/refresh');

    expect(request.request.method).toBe('POST');
    expect(request.request.credentials).toBe('include');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('xsrf token');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ accessToken: 'refreshed-token' });

    await expect(responsePromise).resolves.toBe('refreshed-token');
    expect(TestBed.inject(SessionStore).accessToken()).toBe('refreshed-token');
  });
});
