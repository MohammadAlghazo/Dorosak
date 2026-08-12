# ADR-024: Phase 11 Demo Administration

- Status: Accepted
- Date: 2026-08-12
- Scope: local portfolio administration, CMS, analytics, and audit reads

## Context

Phase 11 must demonstrate useful platform administration without recreating production-scale BI, legal intake, or cloud
operations. The current database already contains the source records needed for a small operational overview, while raw
analytics event ingestion would add storage and privacy work that the portfolio demo does not need.

## Decision

- Admin analytics starts with a permission-gated, read-only overview derived from aggregate SQL over existing source tables.
- The overview returns counts and queue health only. It never returns names, email addresses, message bodies, search terms,
  or raw event payloads.
- The overview is generated on demand for the local demo. Daily aggregate tables and background exports remain future work
  until the data volume or a concrete report requires them.
- Phase 11 CMS is limited to bilingual, revisioned informational pages and FAQs with explicit draft/publish actions.
- Platform settings are typed, allow-listed demo settings. Arbitrary key/value secrets are not accepted.
- Audit access remains separately permission-gated and exposes safe audit projections rather than before/after entity JSON.
- All Phase 11 screens remain local-first and require no analytics, CMS, or export provider.

## Consequences

- The first dashboard is inexpensive to operate and demonstrates authorization and aggregate query design without a new
  analytics schema.
- Source-table aggregates are intentionally bounded to the portfolio dataset. A future public launch must introduce daily
  projections before volume makes repeated aggregate reads unsuitable.
- CMS publishing and settings mutations remain audited high-risk operations; dashboard reads do not require MFA.
