import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import type { Observable, Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import {
  DirectStorageHttpClient,
  DirectStorageUploadError,
  type DirectStorageUploadEvent,
} from '../../core/api/direct-storage-http.client';
import { MediaApiClient } from '../../core/api/media-api.client';
import type {
  MediaPurpose,
  MediaStatus,
  UploadPartGrant,
  UploadSession,
} from '../../core/api/media-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import {
  MediaUploadPersistenceService,
  persistedUploadFromSession,
  type PersistedMediaPart,
  type PersistedMediaUpload,
} from './media-upload-persistence.service';
import type { MediaFileHashes, MediaFilePartHash } from './media-upload-hasher.service';
import { MediaUploadHasher } from './media-upload-hasher.service';

export type MediaUploadStatus =
  | 'idle'
  | 'validating'
  | 'uploading'
  | 'paused'
  | 'finalizing'
  | 'scanning'
  | 'processing'
  | 'ready'
  | 'rejected'
  | 'cancelled'
  | 'error'
  | 'offline';

export interface MediaUploadPartProgress {
  partNumber: number;
  size: number;
  loaded: number;
  state: 'pending' | 'uploading' | 'complete' | 'error';
}

export interface MediaUploadState {
  status: MediaUploadStatus;
  assetId: string | null;
  fileName: string | null;
  purpose: MediaPurpose | null;
  totalBytes: number;
  uploadedBytes: number;
  parts: readonly MediaUploadPartProgress[];
  errorCode: string | null;
  needsFile: boolean;
}

interface PendingStart {
  file: File;
  purpose: MediaPurpose;
  courseId: string;
  idempotencyKey: string;
}

interface ActiveSignedPart {
  part: MediaFilePartHash;
  grant: UploadPartGrant;
}

interface ActiveRequest {
  cancel: () => void;
}

const initialState: MediaUploadState = {
  status: 'idle',
  assetId: null,
  fileName: null,
  purpose: null,
  totalBytes: 0,
  uploadedBytes: 0,
  parts: [],
  errorCode: null,
  needsFile: false,
};

@Injectable()
export class MediaUploadStore {
  private readonly api = inject(MediaApiClient);
  private readonly directStorage = inject(DirectStorageHttpClient);
  private readonly hasher = inject(MediaUploadHasher);
  private readonly persistence = inject(MediaUploadPersistenceService);
  private readonly session = inject(SessionStore);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly uploadState = signal<MediaUploadState>(initialState);
  private upload: PersistedMediaUpload | null = null;
  private currentFile: File | null = null;
  private currentHashes: MediaFileHashes | null = null;
  private pendingStart: PendingStart | null = null;
  private activeSignedPart: ActiveSignedPart | null = null;
  private activeRequest: ActiveRequest | null = null;
  private hashingAbort: AbortController | null = null;
  private pauseRequested = false;
  private operation = 0;
  private courseId: string | null = null;

  readonly state = this.uploadState.asReadonly();
  readonly progressPercent = computed(() => {
    const { totalBytes, uploadedBytes } = this.uploadState();
    return totalBytes === 0 ? 0 : Math.min(100, Math.round((uploadedBytes / totalBytes) * 100));
  });
  readonly canPause = computed(() => this.uploadState().status === 'uploading');
  readonly canResume = computed(() => {
    const status = this.uploadState().status;
    return status === 'paused' || status === 'offline' || status === 'error';
  });

  constructor() {
    effect(() => {
      if (this.connectivity.isOnline()) {
        if (this.uploadState().status === 'offline') {
          this.patchState({ status: 'paused', errorCode: null });
        }
        return;
      }
      if (isInterruptible(this.uploadState().status)) this.pauseForOffline();
    });
    this.destroyRef.onDestroy(() => {
      this.operation++;
      this.abortActiveWork();
    });
  }

  async restore(courseId: string): Promise<void> {
    this.courseId = courseId;
    const userId = this.userId();
    if (!userId) return;
    const upload = await this.persistence.load(userId, courseId);
    if (!upload) return;
    if (!isFuture(upload.expiresAt)) {
      await this.persistence.remove(userId, courseId);
      this.patchState({
        status: 'error',
        assetId: upload.assetId,
        fileName: upload.fileName,
        purpose: upload.purpose,
        totalBytes: upload.fileSize,
        uploadedBytes: 0,
        errorCode: 'MEDIA.SESSION_EXPIRED',
      });
      return;
    }

    this.upload = upload;
    this.currentHashes = null;
    this.currentFile = null;
    const uploadedBytes = sumCompletedBytes(upload.completedParts);
    this.patchState({
      status: upload.uploadCompleted ? 'scanning' : 'paused',
      assetId: upload.assetId,
      fileName: upload.fileName,
      purpose: upload.purpose,
      totalBytes: upload.fileSize,
      uploadedBytes: upload.uploadCompleted ? upload.fileSize : uploadedBytes,
      parts: upload.completedParts.map((part) => ({
        partNumber: part.partNumber,
        size: part.size,
        loaded: part.size,
        state: 'complete',
      })),
      errorCode: null,
      needsFile: !upload.uploadCompleted,
    });
    if (upload.uploadCompleted) void this.pollStatus(++this.operation);
  }

  async selectFile(file: File, purpose: MediaPurpose, courseId: string): Promise<void> {
    this.courseId = courseId;
    if (this.upload && !this.upload.uploadCompleted) {
      if (!sameFile(file, this.upload)) {
        this.patchState({ status: 'error', errorCode: 'MEDIA.ACTIVE_UPLOAD' });
        return;
      }
      this.currentFile = file;
      this.currentHashes = null;
      await this.resume();
      return;
    }

    const hintError = mediaHintError(file, purpose);
    if (hintError) {
      this.upload = null;
      this.currentFile = file;
      this.patchState({
        status: 'error',
        fileName: file.name,
        purpose,
        totalBytes: file.size,
        errorCode: hintError,
      });
      return;
    }
    this.currentFile = file;
    this.currentHashes = null;
    this.pendingStart = {
      file,
      purpose,
      courseId,
      idempotencyKey: globalThis.crypto.randomUUID(),
    };
    await this.createAndUpload(++this.operation);
  }

  pause(): void {
    if (this.uploadState().status !== 'uploading') return;
    this.pauseRequested = true;
    this.patchState({ status: 'paused', errorCode: null });
  }

  async resume(): Promise<void> {
    if (!this.connectivity.isOnline()) {
      this.patchState({ status: 'offline', errorCode: 'MEDIA.OFFLINE' });
      return;
    }
    if (this.upload?.uploadCompleted) {
      await this.pollStatus(++this.operation);
      return;
    }
    if (!this.upload && this.pendingStart) {
      await this.createAndUpload(++this.operation);
      return;
    }
    if (!this.upload || !this.currentFile) {
      this.patchState({
        status: 'paused',
        needsFile: true,
        errorCode: 'MEDIA.FILE_RESELECT_REQUIRED',
      });
      return;
    }
    this.pauseRequested = false;
    await this.prepareAndUpload(++this.operation);
  }

  async retry(): Promise<void> {
    await this.resume();
  }

  async cancel(): Promise<void> {
    const upload = this.upload;
    this.operation++;
    this.pauseRequested = true;
    this.abortActiveWork();
    if (!upload) {
      this.patchState({ status: 'cancelled', errorCode: null, needsFile: false });
      return;
    }
    if (!this.connectivity.isOnline()) {
      this.patchState({ status: 'offline', errorCode: 'MEDIA.CANCEL_REQUIRES_ONLINE' });
      return;
    }
    try {
      const session = await this.requestValue(
        this.api.cancel(upload.uploadSessionId, upload.cancellationKey),
      );
      if (session.state === 'Completed') {
        this.upload = { ...upload, uploadCompleted: true };
        await this.save();
        await this.pollStatus(++this.operation);
        return;
      }
      await this.removePersisted();
      this.resetRuntime();
      this.patchState({ status: 'cancelled', errorCode: null, needsFile: false });
    } catch (error: unknown) {
      this.fail(error);
    }
  }

  private async createAndUpload(operation: number): Promise<void> {
    const pending = this.pendingStart;
    if (!pending || !this.isCurrent(operation)) return;
    if (!this.connectivity.isOnline()) {
      this.patchState({
        status: 'offline',
        fileName: pending.file.name,
        purpose: pending.purpose,
        totalBytes: pending.file.size,
        errorCode: 'MEDIA.OFFLINE',
      });
      return;
    }
    this.patchState({
      status: 'validating',
      assetId: null,
      fileName: pending.file.name,
      purpose: pending.purpose,
      totalBytes: pending.file.size,
      uploadedBytes: 0,
      parts: [],
      errorCode: null,
      needsFile: false,
    });
    try {
      const session = await this.requestValue(
        this.api.createSession(
          {
            purpose: pending.purpose,
            expectedBytes: pending.file.size,
            fileName: pending.file.name,
            contentType: pending.file.type || 'application/octet-stream',
            courseId: pending.courseId,
          },
          pending.idempotencyKey,
        ),
      );
      if (!this.isCurrent(operation)) return;
      if (!isFuture(session.expiresAt)) {
        this.patchState({ status: 'error', errorCode: 'MEDIA.SESSION_EXPIRED' });
        return;
      }
      this.upload = persistedUploadFromSession(session, pending);
      this.patchState({ assetId: session.assetId });
      this.pendingStart = null;
      await this.save();
      await this.prepareAndUpload(operation);
    } catch (error: unknown) {
      if (!this.isCurrent(operation)) return;
      this.fail(error);
    }
  }

  private async prepareAndUpload(operation: number): Promise<void> {
    const upload = this.upload;
    const file = this.currentFile;
    if (!upload || !file || !this.isCurrent(operation)) return;
    if (!isFuture(upload.expiresAt)) {
      this.staleSession();
      return;
    }
    try {
      if (!this.currentHashes) {
        this.patchState({ status: 'validating', errorCode: null, needsFile: false });
        this.hashingAbort = new AbortController();
        this.currentHashes = await this.hasher.hash(
          file,
          upload.partSize,
          this.hashingAbort.signal,
          (loaded) => {
            if (this.isCurrent(operation)) {
              this.patchState({ uploadedBytes: 0, totalBytes: file.size, errorCode: null });
              this.setHashingProgress(loaded);
            }
          },
        );
        this.hashingAbort = null;
        if (!this.isCurrent(operation)) return;
        if (!matchesPersistedParts(upload.completedParts, this.currentHashes.parts)) {
          this.staleSession();
          return;
        }
        this.upload = { ...upload, sha256: this.currentHashes.sha256 };
        await this.save();
      }
      if (upload.mode === 'Stream') {
        await this.uploadStream(operation, file);
      } else {
        await this.uploadMultipart(operation, file, this.currentHashes);
      }
    } catch (error: unknown) {
      if (!this.isCurrent(operation) || isAbortError(error)) return;
      this.fail(error);
    }
  }

  private async uploadStream(operation: number, file: File): Promise<void> {
    const upload = this.requireUpload();
    const hashes = this.requireHashes();
    this.patchState({ status: 'uploading', needsFile: false, errorCode: null });
    const session = await this.requestResult(
      this.api.uploadStream(upload.uploadSessionId, file, hashes.sha256),
      (event) => {
        if (event.kind === 'progress') {
          this.patchState({
            uploadedBytes: Math.min(event.loaded, file.size),
            totalBytes: file.size,
          });
          return undefined;
        }
        return event.session;
      },
    );
    if (!this.isCurrent(operation) || this.pauseRequested) return;
    await this.markUploadCompleted(session, operation);
  }

  private async uploadMultipart(
    operation: number,
    file: File,
    hashes: MediaFileHashes,
  ): Promise<void> {
    for (const part of hashes.parts) {
      if (!this.isCurrent(operation)) return;
      if (this.isPartComplete(part.partNumber)) continue;
      if (this.pauseRequested) {
        this.patchState({ status: 'paused', needsFile: false });
        return;
      }
      this.patchState({ status: 'uploading', needsFile: false, errorCode: null });
      if (this.activeSignedPart?.part.partNumber !== part.partNumber) {
        const grant = await this.requestValue(
          this.api.issuePart(this.requireUpload().uploadSessionId, {
            partNumber: part.partNumber,
            expectedBytes: part.size,
            sha256: part.sha256,
          }),
        );
        if (!this.isCurrent(operation)) return;
        this.activeSignedPart = { part, grant };
      }
      await this.putSignedPart(file);
    }
    if (!this.isCurrent(operation) || this.pauseRequested) return;
    const upload = this.requireUpload();
    const allParts = upload.completedParts;
    if (allParts.length !== hashes.parts.length) {
      throw new MediaUploadFailure(
        'MEDIA.PARTS_INCOMPLETE',
        'All upload parts must complete first.',
      );
    }
    this.patchState({ status: 'finalizing', errorCode: null });
    const session = await this.requestValue(
      this.api.complete(
        upload.uploadSessionId,
        {
          totalBytes: file.size,
          sha256: hashes.sha256,
          parts: allParts,
        },
        upload.completionKey,
      ),
    );
    if (!this.isCurrent(operation)) return;
    await this.markUploadCompleted(session, operation);
  }

  private async putSignedPart(file: File): Promise<void> {
    const active = this.activeSignedPart;
    if (!active)
      throw new MediaUploadFailure('MEDIA.PART_NOT_ISSUED', 'The upload part was not issued.');
    if (!isFuture(active.grant.urlExpiresAt)) {
      throw new MediaUploadFailure('MEDIA.SIGNED_URL_EXPIRED', 'The signed upload URL expired.');
    }
    const offset = (active.part.partNumber - 1) * this.requireUpload().partSize;
    const content = file.slice(offset, offset + active.part.size);
    this.setPartProgress(active.part.partNumber, 0, 'uploading');
    const etag = await this.requestResult<DirectStorageUploadEvent, string>(
      this.directStorage.putPart(
        active.grant.uploadUrl,
        content,
        active.grant.requiredChecksumSha256,
      ),
      (event) => {
        if (event.kind === 'progress') {
          this.setPartProgress(active.part.partNumber, event.loaded, 'uploading');
          return undefined;
        }
        return event.etag;
      },
    );
    const upload = this.requireUpload();
    const completed: PersistedMediaPart = { ...active.part, etag };
    this.upload = {
      ...upload,
      completedParts: [
        ...upload.completedParts.filter((part) => part.partNumber !== completed.partNumber),
        completed,
      ],
    };
    this.activeSignedPart = null;
    this.setPartProgress(completed.partNumber, completed.size, 'complete');
    await this.save();
  }

  private async markUploadCompleted(session: UploadSession, operation: number): Promise<void> {
    const upload = this.requireUpload();
    this.upload = { ...upload, uploadCompleted: session.state === 'Completed' };
    await this.save();
    if (session.state !== 'Completed') {
      this.fail(
        new MediaUploadFailure('MEDIA.SESSION_TERMINAL', 'The upload session did not complete.'),
      );
      return;
    }
    this.patchState({
      status: 'scanning',
      uploadedBytes: upload.fileSize,
      totalBytes: upload.fileSize,
      parts: this.uploadState().parts.map((part) => ({
        ...part,
        loaded: part.size,
        state: 'complete',
      })),
      needsFile: false,
    });
    await this.pollStatus(operation);
  }

  private async pollStatus(operation: number): Promise<void> {
    const upload = this.upload;
    if (!upload || !this.isCurrent(operation)) return;
    while (this.isCurrent(operation)) {
      if (!this.connectivity.isOnline()) {
        this.patchState({ status: 'offline', errorCode: 'MEDIA.OFFLINE' });
        return;
      }
      try {
        const status = await this.requestValue(this.api.getStatus(upload.assetId));
        if (!this.isCurrent(operation)) return;
        if (this.applyMediaStatus(status)) return;
      } catch (error: unknown) {
        if (this.isCurrent(operation)) this.fail(error);
        return;
      }
      await wait(3_000);
    }
  }

  private applyMediaStatus(status: MediaStatus): boolean {
    if (status.state === 'Ready') {
      this.patchState({ status: 'ready', errorCode: null, needsFile: false });
      void this.removePersisted();
      this.upload = null;
      return true;
    }
    if (status.state === 'Rejected' || status.state === 'Deleted') {
      this.patchState({ status: 'rejected', errorCode: status.rejectionCode ?? 'MEDIA.REJECTED' });
      void this.removePersisted();
      this.upload = null;
      return true;
    }
    this.patchState({
      status:
        status.state === 'Processing' || status.state === 'RecoveryPending'
          ? 'processing'
          : 'scanning',
      errorCode: null,
    });
    return false;
  }

  private setHashingProgress(loaded: number): void {
    const state = this.uploadState();
    this.uploadState.set({ ...state, uploadedBytes: 0, totalBytes: state.totalBytes });
    void loaded;
  }

  private setPartProgress(
    partNumber: number,
    loaded: number,
    state: MediaUploadPartProgress['state'],
  ): void {
    const upload = this.requireUpload();
    const current = this.uploadState();
    const existing = current.parts.find((part) => part.partNumber === partNumber);
    const size =
      existing?.size ??
      this.currentHashes?.parts.find((part) => part.partNumber === partNumber)?.size ??
      0;
    const parts = existing
      ? current.parts.map((part) =>
          part.partNumber === partNumber
            ? { ...part, loaded: Math.min(loaded, part.size), state }
            : part,
        )
      : [...current.parts, { partNumber, size, loaded: Math.min(loaded, size), state }];
    const completed = sumCompletedBytes(upload.completedParts);
    const total = upload.fileSize;
    this.uploadState.set({
      ...current,
      parts,
      uploadedBytes: Math.min(
        total,
        completed + (state === 'complete' ? 0 : Math.min(loaded, size)),
      ),
      totalBytes: total,
    });
  }

  private isPartComplete(partNumber: number): boolean {
    return this.requireUpload().completedParts.some((part) => part.partNumber === partNumber);
  }

  private pauseForOffline(): void {
    this.operation++;
    this.abortActiveWork();
    this.patchState({ status: 'offline', errorCode: 'MEDIA.OFFLINE' });
  }

  private staleSession(): void {
    const upload = this.upload;
    if (upload) void this.removePersisted();
    this.resetRuntime();
    this.patchState({ status: 'error', errorCode: 'MEDIA.SESSION_EXPIRED', needsFile: false });
  }

  private fail(error: unknown): void {
    if (!this.connectivity.isOnline() || (error instanceof ApiProblem && error.status === 0)) {
      this.patchState({ status: 'offline', errorCode: 'MEDIA.OFFLINE' });
      return;
    }
    if (isTerminalSessionError(error)) {
      this.staleSession();
      return;
    }
    this.patchState({
      status: 'error',
      errorCode: errorCode(error),
      needsFile: this.currentFile === null,
    });
    if (this.activeSignedPart)
      this.setPartProgress(this.activeSignedPart.part.partNumber, 0, 'error');
  }

  private async save(): Promise<void> {
    const userId = this.userId();
    if (!userId || !this.upload) return;
    try {
      await this.persistence.save(userId, this.upload);
    } catch {
      // Resume persistence is optional; a storage quota failure must not discard the active upload.
    }
  }

  private async removePersisted(): Promise<void> {
    const userId = this.userId();
    const courseId = this.courseId ?? this.upload?.courseId;
    if (!userId || !courseId) return;
    try {
      await this.persistence.remove(userId, courseId);
    } catch {
      // The server remains authoritative if a local cleanup cannot complete.
    }
  }

  private requestValue<T>(source: Observable<T>): Promise<T> {
    return this.requestResult(source, (value) => value);
  }

  private requestResult<TEvent, TResult>(
    source: Observable<TEvent>,
    result: (event: TEvent) => TResult | undefined,
  ): Promise<TResult> {
    return new Promise((resolve, reject) => {
      let subscription: Subscription | null = null;
      let settled = false;
      const finish = (callback: () => void): void => {
        if (settled) return;
        settled = true;
        if (this.activeRequest === request) this.activeRequest = null;
        callback();
      };
      const request: ActiveRequest = {
        cancel: () => {
          subscription?.unsubscribe();
          finish(() => {
            reject(new MediaUploadAbortError());
          });
        },
      };
      this.activeRequest = request;
      subscription = source.subscribe({
        next: (event) => {
          const value = result(event);
          if (value !== undefined) {
            finish(() => {
              resolve(value);
            });
          }
        },
        error: (error: unknown) => {
          finish(() => {
            reject(error instanceof Error ? error : new Error('The request failed.'));
          });
        },
        complete: () => {
          finish(() => {
            reject(new Error('The request completed without a successful response.'));
          });
        },
      });
    });
  }

  private abortActiveWork(): void {
    this.hashingAbort?.abort();
    this.hashingAbort = null;
    this.activeRequest?.cancel();
    this.activeRequest = null;
  }

  private requireUpload(): PersistedMediaUpload {
    if (!this.upload) throw new Error('An upload session is required.');
    return this.upload;
  }

  private requireHashes(): MediaFileHashes {
    if (!this.currentHashes) throw new Error('File checksums are required.');
    return this.currentHashes;
  }

  private resetRuntime(): void {
    this.upload = null;
    this.currentFile = null;
    this.currentHashes = null;
    this.pendingStart = null;
    this.activeSignedPart = null;
  }

  private patchState(patch: Partial<MediaUploadState>): void {
    this.uploadState.update((state) => ({ ...state, ...patch }));
  }

  private userId(): string | null {
    return this.session.identity()?.userId ?? null;
  }

  private isCurrent(operation: number): boolean {
    return operation === this.operation;
  }
}

class MediaUploadFailure extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'MediaUploadFailure';
  }
}

const isInterruptible = (status: MediaUploadStatus): boolean =>
  status === 'validating' ||
  status === 'uploading' ||
  status === 'finalizing' ||
  status === 'scanning' ||
  status === 'processing';

const sameFile = (file: File, upload: PersistedMediaUpload): boolean =>
  file.name === upload.fileName &&
  file.size === upload.fileSize &&
  (file.type || 'application/octet-stream') === upload.contentType &&
  file.lastModified === upload.lastModified;

const matchesPersistedParts = (
  completed: readonly PersistedMediaPart[],
  hashes: readonly MediaFilePartHash[],
): boolean =>
  completed.every((part) => {
    const hash = hashes.find((candidate) => candidate.partNumber === part.partNumber);
    return hash?.size === part.size && hash.sha256 === part.sha256;
  });

const sumCompletedBytes = (parts: readonly PersistedMediaPart[]): number =>
  parts.reduce((total, part) => total + part.size, 0);

const isFuture = (value: string): boolean => {
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) && timestamp > Date.now();
};

class MediaUploadAbortError extends Error {
  constructor() {
    super('Upload was aborted.');
    this.name = 'AbortError';
  }
}

const isAbortError = (error: unknown): boolean =>
  error instanceof Error && error.name === 'AbortError';

const isTerminalSessionError = (error: unknown): boolean =>
  error instanceof ApiProblem &&
  (error.code === 'MEDIA.SESSION_TERMINAL' || error.code === 'MEDIA.NOT_FOUND');

const errorCode = (error: unknown): string => {
  if (
    error instanceof ApiProblem ||
    error instanceof DirectStorageUploadError ||
    error instanceof MediaUploadFailure
  ) {
    return error.code;
  }
  return 'MEDIA.UPLOAD_FAILED';
};

const wait = (milliseconds: number): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, milliseconds));

const maxByPurpose: Readonly<Record<MediaPurpose, number>> = {
  ProfileImage: 10 * 1024 * 1024,
  CourseImage: 20 * 1024 * 1024,
  CourseDocument: 100 * 1024 * 1024,
  AssignmentSubmission: 250 * 1024 * 1024,
  SourceVideo: 10 * 1024 * 1024 * 1024,
};

const mediaHintError = (file: File, purpose: MediaPurpose): string | null => {
  if (file.size <= 0) return 'MEDIA.EMPTY_FILE';
  if (file.size > maxByPurpose[purpose]) return 'MEDIA.FILE_TOO_LARGE';
  const type = file.type.toLowerCase();
  const allowed =
    purpose === 'SourceVideo'
      ? ['video/mp4', 'video/quicktime']
      : purpose === 'CourseDocument' || purpose === 'AssignmentSubmission'
        ? ['application/pdf']
        : ['image/jpeg', 'image/png', 'image/webp'];
  return type && !allowed.includes(type) ? 'MEDIA.FILE_TYPE_HINT' : null;
};
