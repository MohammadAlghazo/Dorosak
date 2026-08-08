import { inject, Injectable } from '@angular/core';
import type { MediaPurpose, UploadMode, UploadSession } from '../../core/api/media-api.types';
import { IndexedDbService } from '../../core/pwa/indexed-db.service';

export interface PersistedMediaPart {
  partNumber: number;
  size: number;
  sha256: string;
  etag: string;
}

export interface PersistedMediaUpload {
  uploadSessionId: string;
  assetId: string;
  purpose: MediaPurpose;
  courseId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  lastModified: number;
  mode: UploadMode;
  partSize: number;
  expiresAt: string;
  completionKey: string;
  cancellationKey: string;
  sha256: string | null;
  completedParts: readonly PersistedMediaPart[];
  uploadCompleted: boolean;
}

@Injectable({ providedIn: 'root' })
export class MediaUploadPersistenceService {
  private readonly indexedDb = inject(IndexedDbService);

  async load(userId: string, courseId: string): Promise<PersistedMediaUpload | null> {
    return this.indexedDb.getUserRecord<PersistedMediaUpload>(key(courseId), userId);
  }

  async save(userId: string, upload: PersistedMediaUpload): Promise<void> {
    // Constructing this record explicitly prevents short-lived signed URLs or local file data
    // from ever crossing the IndexedDB boundary.
    await this.indexedDb.putUserRecord({
      key: key(upload.courseId),
      userId,
      expiresAt: Date.parse(upload.expiresAt),
      value: {
        uploadSessionId: upload.uploadSessionId,
        assetId: upload.assetId,
        purpose: upload.purpose,
        courseId: upload.courseId,
        fileName: upload.fileName,
        contentType: upload.contentType,
        fileSize: upload.fileSize,
        lastModified: upload.lastModified,
        mode: upload.mode,
        partSize: upload.partSize,
        expiresAt: upload.expiresAt,
        completionKey: upload.completionKey,
        cancellationKey: upload.cancellationKey,
        sha256: upload.sha256,
        completedParts: upload.completedParts.map((part) => ({ ...part })),
        uploadCompleted: upload.uploadCompleted,
      } satisfies PersistedMediaUpload,
    });
  }

  async remove(userId: string, courseId: string): Promise<void> {
    const record = await this.indexedDb.getUserRecord<PersistedMediaUpload>(key(courseId), userId);
    if (record) await this.indexedDb.deleteUserRecord(key(courseId));
  }
}

export const persistedUploadFromSession = (
  session: UploadSession,
  request: { purpose: MediaPurpose; courseId: string; file: File },
): PersistedMediaUpload => ({
  uploadSessionId: session.uploadSessionId,
  assetId: session.assetId,
  purpose: request.purpose,
  courseId: request.courseId,
  fileName: request.file.name,
  contentType: request.file.type || 'application/octet-stream',
  fileSize: request.file.size,
  lastModified: request.file.lastModified,
  mode: session.mode,
  partSize: session.partSize,
  expiresAt: session.expiresAt,
  completionKey: globalThis.crypto.randomUUID(),
  cancellationKey: globalThis.crypto.randomUUID(),
  sha256: null,
  completedParts: [],
  uploadCompleted: session.state === 'Completed',
});

const key = (courseId: string): string => `media-upload:${courseId}`;
