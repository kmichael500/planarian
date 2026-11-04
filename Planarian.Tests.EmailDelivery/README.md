# Temporary backend test project

`Planarian.Tests.EmailDelivery` hosts email-delivery and account-access tests until the canonical backend test projects are available in `main`.

## Move to the canonical test projects

**TODO:** After `feature/cave-revisions-completion` merges and
`Planarian.Tests.Unit` and `Planarian.Tests.Integration` are available in
`main`, move these tests into their canonical projects. The temporary
`TemporaryTestProjectLifecycleTests` fails when either project appears.

1. Move unit and architecture-contract tests into `Planarian.Tests.Unit`.
2. Move `AccountAccessPostgresTests`, `InvitationClaimPostgresTests`, and
   `BulkInvitationPostgresTests` into `Planarian.Tests.Integration/Account`,
   reusing its PostgreSQL fixtures rather than this project's Testcontainers helper.
3. Run the moved tests, then remove this temporary project and
   `.github/workflows/email-delivery-tests.yml`.

Account-access changes deploy independently; do not backport the other branch's
test infrastructure to accomplish this migration.

## Account-access integration tests

These tests use disposable PostgreSQL/PostGIS 16 containers with real EF migrations.
Run the relevant cases with:

```bash
dotnet test Planarian.Tests.EmailDelivery/Planarian.Tests.EmailDelivery.csproj \
  --filter 'FullyQualifiedName~AccountAccessPostgresTests|FullyQualifiedName~InvitationClaimPostgresTests|FullyQualifiedName~BulkInvitationPostgresTests'
```

For Colima, set `DOCKER_HOST` to its socket. If Ryuk cannot connect,
`TESTCONTAINERS_RYUK_DISABLED=true` may be needed locally; without Ryuk, a
crashed test process can leave containers behind.

## TODO: deferred email-delivery integration coverage

These email-delivery cases remain for the canonical integration suite. The temporary
PostgreSQL fixture covers account access, not the full email-delivery workflow.

- Verify v30 upgrades legacy `MessageLogs`/`Users` without inventing delivery history or backfilling the new nullable tracking fields.
- Verify the filtered unique `MessageLogs.ProviderCorrelationId` index and both `MessageLogEvents` replay/idempotency constraints under concurrent inserts.
- Verify `RecordProviderEvent` is atomic and idempotent when duplicate webhooks race, including rollback/change-tracker cleanup after a PostgreSQL unique violation.
- Verify concurrent/out-of-order provider events honor the compare-and-swap delivery-state policy, including a later delayed permanent bounce after `Delivered`.
- Verify `TrySetEmailConfirmationMessageLog` permits only one concurrent resend claim, loses safely if confirmation occurs before the compare-and-swap, and rejects a stale in-flight send after a newer pending-email change replaces the confirmation code.
- Verify a provider webhook can update a durably committed message log even when it arrives immediately after submission starts.
- Verify account-user projections choose the latest invitation attempt deterministically and count human/automated opens and clicks from persisted events correctly.
- Verify a failed first invitation send remains retryable, and a successful resend updates `InvitationSentOn` without creating a second invitation identity.
- Verify a request cancellation after registration has durably committed does not cancel the started confirmation-email attempt or its `EmailConfirmationMessageLogId` claim/status reconciliation.
- Verify a request cancellation after invitation creation has durably committed does not cancel the started invitation-email attempt, and a successful submission still persists `InvitationSentOn`.
- Verify cancellation after a `MessageLog` is created cannot leave the attempt `Submitting` solely because link/delete/submission-status reconciliation observed the original request-abort token.
- Verify a losing confirmation-resend compare-and-swap deletes its unused `MessageLog` coherently even when the originating HTTP request is canceled.

## TODO: deferred authentication and transport behavior

These cases require application-host or authentication fixtures beyond this
project's scope. Move them to the canonical integration suite.

- Verify a production HTTPS request receives HSTS, development does not, and an Azure-style request with `X-Forwarded-Proto: https` is treated as HTTPS before HSTS/redirect handling.
- Verify a correctly signed Mailgun event whose environment metadata matches the running deployment reaches repository persistence (case-insensitively), while mismatched or missing environment metadata never persists an event.
- Verify cave-file, profile-photo, and trip-photo reads cannot cross their account/project authorization boundary when a caller supplies another tenant's file or user identifier.
- Verify successful login of a legacy password hash persists the upgraded hash, while a concurrent password change cannot be overwritten by the login rehash.
- Verify stale or deleted-user JWT sessions are rejected by the request pipeline, while pre-v31 JWTs without a session-version claim remain valid for users still at version `0`.
- Verify a stale or deleted-user auth cookie can still POST logout with a valid antiforgery token and clears both auth/antiforgery cookies, while a missing or invalid antiforgery token is rejected.
- Verify PostgreSQL-backed email identity lookups trim surrounding whitespace and behave culture-independently, including under a Turkish current culture.
- Verify Settings password changes require the current password, increment `SessionVersion`, refresh the initiating session, invalidate older sessions, and attempt the password-changed notification without rolling back the password change when email submission fails.
- Verify Forgot Password completion does not require the old password, increments `SessionVersion`, invalidates existing sessions, and attempts the same password-changed notification.
- Verify anonymous user mutations allow only registration, invitation cleanup, email confirmation, password-reset issuance, and password-reset completion field sets; unrelated anonymous user mutations must fail.
- Verify requesting an email change to another user's verified email returns `EmailAlreadyExists`, leaves both verified/pending email state unchanged, and does not send a confirmation email.
- Verify multiple users may hold the same unverified pending email, but only one can ultimately claim it as the verified address; a losing confirmation preserves the user's original verified email.
- Verify concurrent confirmations racing for the same verified email preserve the unique database invariant and return `EmailAlreadyExists` rather than a generic unexpected-error response for the loser.
