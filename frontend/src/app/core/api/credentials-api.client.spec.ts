import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { CredentialsApiClient } from './credentials-api.client';

describe('CredentialsApiClient', () => {
  let client: CredentialsApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    client = TestBed.inject(CredentialsApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('uses the public verification endpoint without exposing internal identifiers', async () => {
    const promise = firstValueFrom(client.verifyCertificate('verify_code'));
    const request = http.expectOne('certificates/verify/verify_code');
    expect(request.request.method).toBe('GET');
    request.flush({
      data: {
        learnerName: 'Demo Learner',
        courseTitle: 'Demo Course',
        locale: 'en',
        completedAt: '2026-08-11T00:00:00Z',
        issuedAt: '2026-08-12T00:00:00Z',
        verificationCode: 'verify_code',
        status: 'Active',
        revokedAt: null,
      },
    });

    const certificate = await promise;
    expect(certificate).not.toHaveProperty('id');
    expect(certificate).not.toHaveProperty('learnerUserId');
    expect(certificate).not.toHaveProperty('email');
  });
});
