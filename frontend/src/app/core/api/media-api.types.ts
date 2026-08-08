export type MediaPurpose =
  'ProfileImage' | 'CourseImage' | 'CourseDocument' | 'AssignmentSubmission' | 'SourceVideo';

export type UploadMode = 'Stream' | 'Multipart';

export interface CreateUploadSessionRequest {
  purpose: MediaPurpose;
  expectedBytes: number;
  fileName: string;
  contentType: string;
  courseId: string | null;
}

export interface UploadSession {
  uploadSessionId: string;
  assetId: string;
  state: 'Initiated' | 'Uploading' | 'Completed' | 'Cancelled' | 'Expired';
  mode: UploadMode;
  expectedBytes: number;
  partSize: number;
  expiresAt: string;
}

export interface IssueUploadPartRequest {
  partNumber: number;
  expectedBytes: number;
  sha256: string;
}

export interface UploadPartGrant {
  uploadSessionId: string;
  partNumber: number;
  expectedBytes: number;
  uploadUrl: string;
  requiredChecksumSha256: string;
  urlExpiresAt: string;
}

export interface CompletedUploadPart {
  partNumber: number;
  size: number;
  sha256: string;
  etag: string;
}

export interface CompleteUploadRequest {
  totalBytes: number;
  sha256: string;
  parts: readonly CompletedUploadPart[];
}

export type MediaAssetState =
  | 'Initiated'
  | 'Uploaded'
  | 'Scanning'
  | 'Processing'
  | 'Ready'
  | 'Rejected'
  | 'RecoveryPending'
  | 'Deleted';

export interface MediaVariant {
  id: string;
  kind: string;
  contentType: string;
  bytes: number;
  width: number | null;
  height: number | null;
  durationSeconds: number | null;
}

export interface MediaStatus {
  assetId: string;
  purpose: MediaPurpose;
  state: MediaAssetState;
  contentType: string;
  declaredBytes: number;
  verifiedBytes: number | null;
  rejectionCode: string | null;
  variants: readonly MediaVariant[];
}

export interface DownloadGrantRequest {
  variantId: string | null;
  fileName: string | null;
}

export interface DownloadGrant {
  assetId: string;
  variantId: string;
  url: string;
  expiresAt: string;
  fileName: string;
  contentType: string;
}

export type MediaUploadEvent =
  | { kind: 'progress'; loaded: number; total: number }
  | { kind: 'complete'; session: UploadSession };
