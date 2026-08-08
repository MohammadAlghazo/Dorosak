# ADR-019: Phase 7 Media and Content Delivery Contracts

- Status: Accepted
- Date: 2026-08-07
- Scope: upload sessions, quarantine, processing, variants, delivery grants, and MediaWorker
- Supersedes missing local text for ADR-006 where this decision is more specific

## Context

Phase 7 must accept untrusted large files without routing them through API memory, keep originals private, fail closed when
malware scanning is unavailable, produce immutable ready variants, and give authorized callers short-lived delivery grants.
Phase 8 consumes only Ready media when creating immutable course releases.

## Decisions

### Providers and boundaries

- Application code depends on `IObjectStorage`, not a provider SDK. Phase 7 ships an S3-compatible adapter for local MinIO
  and tests. The Azure Blob adapter, CDN signing, replication, and production credentials are Phase 12 deployment work.
- The API issues upload sessions and signed URLs. `Dorosak.MediaWorker` exclusively scans and processes uploaded objects.
  It is a separate executable/container with bounded concurrency and no inbound HTTP endpoint.
- Durable media work is claimed from PostgreSQL with `FOR UPDATE SKIP LOCKED`. This preserves the established database
  outbox/retry model; Hangfire integration may replace the claim adapter without changing media contracts.

### Object keys and storage

- Clients never choose object keys. Keys are ASCII and server-generated:
  - `quarantine/{environment}/{ownerUserId}/{assetId}/original`
  - `ready/{environment}/{assetId}/{variantId}/{fileName}`
  - `captions/{environment}/{assetId}/{captionId}.vtt`
- Quarantine and ready storage are private. Originals remain private after processing and are never returned as public URLs.
- Database records retain provider, container, object key, version ID/ETag, SHA-256, verified bytes, and lifecycle state.
- Signed URLs are credentials, are never persisted or logged, and expire after ten minutes for upload and five minutes for
  delivery by default.

### Supported purposes and limits

- Purposes are `ProfileImage` (10 MiB), `CourseImage` (20 MiB), `CourseDocument` (100 MiB), `AssignmentSubmission`
  (250 MiB), and `SourceVideo` (10 GiB). Limits are configurable but startup validation forbids zero/unbounded production
  values.
- Accepted source formats are JPEG/PNG/WebP for images, PDF for documents, and MP4/QuickTime for source video. SVG,
  archives, executable formats, HTML, password-protected documents, and unknown/polyglot structures are rejected.
- Browser `Content-Type`, extension, and filename are metadata only. MediaWorker validates magic bytes and structure.
- SHA-256 is mandatory for the complete source. Part checksums use SHA-256 and duplicate part numbers are rejected.

### Upload protocol

- `POST /api/v1/uploads` creates an idempotent upload session after ownership, quota, daily usage, and concurrency checks.
  It returns `Stream` for files up to 32 MiB and `Multipart` otherwise.
- `PUT /api/v1/uploads/{id}/content` streams a small body directly to quarantine with strict `Content-Length`; it never
  buffers the complete file.
- `POST /api/v1/uploads/{id}/parts` records a unique part number/checksum and returns a short-lived signed `PUT` URL.
- `POST /api/v1/uploads/{id}/complete` accepts the uploaded part numbers/ETags, completes multipart storage exactly once,
  verifies declared size metadata, and queues scanning.
- `DELETE /api/v1/uploads/{id}` aborts incomplete storage and releases reserved quota. Repeated cancellation/completion is
  idempotent and returns the existing terminal result.
- Session TTL is 24 hours. Part size is selected between 8 MiB and 64 MiB. The API never proxies multipart part bodies.

### State machines and retries

- Upload session states are `Initiated`, `Uploading`, `Completed`, `Cancelled`, and `Expired`.
- Asset states are `Initiated`, `Uploaded`, `Scanning`, `Processing`, `Ready`, `Rejected`, `RecoveryPending`, and `Deleted`.
- Valid availability path is `Initiated -> Uploaded -> Scanning -> Processing -> Ready`; rejection may occur from scan or
  processing, and only unreferenced assets may transition to `Deleted` after retention.
- Scanner/storage transient failure leaves the object quarantined and retries with exponential backoff and jitter. Five
  failed attempts mark processing failed for operations/alerting but do not make the object available.
- Worker claims and completion are idempotent. A stale lock expires after five minutes. Cleanup expires sessions, aborts
  multipart uploads, and removes orphan quarantine objects after a 24-hour grace period.

### Validation and processing

- MediaWorker downloads to a restricted temporary file, computes SHA-256/size, inspects magic bytes, and streams the file
  to ClamAV `INSTREAM`. Scanner timeout/unavailability never fails open.
- Images are decoded and re-encoded by FFmpeg with metadata removed. Course/profile images produce immutable JPEG, WebP,
  and AVIF variants where the installed FFmpeg supports the encoder, at widths 320, 640, 1280, and source width limits.
- Documents remain private PDF variants after validation and scanning.
- Videos are inspected with `ffprobe`, then FFmpeg produces HLS/fMP4 renditions at 360p, 720p, and 1080p when the source
  permits, H.264/AAC, six-second segments, a master playlist, and a poster image. Processes use argument lists without a
  shell, have time/resource limits, run non-root, and have no required network egress.
- Caption tracks are UTF-8 WebVTT, private, scanned/validated, and associated to one asset with locale and label.

### Authorization, references, and delivery

- Upload/read authorization is resource-based. Owner, explicit course collaborators, and `Media.ManageAny` are evaluated
  server-side; unauthorized private IDs return `404`.
- Course media creation requires an owned/editable course ID. Profile and assignment purposes require their concrete owner.
- Authoring stores concrete nullable media asset foreign keys. It may reference an asset before Ready, but publication
  readiness fails until every referenced asset is Ready.
- `GET /api/v1/media/{assetId}/status` returns state and safe metadata only.
- `POST /api/v1/media/{assetId}/download-grant` rechecks ownership/entitlement and returns a signed URL for a Ready variant,
  never for the quarantine original. Grants are reusable only until their short expiration.
- Safe download filenames are response metadata and never influence keys. Range support is delegated to object storage/CDN.

### Quotas and accounting

- Defaults are teacher account 500 GiB, course 200 GiB, student account 10 GiB, teacher daily 100 GiB, student daily
  20 GiB. Session creation reserves expected source bytes atomically; cancellation/expiry releases the reservation.
- Verified source bytes replace the reservation after completion. Variant bytes are accounted separately. Cross-user
  deduplication is not performed in Phase 7 to avoid ownership disclosure and reference-count complexity.

### Frontend

- Upload UI is lazy-loaded and exposes idle, validating, uploading, paused, finalizing, scanning, processing, ready,
  rejected, cancelled, error, and offline states.
- Direct-storage requests use a dedicated client with no bearer/API metadata/CSRF headers. Progress is per-part plus total.
- Multipart sessions persist non-secret metadata in IndexedDB for reload resume; signed URLs are never persisted. A user
  must reselect the local file after browser restart before bytes resume.
- Upload mutations are not automatically retried. Users can retry failed parts, pause issuance, resume, or cancel. Offline
  state pauses uploads and does not queue file bytes for background sync.
- Progress uses accessible native progress semantics and throttled polite announcements. All errors have Arabic/English copy.

### Audit, telemetry, and tests

- Audit events cover session issued/completed/cancelled/expired, scan clean/infected/failed, processing ready/rejected,
  deletion, and download grant issuance. Signed URLs, checksums, filenames, object keys, and user/course IDs are excluded
  from logs and metric labels.
- Required tests include lifecycle/domain transitions, quota/concurrency, duplicate parts, completion idempotency, IDOR,
  EICAR, scanner/storage outage, corrupt/magic mismatch, oversized streams, interrupted multipart cleanup, signed grant
  restrictions, real PostgreSQL migrations, MinIO/ClamAV adapters, worker duplicate claims, frontend resume/cancel/progress,
  MediaWorker container smoke, and bounded-resource execution.

## Consequences

- Phase 7 can operate locally with MinIO/ClamAV while retaining a provider-neutral production boundary.
- Drafts may reference media, but Phase 8 remains responsible for immutable release activation.
- Azure Blob/CDN implementation and provider disaster-recovery controls remain explicit Phase 12 work, not hidden Phase 7
  assumptions.
