# ADR-018: Phase 6 Catalog and Authoring Contracts

- Status: Accepted
- Date: 2026-08-07
- Scope: Phase 6 catalog, teacher onboarding, authoring drafts, review, and public discovery contracts

## Context

Phase 6 must establish catalog and authoring without activating immutable `CourseRelease` records. Media, assessments,
enrollment, reviews, commerce, and recommendation projections belong to later phases. Public discovery must never expose
draft or merely approved content.

## Decisions

### Delivery boundary

- Phase 6 implements taxonomy, teacher applications, teacher profiles, course ownership/collaboration, localized course
  metadata and permanent slugs, versioned drafts/curriculum, publication review, and public catalog/search contracts.
- Review approval moves a course to `ReadyToPublish`. Phase 8 alone may create and activate an immutable release and move
  it to `Published`.
- Public catalog, detail, search, featured, popular, and recommendation endpoints read release-backed projections only.
  They return valid empty responses until Phase 8 creates those projections. Drafts are never used as a public fallback.
- `catalog_documents`, popularity, recommendation, price, rating, and enrollment projections are deferred to their owning
  phases. Phase 6 defines transport contracts without fake data or nullable release references.

### Teacher onboarding

- Application states are `Pending`, `InReview`, `Approved`, `Rejected`, and `Withdrawn`.
- A user may have only one `Pending` or `InReview` application. A rejected or withdrawn application may be followed by a
  new application.
- Submission requires confirmed email. Approval atomically creates the teacher profile, assigns the `Teacher` role while
  retaining `Student`, increments authorization/session versions, and records a security event.
- Rejection requires a reviewer reason. Review endpoints require `TeacherApplication.ReviewAny` and recent Admin MFA.

### Courses, drafts, and review

- Phase 6 course states are `Draft`, `InReview`, `ChangesRequested`, `ReadyToPublish`, and `Archived`.
- `courses.owner_user_id` is authoritative. Collaborator roles are `Editor`, `CoInstructor`, and `Reviewer`; `Owner` is
  prohibited. Only the owner submits, archives, or transfers ownership.
- Each course has one active draft. Draft metadata and curriculum writes require `If-Match` with ETag format `"v{version}"`.
  Missing preconditions return `428`; stale versions return `412 COURSE.VERSION_CONFLICT` with the current ETag header.
- Curriculum uses stable section/lesson UUIDv7 IDs and integer positions. Every accepted update appends immutable section
  and lesson revisions; it never overwrites historical revisions.
- Allowed review transitions are `Draft|ChangesRequested -> InReview`, `InReview -> ChangesRequested|ReadyToPublish`, and
  `InReview -> Draft` for owner withdrawal. Final publish/unpublish transitions are deferred to Phase 8.
- Soft deletion is allowed only before publication approval. Delete, submit, review, and later publish actions are audited.

### Localization and slugs

- Supported content locales are exactly `ar` and `en`; each course has a required default locale and may add the other.
- Public route locale is authoritative and is forwarded through `Accept-Language`. Missing localization returns `404`.
- Slugs use lowercase Latin letters/digits/hyphens. Arabic titles receive a deterministic transliteration fallback plus a
  short stable suffix when no Latin text exists.
- `(locale, slug)` is permanently unique. One slug per course/locale is current; historical slugs are retained forever.
  Once releases are active, historical requests return `308` to the current localized slug.

### Taxonomy

- Categories use stable lowercase codes, optional parent categories, explicit display order, and mandatory Arabic/English
  localizations. Phase 6 seeds only approved top-level codes: `technology`, `business`, `data`, and `personal-development`.
- Tags use stable codes and localized labels but have no production seed until a curated list is approved.
- Taxonomy mutations require `Catalog.ManageTaxonomy` and invalidate catalog caches only after commit.

### Public queries and search

- Success responses keep the `{ "data": ... }` envelope. Paged data owns `items`, `nextCursor`, and `hasMore`.
- Catalog default limit is `24`; search default is `20`; maximum is `100`.
- Cursors are opaque base64url JSON signed with HMAC and include version, deterministic sort keys, and a canonical query
  hash. Invalid or mismatched cursors return `422`.
- Catalog sort modes are `newest`, `title`, and `popular`. Search adds `relevance`. Deterministic ID tie-breakers are
  mandatory.
- Blank search behaves as catalog browsing. Suggestions require at least two characters, debounce at `250 ms`, and return
  at most eight items.
- Highlight output is `[{ "text": "...", "matched": true|false }]`; raw HTML is prohibited.
- Arabic normalization is versioned as `ar-v1`: Unicode Form C, remove combining Arabic diacritics and tatweel, normalize
  alef variants to alef, alef maqsura to ya, and preserve original display text. English uses PostgreSQL `english` FTS.
- Search telemetry stores locale, query-length/result-count buckets, latency, sort/filter dimensions, zero-result flag, and
  normalizer version only. Query text and persistent query hashes are prohibited.

### Cache, rate limits, and SSR

- Public catalog/category cache keys include environment, schema version, locale, every filter, sort, cursor/query hash,
  and projection generation. Catalog TTL is 60 seconds; taxonomy TTL is five minutes; both use jitter.
- Redis failures fail open to PostgreSQL for public reads. Authorization and business invariants never depend on Redis.
- Search limits are 60 requests/minute per anonymous IP and 180/minute per authenticated user, with `Retry-After`.
- Public SSR contains anonymous data only. Enrollment, personalized recommendations, and other user-specific sections load
  after hydration and use `no-store`.
- Search pages are `noindex,follow`; catalog and current course slugs are indexable and include canonical/hreflang links.

### Security and testing

- UI permissions are hints only. Server resource authorization checks ownership/collaboration and course state before
  returning private resources; hidden resources return `404`.
- Rich text remains plain sanitized text in Phase 6. Arbitrary HTML is not accepted.
- The phase gate requires domain transition tests, real PostgreSQL migration/query tests, draft concurrency tests, IDOR and
  permission tests, search contract/normalization/cursor tests, frontend async-state tests, and Arabic/English accessibility.

## Consequences

- Phase 6 can be completed without weakening the release boundary or publishing incomplete content.
- Public discovery contracts and taxonomy are usable immediately, while course results remain empty until Phase 8.
- Phase 8 must consume `ReadyToPublish` reviews and add immutable release/search projection migrations before public courses
  become discoverable.
