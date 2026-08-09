# Dorosak

Dorosak is an Arabic-first educational platform for students, instructors, and platform administrators.

## Project Status

The repository contains the production backend foundation plus Phase 5 identity/security, Phase 6 catalog/authoring,
Phase 7 media/content delivery, and Phase 8 learning/assessment/publishing. Implemented flows include immutable course
releases, release-backed discovery, demo checkout and entitlement-backed enrollment, learner manifests and progress,
notes/bookmarks, quizzes with
duration/deadline enforcement and append-only grading revisions, text assignments, protected delivery grants, and the
corresponding Arabic/English Angular workspaces.
Phase 9 is now in progress: engagement/realtime foundations, assessment audiences, and secure PDF assignment attachments
are being added. New production video uploads are intentionally deferred until the storage/processing/CDN server is chosen;
the provider-neutral local media pipeline remains available for development and tests.
A fake checkout using `100 DEMO` credits is available for product testing and never accepts card or bank details. Real
payment-provider integration and production Azure/CDN deployment remain deferred.

## Architecture

[`PROJECT_PLAN.md`](PROJECT_PLAN.md) is the single source of truth for product scope, architecture, security, data,
delivery, and operational decisions.

## Development Setup

Follow [`docs/DEVELOPMENT_SETUP.md`](docs/DEVELOPMENT_SETUP.md) to configure Neon access and start the local
Redis, MinIO, Mailpit, and ClamAV services.

The backend solution is located at `backend/Dorosak.slnx`. Restore, build, and test it from the repository root:

```powershell
dotnet restore .\backend\Dorosak.slnx --locked-mode
dotnet build .\backend\Dorosak.slnx --no-restore
dotnet test .\backend\Dorosak.slnx --no-build --no-restore
```

## Approved Baseline

- .NET 10 LTS and ASP.NET Core 10
- Angular 21.2 LTS with SSR, hydration, and PWA support
- Neon PostgreSQL
- Redis-compatible managed services
- Docker and Docker Compose
- Azure Container Apps reference deployment
- GitHub Actions CI/CD

## Repository Rules

- Do not commit secrets, connection strings, private keys, or local environment files.
- Keep source code, identifiers, comments, and commit messages in English.
- Record architecture changes in `PROJECT_PLAN.md` and an ADR before implementation.
- Complete the required verification before committing each delivery phase.
