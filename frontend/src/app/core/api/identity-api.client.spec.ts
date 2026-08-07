import {
  provideHttpClient,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { apiMetadataInterceptor, bearerInterceptor } from './api.interceptors';
import { IdentityApiClient } from './identity-api.client';
import { RuntimeConfigService } from './runtime-config.service';

describe('IdentityApiClient', () => {
  let client: IdentityApiClient;
  let controller: HttpTestingController;
  let session: SessionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
          withInterceptors([apiMetadataInterceptor, bearerInterceptor]),
        ),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: {
            apiUrl: (path: string) => `/api/v1/${path}`,
          },
        },
      ],
    });
    client = TestBed.inject(IdentityApiClient);
    controller = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    document.cookie = 'XSRF-TOKEN=csrf-token; Path=/';
  });

  afterEach(() => {
    controller.verify();
    document.cookie = 'XSRF-TOKEN=; Path=/; Max-Age=0';
  });

  it('bootstraps CSRF once and unwraps neutral guest responses', async () => {
    const registrationPromise = firstValueFrom(
      client.register({
        displayName: 'Learner',
        email: 'learner@example.test',
        password: 'correct horse battery staple',
      }),
    );
    const csrf = controller.expectOne('/api/v1/auth/csrf');
    expect(csrf.request.headers.has('Authorization')).toBe(false);
    csrf.flush(null);

    const registration = controller.expectOne('/api/v1/auth/register');
    expect(registration.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-token');
    expect(registration.request.headers.has('Authorization')).toBe(false);
    registration.flush({ data: { accepted: true } });
    await expect(registrationPromise).resolves.toEqual({ accepted: true });

    const forgotPromise = firstValueFrom(client.requestPasswordReset('learner@example.test', 'en'));
    const forgot = controller.expectOne('/api/v1/auth/password/forgot');
    forgot.flush({ data: { accepted: true } });
    await expect(forgotPromise).resolves.toEqual({ accepted: true });
  });

  it('binds a new CSRF token to the authenticated session before security mutations', async () => {
    session.establish(authSession());
    client.resetCsrf();

    const setupPromise = firstValueFrom(client.setupMfa());
    const csrf = controller.expectOne('/api/v1/auth/csrf');
    expect(csrf.request.headers.get('Authorization')).toBe('Bearer access-token');
    csrf.flush(null);

    const setup = controller.expectOne('/api/v1/auth/mfa/setup');
    expect(setup.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(setup.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-token');
    setup.flush({ data: { secret: 'SECRET', otpAuthUri: 'otpauth://totp/Dorosak' } });

    await expect(setupPromise).resolves.toEqual({
      secret: 'SECRET',
      otpAuthUri: 'otpauth://totp/Dorosak',
    });
  });
});

const authSession = () => ({
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  identity: {
    userId: 'user-1',
    sessionId: 'session-1',
    displayName: 'Learner',
    email: 'learner@example.test',
    emailVerified: true,
    mfaEnabled: false,
    authenticatedAt: '2029-12-31T23:50:00Z',
    recentAuthenticationExpiresAt: '2030-01-01T00:05:00Z',
    authorizationVersion: 1,
    roles: ['Student'],
    permissions: ['Security.ManageOwn'],
    authenticationMethods: ['pwd'],
  },
});
