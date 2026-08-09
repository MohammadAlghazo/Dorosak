# ADR-022: Temporary Demo Commerce

- Status: Accepted temporarily
- Date: 2026-08-09
- Scope: non-monetary checkout used while real payment integration is intentionally postponed

## Decision

- Published courses display a fixed `100 DEMO` credit price. `DEMO` has no monetary value and cannot be purchased,
  withdrawn, transferred, or refunded as money.
- The authenticated checkout explicitly simulates `success` or `failure`. It never asks for PAN, CVV, bank account,
  billing address, or provider payment credentials.
- Each attempt stores a durable `DemoOrder` and `DemoPayment` snapshot. Simulated success grants a `Demo` entitlement and
  creates the learner enrollment transactionally; simulated failure grants nothing.
- Idempotency and audit rules remain mandatory so the product flow exercises the same reliability boundary expected from
  a future provider adapter.

## Replacement boundary

- Before accepting real money, replace `DemoProvider` with a hosted provider checkout and verified webhook fulfillment,
  add legal/tax/refund/dispute decisions, and run the full commerce security and reconciliation gates.
