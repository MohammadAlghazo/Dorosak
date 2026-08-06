# ADR-017: Phase 5 Identity Contracts

## Status

Accepted on 2026-08-06.

## Context

The architecture plan defines the browser token model and Phase 5 capabilities, but several transport and security values must be fixed before the API and Angular client can share stable contracts.

## Decision

- Successful JSON responses use a `data` envelope. Successful commands without a response body return `204`.
- Access tokens are asymmetric JWTs valid for 10 minutes and held in browser memory only.
- Refresh tokens are opaque, stored only as hashes, rotated on every use, idle for 14 days, and expire absolutely after 30 days.
- A refresh request repeated within 10 seconds is treated as a concurrent race and does not revoke the family. Reuse after that window revokes the session family and records a security event.
- The refresh cookie is `__Secure-dorosak-refresh` with `Secure`, `HttpOnly`, `SameSite=Lax`, and `Path=/api/v1/auth`.
- Antiforgery uses the readable `XSRF-TOKEN` cookie and `X-XSRF-TOKEN` header.
- Email verification tokens expire after 24 hours. Password-reset tokens expire after 1 hour.
- Recent authentication is valid for 15 minutes.
- Sign-in returns either `authenticated` or `mfaRequired`. MFA challenges are opaque, expire after 5 minutes, allow five attempts, and never issue access or refresh credentials before completion.
- TOTP secrets are protected with ASP.NET Core Data Protection. Recovery codes are random, hashed, single-use values shown once.
- Permission definitions remain code constants. Role assignments use ASP.NET Core Identity role claims with a stable `permission` claim type.
- HIBP k-anonymity is the reference breached-password adapter. It is required when enabled in production configuration and disabled in local development and automated tests unless explicitly exercised.
- Redis uses atomic counters for sensitive endpoint limits. Registration, sign-in, password reset, and refresh fail closed with `503` when the security rate-limit store is unavailable.
- `GET /api/v1/me/profile` returns the current identity and capability snapshot after sign-in or refresh. Browser permissions are UX hints only.

## Consequences

- API and Angular contracts can be tested together without parsing JWT claims for mutable account state.
- API and Worker workloads must share Data Protection keys in multi-process environments.
- Production requires an asymmetric signing key and a dedicated Redis security connection.
- A later provider change for breached-password checks or email delivery remains behind Infrastructure adapters.
