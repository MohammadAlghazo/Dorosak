# ADR-021: Phase 9 Engagement, Realtime, and Assignment Attachment Contracts

- Status: Accepted
- Date: 2026-08-09
- Scope: engagement, communications, realtime notifications, and binary assignment submission attachments
- Depends on: ADR-019, ADR-020

## Context

Phase 9 is the first phase after immutable releases and learning are operational. The roadmap names the phase
`Engagement and Realtime`, while ADR-019 and ADR-020 explicitly defer binary assignment submission files to Phase 9.
Both concerns share the same security boundary: a learner may interact only with resources reachable from the pinned
release and active enrollment, and every user-generated binary remains private until the existing MediaWorker pipeline
marks it `Ready`.

## Decisions

### Assignment submission attachments

- Binary files are owned by a concrete `AssignmentSubmission`; the database uses `assessment.submission_files` with a
  concrete foreign key to `assessment.assignment_submissions` and `media.media_assets`.
- A submission file row and its media asset are created together when the learner requests an upload session, after the
  learner owns the enrollment, the assignment version is present in the pinned release, the submission is still editable
  under its deadline/submission policy, and the requested file count, size, content type, and account quota pass
  server-side checks. The row remains `Pending` by the referenced asset state until processing reaches `Ready`.
- The existing media upload session is reused with purpose `AssignmentSubmission`. Its asset receives the submission
  owner and course association, but no public or course-wide media authorization is inferred from those fields.
- Upload completion queues the normal ClamAV/magic-byte/processing job. Private asset status metadata remains visible to
  the submission owner for polling, but file content and download grants are unavailable until the referenced asset is
  `Ready`; `Rejected`, `Deleted`, and non-ready assets never expose file content.
- Assignment submission and file attachment mutations are idempotent. Reusing a key with a different payload returns
  the existing idempotency conflict; concurrent attach requests are serialized by the submission row lock and unique
  `(submission_id, client_file_id)` constraint.
- Download grants recheck submission ownership, course-grader scope, assignment version, enrollment status, and asset
  readiness on every request. Quarantine object keys and signed URLs are never logged, cached, or returned.
- Phase 9 supports PDF and the configured safe document/image types only. Archives, executable formats, HTML/SVG, and
  password-protected documents remain rejected. Azure Blob/CDN provider work remains Phase 12.

### Assessment audience

- Every quiz and assignment version declares `AllEnrolled` or `SelectedLearners`. The selected form stores concrete user
  IDs that must already have a current enrollment in the course; an empty or foreign selection is rejected.
- Audience membership is immutable with the assessment version and is rechecked when rendering learner manifests, reading
  lessons/attempts, starting or submitting attempts, creating submissions, and attaching files. Knowing a hidden version ID
  never grants access and returns the same safe `404` shape.
- The published release snapshots the audience mode while concrete selected membership remains attached to the immutable
  assessment version. New learners automatically receive only `AllEnrolled` assessments; selected audiences do not expand
  when another learner later enrolls.

### Deferred video provider

- Source-video processing remains a tested local/provider-neutral capability from Phase 7. The instructor UI does not offer
  new source-video uploads in this phase. Production video storage, encoding capacity, CDN delivery, quotas, and provider
  credentials remain disabled until the hosting/server decision in Phase 12.

### Engagement and communications

- Reviews, discussion threads/comments, reports, moderation cases/actions, conversations/messages, notifications, and
  announcements are durable PostgreSQL state in their owning schemas. No business state is stored only in Redis or
  SignalR.
- A learner may review a course only with an active or completed enrollment for that course. One review per learner/course
  is enforced by a unique constraint. Discussion visibility is checked against the course/release enrollment boundary.
- Comments allow a maximum depth of two, likes are unique per user/comment, and reports target exactly one concrete
  resource. Moderation actions are append-only and audited; hidden or removed content returns the safe public shape.
- Messages use `(conversation_id, sender_id, client_message_id)` for deduplication. Only current participants may read or
  send. Notifications are created in the same transaction as the durable event and are read/updated only by their owner.
- Course announcements are teacher/admin mutations and learner-owned notification projections; they never authorize a
  learner to access a course or release that the learner does not already own.

### Discussion implementation profile

- A discussion thread is owned by a concrete course release and may optionally target one release lesson. Learner routes
  resolve the scope through the learner's active or completed enrollment and active entitlement; instructor routes resolve
  it through the course owner, a non-reviewer collaborator, or an explicit course-management permission. Invalid or foreign
  scopes use the same safe `404` shape.
- A numeric comment depth of `0` is a root comment and the maximum numeric depth is `2`; a reply at depth `2` is rejected.
  The parent foreign key stores the parent depth so PostgreSQL enforces the same invariant as the domain model.
- Authors may edit their own published thread/comment during a 15-minute edit window. They may remove their own published
  content after that window; removal is a tombstone and never a hard delete. Moderation states retain the safe public shape.
- Thread/comment mutations use one transaction for the business row, audit row, and idempotency record. Resource advisory
  locks serialize thread/comment removal against replies and likes. Idempotent create replays reauthorize the current scope
  and rebuild the current safe response instead of returning stale private content.
- REST discussion reads use signed, query-bound cursors. Course and lesson scopes have separate opaque paths, and private
  responses remain `no-store`; realtime delivery and resynchronization remain a subsequent slice over these durable rows.

### Reports and moderation implementation profile

- The first reports migration supports concrete `course_id`, `review_id`, `comment_id`, and `reported_user_id` foreign
  keys with `num_nonnulls(...) = 1`. `message_id` is added only after Communications introduces its durable message table;
  a generic or unconstrained message target is not permitted as an interim compatibility shortcut.
- Report reasons are `Spam`, `Harassment`, `HateSpeech`, `Misinformation`, `Copyright`, `PersonalData`, and `Other`.
  Reports and cases use `Open`, `InReview`, `Resolved`, and `Dismissed`. One unresolved report per reporter and concrete
  target is enforced independently of transport idempotency.
- A moderation case is created in the same transaction as its report. Administrative actions are append-only and limited
  initially to `StartReview`, `HideContent`, `RestoreContent`, `Resolve`, and `Dismiss`. Hide/restore applies only to review
  and comment targets owned by Engagement; course and account actions require a later coordinated command in their owning
  module rather than a cross-module write.
- Creating a report requires current visibility of its target and cannot disclose a foreign private discussion. Report
  creation and moderation actions require idempotency keys. Moderation reads require `Moderation.ReviewAny`; actions use
  the existing Admin high-risk policy, MFA/recent authentication, an audit header, and a separate decision reason.
- Reports, cases, and actions are retained for 730 days after case closure unless legal hold or an approved policy requires
  longer retention. No target body snapshot is copied into the report, moderation action, diagnostic log, or audit payload.

### Realtime

- REST is the source of truth and provides cursor/sequence-based resynchronization. SignalR is best effort for notification
  and engagement updates, not a persistence boundary.
- A single `/hubs/realtime` hub is introduced after durable contracts. Every group join checks current authorization; the
  client supplies no trusted group name. Events carry `eventId`, `eventType`, `schemaVersion`, `occurredAt`, `resourceId`,
  and `sequence` where applicable.
- Redis is used only as a backplane/ephemeral transport. On reconnect the client calls REST with its last sequence and
  safely handles duplicates.

## Security and retention

- Private attachment and engagement responses are `no-store`; service-worker/public transfer caches exclude them.
- IDs from another learner, course, submission, or conversation are hidden as `404` where the resource could disclose
  existence. Admin/high-risk actions require the existing permission, recent-authentication, MFA, idempotency, and audit
  policies.
- Attachment metadata is retained with the immutable submission history while the submission is retained. Physical media
  deletion is allowed only after all concrete references and the retention/grace period are gone.
- Logs and metrics exclude file names, object keys, checksums, message bodies, notes, and raw PII.

## Exit criteria

- PostgreSQL migration and schema/grant tests pass on a clean database.
- Attachment lifecycle tests cover IDOR, deadline/ownership, duplicate/idempotent attach, non-ready/rejected media,
  ClamAV EICAR, quota/concurrency, and safe download grants.
- Engagement tests cover review eligibility/uniqueness, comment depth/likes, report target constraints, participant IDOR,
  message dedupe, notification ownership, moderation audit, and announcement scope.
- API/OpenAPI contracts, Angular Arabic/English RTL/LTR states, mobile accessibility, reconnect/resync behavior, and full
  backend/frontend gates pass.
