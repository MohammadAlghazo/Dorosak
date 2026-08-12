# ADR-022: Portfolio Demo Commerce and Credentials

- Status: Accepted
- Date: 2026-08-12
- Scope: non-monetary commerce, subscriptions, and credentials for the portfolio demo

## Decision

- Published courses display a fixed `100 DEMO` credit price. `DEMO` has no monetary value and cannot be purchased,
  withdrawn, transferred, or refunded as money.
- The authenticated checkout explicitly simulates `success` or `failure`. It never asks for PAN, CVV, bank account,
  billing address, or provider payment credentials.
- Each attempt stores a durable `DemoOrder` and `DemoPayment` snapshot. Simulated success grants a `Demo` entitlement and
  creates the learner enrollment transactionally; simulated failure grants nothing.
- Idempotency and audit rules remain mandatory so the product flow exercises the same reliability boundary expected from
  a future provider adapter.
- Demo subscriptions are local plan records only. They may be activated or canceled without billing cycles, invoices,
  proration, tax, renewal charges, or provider webhooks.
- Certificates are local immutable records issued from confirmed course completion. They use a random public verification
  code and an HTML/print view; hosted PDF generation, QR delivery, and external signing are optional future adapters.
- Refunds, disputes, chargebacks, payouts, tax, KYC, provider reconciliation, and real-money ledgers are outside the
  portfolio demo definition of done.

## Future replacement boundary

- Before accepting real money, replace `DemoProvider` with a hosted provider checkout and verified webhook fulfillment,
  add legal/tax/refund/dispute decisions, and run the full commerce security and reconciliation gates.
- Future providers must be added behind Infrastructure ports. Domain and Application code must not depend on Stripe,
  Azure, Cloudinary, Postmark, or any other vendor SDK.
