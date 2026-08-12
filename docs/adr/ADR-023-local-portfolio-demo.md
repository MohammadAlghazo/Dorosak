# ADR-023: Local-First Portfolio Demo

- Status: Accepted
- Date: 2026-08-12
- Scope: definition of done, local infrastructure, and optional cloud boundaries

## Context

Dorosak is currently intended as a CV/portfolio demo, not a public commercial launch. Requiring cloud subscriptions,
merchant accounts, domains, or paid providers would add cost and operational work without improving the demonstrated
application architecture.

## Decision

- The portfolio demo runs with local PostgreSQL, Redis, MinIO, Mailpit, and ClamAV through Docker Compose.
- No Azure, Neon, Cloudinary, Postmark, Stripe, domain, TLS certificate, or other external account is required for the
  portfolio definition of done.
- Local adapters are the default. External adapters remain disabled and configuration-driven so a future deployment can
  replace infrastructure without changing Domain rules or public API contracts.
- GitHub Actions builds and tests the application, but cloud deployment, staging promotion, canary rollout, on-call alerts,
  and production disaster recovery are optional future-launch work.
- A clean local setup, deterministic demo data, tested user journeys, and clear documentation replace the former cloud
  staging and production launch gates.

## Future launch boundary

If Dorosak is launched publicly, create a new ADR after choosing a budget and provider. That work must cover hosting,
managed PostgreSQL, object storage/CDN, email delivery, secrets, backups, monitoring, domains/TLS, legal policies, and any
real-money payment requirements. Azure is one possible provider, not an application dependency.
