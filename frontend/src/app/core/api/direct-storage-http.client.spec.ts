import { TestBed } from '@angular/core/testing';
import { firstValueFrom, toArray } from 'rxjs';
import { DirectStorageHttpClient } from './direct-storage-http.client';

describe('DirectStorageHttpClient', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends signed storage uploads without API, bearer, CSRF, or credential metadata', async () => {
    vi.stubGlobal('XMLHttpRequest', FakeXmlHttpRequest);
    TestBed.configureTestingModule({});
    const client = TestBed.inject(DirectStorageHttpClient);

    const events = await firstValueFrom(
      client
        .putPart(
          'https://storage.example.test/bucket/key?signature=secret',
          new Blob([new Uint8Array(8)]),
          'checksum-base64',
        )
        .pipe(toArray()),
    );

    const request = FakeXmlHttpRequest.latest;
    expect(request).toBeDefined();
    expect(request?.method).toBe('PUT');
    expect(request?.withCredentials).toBe(false);
    expect(Object.fromEntries(request?.headers ?? [])).toEqual({
      'x-amz-checksum-sha256': 'checksum-base64',
    });
    expect(request?.headers.has('Authorization')).toBe(false);
    expect(request?.headers.has('X-XSRF-TOKEN')).toBe(false);
    expect(request?.headers.has('Accept-Language')).toBe(false);
    expect(events).toEqual([
      { kind: 'progress', loaded: 4, total: 8 },
      { kind: 'complete', etag: '"etag-1"' },
    ]);
  });
});

class FakeXmlHttpRequest {
  static latest: FakeXmlHttpRequest | undefined;

  readonly headers = new Map<string, string>();
  readonly upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null };
  withCredentials = true;
  status = 200;
  method = '';
  url = '';
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;
  onload: (() => void) | null = null;

  constructor() {
    FakeXmlHttpRequest.latest = this;
  }

  open(method: string, url: string): void {
    this.method = method;
    this.url = url;
  }

  setRequestHeader(name: string, value: string): void {
    this.headers.set(name, value);
  }

  getResponseHeader(name: string): string | null {
    return name === 'ETag' ? '"etag-1"' : null;
  }

  send(): void {
    this.upload.onprogress?.({ lengthComputable: true, loaded: 4, total: 8 } as ProgressEvent);
    this.onload?.();
  }

  abort(): void {
    this.onabort?.();
  }
}
