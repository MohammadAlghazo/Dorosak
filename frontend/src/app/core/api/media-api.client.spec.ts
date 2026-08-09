import { HttpEventType, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, toArray } from 'rxjs';
import { IdentityApiClient } from './identity-api.client';
import { MediaApiClient } from './media-api.client';
import type { MediaStatus, UploadPartGrant, UploadSession } from './media-api.types';
import { XHR_UPLOAD_PROGRESS } from './api-context';

describe('MediaApiClient', () => {
  let client: MediaApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(MediaApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('matches the session, part, completion, cancellation, status, and grant contracts', async () => {
    const createPromise = firstValueFrom(
      client.createSession(
        {
          purpose: 'CourseDocument',
          expectedBytes: 8,
          fileName: 'notes.pdf',
          contentType: 'application/pdf',
          courseId: 'course-1',
        },
        'create-key',
      ),
    );
    const create = http.expectOne('uploads');
    expect(create.request.headers.get('Idempotency-Key')).toBe('create-key');
    expect(create.request.body).toMatchObject({ purpose: 'CourseDocument', expectedBytes: 8 });
    create.flush({ data: uploadSession });
    await expect(createPromise).resolves.toEqual(uploadSession);

    const partPromise = firstValueFrom(
      client.issuePart('upload-1', { partNumber: 1, expectedBytes: 8, sha256: 'a'.repeat(64) }),
    );
    const part = http.expectOne('uploads/upload-1/parts');
    expect(part.request.method).toBe('POST');
    part.flush({ data: partGrant });
    await expect(partPromise).resolves.toEqual(partGrant);

    const completePromise = firstValueFrom(
      client.complete(
        'upload-1',
        {
          totalBytes: 8,
          sha256: 'b'.repeat(64),
          parts: [{ partNumber: 1, size: 8, sha256: 'a'.repeat(64), etag: '"etag-1"' }],
        },
        'complete-key',
      ),
    );
    const complete = http.expectOne('uploads/upload-1/complete');
    expect(complete.request.headers.get('Idempotency-Key')).toBe('complete-key');
    complete.flush({ data: { ...uploadSession, state: 'Completed' } });
    await expect(completePromise).resolves.toMatchObject({ state: 'Completed' });

    const cancelPromise = firstValueFrom(client.cancel('upload-1', 'cancel-key'));
    const cancel = http.expectOne('uploads/upload-1');
    expect(cancel.request.method).toBe('DELETE');
    expect(cancel.request.headers.get('Idempotency-Key')).toBe('cancel-key');
    cancel.flush({ data: { ...uploadSession, state: 'Cancelled' } });
    await expect(cancelPromise).resolves.toMatchObject({ state: 'Cancelled' });

    const statusPromise = firstValueFrom(client.getStatus('asset-1'));
    http.expectOne('media/asset-1/status').flush({ data: mediaStatus });
    await expect(statusPromise).resolves.toEqual(mediaStatus);

    const grantPromise = firstValueFrom(
      client.createDownloadGrant('asset-1', { variantId: null, fileName: 'notes.pdf' }),
    );
    const grant = http.expectOne('media/asset-1/download-grant');
    expect(grant.request.body).toEqual({ variantId: null, fileName: 'notes.pdf' });
    grant.flush({
      data: {
        assetId: 'asset-1',
        variantId: 'variant-1',
        url: 'https://storage.example.test/signed-download',
        expiresAt: '2099-01-01T00:00:00.000Z',
        fileName: 'notes.pdf',
        contentType: 'application/pdf',
      },
    });
    await expect(grantPromise).resolves.toMatchObject({ variantId: 'variant-1' });
  });

  it('requests the XHR transport and exposes streamed upload progress', async () => {
    const eventsPromise = firstValueFrom(
      client
        .uploadStream(
          'upload-1',
          new File([new Uint8Array(8)], 'notes.pdf', { type: 'application/pdf' }),
          'a'.repeat(64),
        )
        .pipe(toArray()),
    );
    const request = http.expectOne('uploads/upload-1/content');
    expect(request.request.context.get(XHR_UPLOAD_PROGRESS)).toBe(true);
    expect(request.request.headers.get('X-Content-SHA256')).toBe('a'.repeat(64));
    request.event({ type: HttpEventType.UploadProgress, loaded: 4, total: 8 });
    request.flush({ data: { ...uploadSession, state: 'Completed' } });

    await expect(eventsPromise).resolves.toEqual([
      { kind: 'progress', loaded: 4, total: 8 },
      { kind: 'complete', session: { ...uploadSession, state: 'Completed' } },
    ]);
  });
});

const uploadSession: UploadSession = {
  uploadSessionId: 'upload-1',
  assetId: 'asset-1',
  state: 'Initiated',
  mode: 'Multipart',
  expectedBytes: 8,
  partSize: 8,
  expiresAt: '2099-01-01T00:00:00.000Z',
};

const partGrant: UploadPartGrant = {
  uploadSessionId: 'upload-1',
  partNumber: 1,
  expectedBytes: 8,
  uploadUrl: 'https://storage.example.test/signed-upload',
  requiredChecksumSha256: 'checksum-base64',
  urlExpiresAt: '2099-01-01T00:00:00.000Z',
};

const mediaStatus: MediaStatus = {
  assetId: 'asset-1',
  purpose: 'CourseDocument',
  state: 'Ready',
  contentType: 'application/pdf',
  declaredBytes: 8,
  verifiedBytes: 8,
  rejectionCode: null,
  variants: [],
  captions: [],
};
