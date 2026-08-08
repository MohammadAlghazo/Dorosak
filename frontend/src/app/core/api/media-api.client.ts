import { HttpClient, HttpEventType, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { filter, map, type Observable, switchMap } from 'rxjs';
import { IdentityApiClient } from './identity-api.client';
import type { ApiEnvelope } from './api-envelope';
import type {
  CompleteUploadRequest,
  CreateUploadSessionRequest,
  DownloadGrant,
  DownloadGrantRequest,
  IssueUploadPartRequest,
  MediaStatus,
  MediaUploadEvent,
  UploadPartGrant,
  UploadSession,
} from './media-api.types';
import { XHR_UPLOAD_PROGRESS } from './api-context';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';

@Injectable({ providedIn: 'root' })
export class MediaApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  createSession(
    request: CreateUploadSessionRequest,
    idempotencyKey: string,
  ): Observable<UploadSession> {
    return this.mutation(() =>
      this.http.post<ApiEnvelope<UploadSession>>('uploads', request, {
        context: authenticatedMutationContext(),
        headers: idempotencyHeaders(idempotencyKey),
      }),
    );
  }

  uploadStream(uploadSessionId: string, file: File, sha256: string): Observable<MediaUploadEvent> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.put<ApiEnvelope<UploadSession>>(
          `uploads/${encodeURIComponent(uploadSessionId)}/content`,
          file,
          {
            context: authenticatedMutationContext().set(XHR_UPLOAD_PROGRESS, true),
            headers: new HttpHeaders({
              'Content-Type': file.type || 'application/octet-stream',
              'X-Content-SHA256': sha256,
            }),
            observe: 'events',
            reportProgress: true,
          },
        ),
      ),
      filter(
        (event) =>
          event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
      ),
      map((event): MediaUploadEvent => {
        if (event.type === HttpEventType.UploadProgress) {
          return { kind: 'progress', loaded: event.loaded, total: event.total ?? file.size };
        }
        if (event.body === null) throw new Error('The upload API returned an empty response.');
        return { kind: 'complete', session: event.body.data };
      }),
    );
  }

  issuePart(uploadSessionId: string, request: IssueUploadPartRequest): Observable<UploadPartGrant> {
    return this.mutation(() =>
      this.http.post<ApiEnvelope<UploadPartGrant>>(
        `uploads/${encodeURIComponent(uploadSessionId)}/parts`,
        request,
        { context: authenticatedMutationContext() },
      ),
    );
  }

  complete(
    uploadSessionId: string,
    request: CompleteUploadRequest,
    idempotencyKey: string,
  ): Observable<UploadSession> {
    return this.mutation(() =>
      this.http.post<ApiEnvelope<UploadSession>>(
        `uploads/${encodeURIComponent(uploadSessionId)}/complete`,
        request,
        {
          context: authenticatedMutationContext(),
          headers: idempotencyHeaders(idempotencyKey),
        },
      ),
    );
  }

  cancel(uploadSessionId: string, idempotencyKey: string): Observable<UploadSession> {
    return this.mutation(() =>
      this.http.delete<ApiEnvelope<UploadSession>>(
        `uploads/${encodeURIComponent(uploadSessionId)}`,
        {
          context: authenticatedMutationContext(),
          headers: idempotencyHeaders(idempotencyKey),
        },
      ),
    );
  }

  getStatus(assetId: string): Observable<MediaStatus> {
    return this.http
      .get<ApiEnvelope<MediaStatus>>(`media/${encodeURIComponent(assetId)}/status`, {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  createDownloadGrant(assetId: string, request: DownloadGrantRequest): Observable<DownloadGrant> {
    return this.mutation(() =>
      this.http.post<ApiEnvelope<DownloadGrant>>(
        `media/${encodeURIComponent(assetId)}/download-grant`,
        request,
        { context: authenticatedMutationContext() },
      ),
    );
  }

  private mutation<T>(request: () => Observable<ApiEnvelope<T>>): Observable<T> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(request),
      map((response) => response.data),
    );
  }
}

const idempotencyHeaders = (key: string): HttpHeaders =>
  new HttpHeaders({ 'Idempotency-Key': key });
