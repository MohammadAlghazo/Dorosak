import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Observable } from 'rxjs';

export type DirectStorageUploadEvent =
  { kind: 'progress'; loaded: number; total: number } | { kind: 'complete'; etag: string };

export class DirectStorageUploadError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'DirectStorageUploadError';
  }
}

/**
 * Signed object-storage requests deliberately bypass Angular's API pipeline. Signed URLs are
 * credentials and must not receive bearer, locale, CSRF, refresh, retry, or telemetry headers.
 */
@Injectable({ providedIn: 'root' })
export class DirectStorageHttpClient {
  private readonly platformId = inject(PLATFORM_ID);

  putPart(
    uploadUrl: string,
    content: Blob,
    checksumSha256: string,
  ): Observable<DirectStorageUploadEvent> {
    return new Observable((subscriber) => {
      if (!isPlatformBrowser(this.platformId)) {
        subscriber.error(
          new DirectStorageUploadError(
            'MEDIA.BROWSER_REQUIRED',
            'Direct storage requires a browser.',
          ),
        );
        return undefined;
      }
      if (!isAbsoluteHttpUrl(uploadUrl)) {
        subscriber.error(
          new DirectStorageUploadError(
            'MEDIA.INVALID_UPLOAD_URL',
            'The storage upload URL is invalid.',
          ),
        );
        return undefined;
      }

      const request = new XMLHttpRequest();
      request.open('PUT', uploadUrl, true);
      request.withCredentials = false;
      request.setRequestHeader('x-amz-checksum-sha256', checksumSha256);
      request.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          subscriber.next({ kind: 'progress', loaded: event.loaded, total: event.total });
        }
      };
      request.onerror = () => {
        subscriber.error(
          new DirectStorageUploadError('MEDIA.STORAGE_UPLOAD_FAILED', 'Storage upload failed.'),
        );
      };
      request.onabort = () => {
        subscriber.error(
          new DirectStorageUploadError('MEDIA.UPLOAD_ABORTED', 'Storage upload was aborted.'),
        );
      };
      request.onload = () => {
        if (request.status < 200 || request.status >= 300) {
          subscriber.error(
            new DirectStorageUploadError(
              'MEDIA.STORAGE_UPLOAD_FAILED',
              `Storage upload failed with HTTP ${String(request.status)}.`,
            ),
          );
          return;
        }
        const etag = request.getResponseHeader('ETag');
        if (!etag) {
          subscriber.error(
            new DirectStorageUploadError(
              'MEDIA.ETAG_MISSING',
              'Storage did not expose the uploaded part ETag.',
            ),
          );
          return;
        }
        subscriber.next({ kind: 'complete', etag });
        subscriber.complete();
      };
      request.send(content);

      return () => {
        request.abort();
      };
    });
  }
}

const isAbsoluteHttpUrl = (value: string): boolean => {
  try {
    const url = new URL(value);
    return url.protocol === 'https:' || url.protocol === 'http:';
  } catch {
    return false;
  }
};
