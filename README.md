# Dorosak

Dorosak is an Arabic-first educational platform for students, instructors, and platform administrators.

## Project Status

The repository contains the production backend foundation plus Phase 5 identity/security and Phase 6 catalog/authoring:
teacher onboarding, course drafts and revisions, taxonomy, publication review, public discovery contracts, Angular
catalog/search flows, instructor authoring, and administrative review. Immutable releases, media, learning, commerce,
and enrollment remain in subsequent phases.

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
