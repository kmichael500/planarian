# Temporary email-delivery test project

`Planarian.Tests.EmailDelivery` is a temporary home for backend tests added on the email-delivery branch because `main` does not yet contain the canonical backend unit/integration test infrastructure.

## Removal trigger

`TemporaryTestProjectLifecycleTests` intentionally fails as soon as either `Planarian.Tests.Unit/Planarian.Tests.Unit.csproj` or `Planarian.Tests.Integration/Planarian.Tests.Integration.csproj` exists. At that point:

1. Move pure/model-contract tests into the canonical unit-test project. Keep architecture-wide contract tests (for example route-name uniqueness) under the canonical unit project’s `Architecture` area.
2. Implement the deferred PostgreSQL-backed tests below in the canonical integration-test project.
3. Delete `Planarian.Tests.EmailDelivery` and `.github/workflows/email-delivery-tests.yml`.

## TODO: deferred PostgreSQL/integration coverage

These require the real PostgreSQL integration-test harness; do not replace them with EF InMemory tests because the important behavior depends on PostgreSQL constraints, transactions, and `ExecuteUpdateAsync`.

- Verify v30 upgrades legacy `MessageLogs`/`Users` without inventing delivery history or backfilling the new nullable tracking fields.
- Verify the filtered unique `MessageLogs.ProviderCorrelationId` index and both `MessageLogEvents` replay/idempotency constraints under concurrent inserts.
- Verify `RecordProviderEvent` is atomic and idempotent when duplicate webhooks race, including rollback/change-tracker cleanup after a PostgreSQL unique violation.
- Verify concurrent/out-of-order provider events honor the compare-and-swap delivery-state policy, including a later delayed permanent bounce after `Delivered`.
- Verify `TrySetEmailConfirmationMessageLog` permits only one concurrent resend claim and loses safely if confirmation occurs before the compare-and-swap.
- Verify a provider webhook can update a durably committed message log even when it arrives immediately after submission starts.
- Verify invitation acceptance/reassignment preserves historical invitation `MessageLogs` before the temporary invited user is deleted and that the composite `SetNull` relationship behaves as intended.
- Verify account-user projections choose the latest invitation attempt deterministically and count human/automated opens and clicks from persisted events correctly.
- Verify a failed first invitation send remains retryable, and a successful resend updates `InvitationSentOn` without creating a second invitation identity.
- Verify a request cancellation after registration has durably committed does not cancel the started confirmation-email attempt or its `EmailConfirmationMessageLogId` claim/status reconciliation.
- Verify a request cancellation after invitation creation has durably committed does not cancel the started invitation-email attempt, and a successful submission still persists `InvitationSentOn`.
- Verify cancellation after a `MessageLog` is created cannot leave the attempt `Submitting` solely because link/delete/submission-status reconciliation observed the original request-abort token.
- Verify a losing confirmation-resend compare-and-swap deletes its unused `MessageLog` coherently even when the originating HTTP request is canceled.
