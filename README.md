# Dorosak

Dorosak is an Arabic-first educational platform and portfolio demo for students, instructors, and platform administrators.

## Project Status

The repository contains the production backend foundation plus Phase 5 identity/security, Phase 6 catalog/authoring,
Phase 7 media/content delivery, and Phase 8 learning/assessment/publishing. Implemented flows include immutable course
releases, release-backed discovery, demo checkout and entitlement-backed enrollment, learner manifests and progress,
notes/bookmarks, quizzes with
duration/deadline enforcement and append-only grading revisions, text assignments, protected delivery grants, and the
corresponding Arabic/English Angular workspaces.
Phase 9 is locally implemented: course reviews, release-scoped discussions, authenticated reports, audited moderation,
assessment audiences, secure PDF assignment attachments, conversations, notifications, announcements, and SignalR
reconnect/resynchronization are available in the Arabic/English Angular workspaces.
Processed profile/course images can optionally use Cloudinary after quarantine scanning and FFmpeg re-encoding; the adapter
is disabled by default and no credentials are committed. New production video uploads are intentionally deferred until the
storage/processing/CDN server is chosen; the provider-neutral local pipeline remains available for development and tests.
A fake checkout using `100 DEMO` credits is available for product testing and never accepts card or bank details. Real
payment-provider integration, cloud deployment, and public launch are optional future work and are not required to complete
the portfolio demo.

## Architecture

[`PROJECT_PLAN.md`](PROJECT_PLAN.md) is the single source of truth for product scope, architecture, security, data,
delivery, and operational decisions.

## Development Setup

Follow [`docs/DEVELOPMENT_SETUP.md`](docs/DEVELOPMENT_SETUP.md) to start local PostgreSQL, Redis, MinIO, Mailpit, and ClamAV.
No Azure, Neon, Cloudinary, email-provider, or payment-provider account is required.

The backend solution is located at `backend/Dorosak.slnx`. Restore, build, and test it from the repository root:

```powershell
dotnet restore .\backend\Dorosak.slnx --locked-mode
dotnet build .\backend\Dorosak.slnx --no-restore
dotnet test .\backend\Dorosak.slnx --no-build --no-restore
```

## Approved Baseline

- .NET 10 LTS and ASP.NET Core 10
- Angular 21.2 LTS with SSR, hydration, and PWA support
- PostgreSQL 18 locally; Neon remains an optional future host
- Redis locally; a managed service remains an optional future adapter
- Docker and Docker Compose
- GitHub Actions CI/CD

## Repository Rules

- Do not commit secrets, connection strings, private keys, or local environment files.
- Keep source code, identifiers, comments, and commit messages in English.
- Record architecture changes in `PROJECT_PLAN.md` and an ADR before implementation.
- Complete the required verification before committing each delivery phase.
