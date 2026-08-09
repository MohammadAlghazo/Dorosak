# ADR-020: Phase 8 Release, Learning, and Assessment Contracts

- Status: Accepted
- Date: 2026-08-08
- Scope: immutable releases, publishing, free enrollment, learning/progress, quizzes, and assignment text grading

## Context

Phase 6 intentionally stopped at `ReadyToPublish`; Phase 7 provides Ready media but defers enrollment entitlement and
assignment file uploads. Phase 8 must make an approved course learnable without mutating the learner's experience when a
new course release is published.

## Decisions

### Publication and release

- Final activation is an explicit Admin operation requiring `Course.PublishAny`, recent authentication, and Admin MFA. A
  teacher may request/review but cannot activate a release.
- `PublishingCoordinator` is an Application orchestration service. It reads Authoring, Media, and Assessment readiness
  ports, builds one canonical manifest, and sends one Catalog activation command. It never accesses a `DbSet` directly.
- Activation is synchronous and atomic for the release root, manifest rows, catalog document projection, course state, and
  cache-generation update. The transactional outbox publishes invalidation after commit.
- A release has `Draft`, `Active`, `Superseded`, and `Unpublished` states. Only one release is Active per course. Existing
  enrollments keep their pinned Active/Superseded release; new enrollments use the current Active release.
- Each release has a unique monotonically increasing `releaseNumber` and canonical SHA-256 `manifestHash`. Release rows and
  their manifest children are immutable: runtime has INSERT/SELECT only and no UPDATE/DELETE privileges. Application update
  commands are absent; retention deletion is restricted to a migration/maintenance role.
- An approved review must have the current draft version. Every referenced media asset and required variant must be `Ready`.
  Every quiz/assignment version must be `Ready`. Any failure leaves the course `ReadyToPublish` and creates an auditable
  failed activation result without a partial release.
- After an Active release exists, a new authoring revision may be started while the current release remains public. The draft
  is independent; publishing it creates the next release. Unpublishing blocks new enrollment/public discovery but does not
  revoke existing pinned enrollments.

### Release manifest

- The manifest snapshots localized metadata, current slug, taxonomy terms, instructor display names, ordered sections and
  lessons, exact lesson revisions, exact media asset/variant IDs, caption references, completion requirements, quiz versions,
  and assignment versions.
- Catalog documents are release-owned snapshots. They contain weighted English FTS/search text, normalized Arabic text,
  typed filter columns, and safe highlight data. They are created in the same activation transaction; rebuild is idempotent
  by `(releaseId, locale)`.
- Phase 8 catalog price is always `Free`; Commerce replaces this with captured entitlement sources in Phase 10. Featured,
  popularity, recommendation, ratings, and discussions remain empty/deferred projections.
- Historical slugs return `308` to the current active localized slug. A course detail never falls back to a draft.

### Enrollment and entitlement

- Phase 8 supports free enrollment only. A free enrollment atomically creates/activates one `learning.entitlement` and one
  `learning.enrollment` pinned to the current Active release. Payment/order/subscription sources are Phase 10 contracts.
- Enrollment states are `Active`, `Completed`, `Suspended`, `Revoked`, and `Expired`; entitlement states are `Active`,
  `Revoked`, and `Expired`. Re-enrollment after Revoked creates a new enrollment identity and pins the current release.
- Enrollment creation is idempotent by `(userId, courseId, Idempotency-Key)` and concurrent requests produce one active result.
- Server checks enrollment, entitlement, release membership, and lesson membership before returning a manifest/lesson/media;
  hidden resources return `404`.

### Learning and progress

- Learner routes are `/:locale/my-learning`, `/:locale/learn/:enrollmentId`, and
  `/:locale/learn/:enrollmentId/lessons/:lessonId`.
- The manifest is release-pinned and includes sections, lesson order/types, exact media variants/captions, assessment
  references, completion rules, learner progress, and next lesson. It contains asset IDs, not persisted signed URLs.
- Progress commands carry `clientCommandId`, monotonic `sequence`, position, watched intervals, and an explicit completion
  intent. The server merges intervals, rejects stale sequences from reverting completion, and deduplicates commands in one
  transaction. Mutations are never automatically retried by the frontend.
- Video completion requires 90% watched coverage; article/document completion requires explicit completion; quiz/assignment
  completion follows assessment result. Course/section completion is recalculated from the pinned manifest.
- Notes, bookmarks, recently viewed, and continue-learning are own-user records. They use bounded text, idempotent client
  commands, and user-scoped IndexedDB metadata/outbox only. Protected video and signed URLs are never service-worker cached.

### Assessments and assignment boundary

- Quizzes are fully in Phase 8: immutable quiz versions, single/multiple choice, true/false, short answer, ordered questions,
  attempt limits, duration/deadline, pass score, server-authoritative answers, objective auto-scoring, and manual short-answer
  grading where required.
- Quiz attempts are tied to `(enrollmentId, quizVersionId)`, have an idempotent start/submit, enforce limits/deadlines under
  row locks, and never expose correct answers before submission policy allows.
- Assignment definitions and immutable versions, text submissions, deadlines, multiple-submission policy, grading, feedback,
  grade revisions, and audit are in Phase 8. Binary assignment submission files are implemented in Phase 9 through the
  concrete submission-file and MediaWorker pipeline.
- Grade overrides require course teacher scope or Admin permission, recent authentication, audit event, and append-only grade
  revision. A learner cannot grade or read another learner's submission.

### Media playback

- Phase 8 uses short-lived fMP4 variant grants for the local/provider-neutral player and exposes quality selection. HLS objects,
  poster, and captions are included in the manifest; multi-object CDN authorization remains Phase 12.
- The player supports keyboard controls, captions, transcript, speed, quality, volume, Picture-in-Picture, reduced motion,
  no autoplay with sound, safe loading/error/offline states, and progress batching.

### Cache, telemetry, and security

- Release activation increments a projection generation and invalidates catalog/detail/search/suggestion caches after commit
  through the outbox. Public responses contain release snapshots only; learner responses are `no-store`.
- Publish/unpublish, enrollment, progress conflict/completion, quiz submit/grade, and entitlement changes are audited. Signed
  URLs, answers, notes, and PII are never logged or emitted as metric labels.
- The API enforces rate limits, CSRF for unsafe cookie requests, IDOR checks, optimistic/idempotent commands, and `404` for
  hidden resources. Frontend permission checks remain hints only.

## Consequences

- Phase 8 can publish and teach free courses while preserving old release experiences for current learners.
- Commerce and binary assignment files remain clean extension points rather than insecure placeholders.
- Release activation is more expensive than a status update because it snapshots all dependencies atomically, but it makes
  rollback, learner reproducibility, and catalog consistency explicit.
