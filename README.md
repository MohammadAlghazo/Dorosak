# Dorosak

Dorosak is an Arabic-first educational platform for students, instructors, and platform administrators.

## Project Status

The repository is in the architecture and engineering-foundation phase. Application source code has not been
generated yet.

## Architecture

[`PROJECT_PLAN.md`](PROJECT_PLAN.md) is the single source of truth for product scope, architecture, security, data,
delivery, and operational decisions.

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
