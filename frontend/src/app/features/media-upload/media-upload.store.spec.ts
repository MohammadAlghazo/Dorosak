import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { DirectStorageHttpClient } from '../../core/api/direct-storage-http.client';
import { MediaApiClient } from '../../core/api/media-api.client';
import type {
  MediaStatus,
  MediaUploadEvent,
  UploadPartGrant,
  UploadSession,
} from '../../core/api/media-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import {
  MediaUploadPersistenceService,
  type PersistedMediaUpload,
} from './media-upload-persistence.service';
import { MediaUploadHasher } from './media-upload-hasher.service';
import { MediaUploadStore } from './media-upload.store';

describe('MediaUploadStore', () => {
  it('moves a stream upload through uploading, scanning, and ready', async () => {
    const api = mediaApi({
      createSession: () => of(session('Stream')),
      uploadStream: vi.fn(() =>
        of(
          { kind: 'progress', loaded: 4, total: 8 } satisfies MediaUploadEvent,
          {
            kind: 'complete',
            session: { ...session('Stream'), state: 'Completed' },
          } satisfies MediaUploadEvent,
        ),
      ),
      getStatus: vi.fn(() => of(readyStatus)),
    });
    const persistence = persistenceApi();
    configure({ api, persistence });
    const store = TestBed.inject(MediaUploadStore);

    await store.selectFile(file('cover.webp', 8, 'image/webp'), 'CourseImage', 'course-1');

    expect(store.state()).toMatchObject({
      status: 'ready',
      assetId: 'asset-1',
      uploadedBytes: 8,
      totalBytes: 8,
    });
    expect(api.uploadStream).toHaveBeenCalledOnce();
    expect(api.getStatus).toHaveBeenCalledWith('asset-1');
    expect(persistence.save).toHaveBeenCalled();
  });

  it('aborts an in-flight signed-storage part before cancelling its session', async () => {
    let aborted = false;
    const directStorage = {
      putPart: vi.fn(
        () =>
          new Observable<never>(() => () => {
            aborted = true;
          }),
      ),
    };
    const api = mediaApi({
      createSession: () => of(session('Multipart')),
      issuePart: () => of(partGrant),
      cancel: vi.fn(() => of({ ...session('Multipart'), state: 'Cancelled' })),
    });
    configure({ api, directStorage });
    const store = TestBed.inject(MediaUploadStore);

    const start = store.selectFile(file('lesson.mp4', 8, 'video/mp4'), 'SourceVideo', 'course-1');
    await vi.waitFor(() => {
      expect(directStorage.putPart).toHaveBeenCalledOnce();
    });

    await store.cancel();
    await start;

    expect(aborted).toBe(true);
    expect(api.cancel).toHaveBeenCalledWith('upload-1', expect.any(String));
    expect(store.state()).toMatchObject({ status: 'cancelled', errorCode: null });
  });

  it('marks expired reload metadata as stale without issuing API requests', async () => {
    const stale: PersistedMediaUpload = {
      ...persistedUpload(),
      expiresAt: '2000-01-01T00:00:00.000Z',
    };
    const api = mediaApi();
    const persistence = persistenceApi({ load: () => Promise.resolve(stale) });
    configure({ api, persistence });
    const store = TestBed.inject(MediaUploadStore);

    await store.restore('course-1');

    expect(store.state()).toMatchObject({ status: 'error', errorCode: 'MEDIA.SESSION_EXPIRED' });
    expect(persistence.remove).toHaveBeenCalledWith('user-1', 'course-1');
    expect(api.getStatus).not.toHaveBeenCalled();
  });

  it('persists completed part metadata but never signed upload URLs or file bytes', async () => {
    const directStorage = {
      putPart: vi.fn(() => of({ kind: 'complete' as const, etag: '"etag-1"' })),
    };
    const api = mediaApi({
      createSession: () => of(session('Multipart')),
      issuePart: () => of(partGrant),
      complete: () => of({ ...session('Multipart'), state: 'Completed' }),
      getStatus: () => of(readyStatus),
    });
    const persistence = persistenceApi();
    configure({ api, persistence, directStorage });
    const store = TestBed.inject(MediaUploadStore);

    await store.selectFile(file('lesson.mp4', 8, 'video/mp4'), 'SourceVideo', 'course-1');

    const saved = JSON.stringify(persistence.save.mock.calls);
    expect(saved).not.toContain(partGrant.uploadUrl);
    expect(saved).not.toContain('data:video');
    expect(saved).toContain('etag-1');
  });
});

const configure = ({
  api = mediaApi(),
  persistence = persistenceApi(),
  directStorage = { putPart: vi.fn(() => of({ kind: 'complete' as const, etag: '"etag-1"' })) },
}: {
  api?: ReturnType<typeof mediaApi>;
  persistence?: ReturnType<typeof persistenceApi>;
  directStorage?: { putPart: ReturnType<typeof vi.fn> };
} = {}): void => {
  TestBed.configureTestingModule({
    providers: [
      MediaUploadStore,
      {
        provide: MediaUploadHasher,
        useValue: {
          hash: vi.fn((_file: File, partSize: number) => ({
            sha256: 'a'.repeat(64),
            parts: partSize > 0 ? [{ partNumber: 1, size: 8, sha256: 'b'.repeat(64) }] : [],
          })),
        },
      },
      { provide: MediaApiClient, useValue: api },
      { provide: DirectStorageHttpClient, useValue: directStorage },
      { provide: MediaUploadPersistenceService, useValue: persistence },
      { provide: ConnectivityStore, useValue: { isOnline: () => true } },
      { provide: SessionStore, useValue: { identity: () => ({ userId: 'user-1' }) } },
    ],
  });
};

const mediaApi = (overrides: Partial<Record<string, unknown>> = {}) => ({
  createSession: vi.fn(() => of(session('Stream'))),
  uploadStream: vi.fn(() =>
    of({
      kind: 'complete',
      session: { ...session('Stream'), state: 'Completed' },
    } satisfies MediaUploadEvent),
  ),
  issuePart: vi.fn(() => of(partGrant)),
  complete: vi.fn(() => of({ ...session('Multipart'), state: 'Completed' })),
  cancel: vi.fn(() => of({ ...session('Multipart'), state: 'Cancelled' })),
  getStatus: vi.fn(() => of(readyStatus)),
  ...overrides,
});

const persistenceApi = (overrides: Partial<Record<string, unknown>> = {}) => ({
  load: vi.fn(() => Promise.resolve(null)),
  save: vi.fn(() => Promise.resolve()),
  remove: vi.fn(() => Promise.resolve()),
  ...overrides,
});

const session = (mode: 'Stream' | 'Multipart'): UploadSession => ({
  uploadSessionId: 'upload-1',
  assetId: 'asset-1',
  state: 'Initiated',
  mode,
  expectedBytes: 8,
  partSize: mode === 'Multipart' ? 8 : 0,
  expiresAt: '2099-01-01T00:00:00.000Z',
});

const partGrant: UploadPartGrant = {
  uploadSessionId: 'upload-1',
  partNumber: 1,
  expectedBytes: 8,
  uploadUrl: 'https://storage.example.test/private-presigned-upload-url',
  requiredChecksumSha256: 'YmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI=',
  urlExpiresAt: '2099-01-01T00:00:00.000Z',
};

const readyStatus: MediaStatus = {
  assetId: 'asset-1',
  purpose: 'CourseImage',
  state: 'Ready',
  contentType: 'image/webp',
  declaredBytes: 8,
  verifiedBytes: 8,
  rejectionCode: null,
  variants: [],
  captions: [],
};

const persistedUpload = (): PersistedMediaUpload => ({
  uploadSessionId: 'upload-1',
  assetId: 'asset-1',
  purpose: 'CourseImage',
  courseId: 'course-1',
  fileName: 'cover.webp',
  contentType: 'image/webp',
  fileSize: 8,
  lastModified: 1,
  mode: 'Stream',
  partSize: 0,
  expiresAt: '2099-01-01T00:00:00.000Z',
  completionKey: 'complete-key',
  cancellationKey: 'cancel-key',
  sha256: null,
  completedParts: [],
  uploadCompleted: false,
});

const file = (name: string, size: number, type: string): File =>
  new File([new Uint8Array(size)], name, { type, lastModified: 1 });
