# Terrane Maps Project Protocols

**Status:** Canonical project workflow router
**Version:** 1.1
**Effective date:** 2026-08-21

## Purpose

This document is the canonical registry and router for reusable Terrane Maps project workflows.

It intentionally does **not** duplicate the detailed procedures in the workflow documents. Its job is to identify which applies, require that procedure to be consulted before acting, and keep the Project Instructions stable as workflows evolve.

When a workflow below is invoked, consult the named protocol file itself before performing the workflow. Do not rely on a remembered, summarized, or earlier-chat version of that protocol.

If a required protocol file is unavailable or cannot be consulted, say so and do not pretend that workflow's full procedure was applied.

## Workflow registry

### Finalize for Codex

**Typical triggers:**

- “Finalize this for Codex.”
- An unmistakable direct variant asking to finalize the developed work into a Codex implementation handoff.

**Authoritative procedure:** `FINALIZE_FOR_CODEX.md`

**Mode:** Research, validation, specification finalization, and Codex handoff packaging.

Before producing the final handoff, consult `FINALIZE_FOR_CODEX.md` and execute it in full.

### Implement with Desktop Commander

**Typical triggers:**

- “Implement this with Desktop Commander.”
- “Use Desktop Commander to implement this plan.”
- “Implement the attached plan with Desktop Commander.”
- An unmistakable direct variant requesting implementation through Desktop Commander.

**Authoritative procedure:** `IMPLEMENT_WITH_DESKTOP_COMMANDER.md`

**Mode:** Live repository implementation and validation.

Before modifying the repository, consult `IMPLEMENT_WITH_DESKTOP_COMMANDER.md` and execute it in full.

### Validate / Review Changes

**Typical triggers:**

- “Validate these changes.”
- “Validate current changes.”
- “Validate staged changes.”
- “Validate this branch against main.”
- “Review these changes.”
- “Heavily validate this.”
- An unmistakable direct variant requesting adversarial validation or review of repository changes, architecture, plans, or documentation.

**Authoritative procedure:** `VALIDATE_CHANGES.md`

**Mode:** Evidence-based, adversarial, review-only validation unless the user explicitly changes the task to implementation.

Before beginning the review, consult `VALIDATE_CHANGES.md` and execute it in full.

## Dispatch rules

1. Use the user's current request to determine the workflow. Natural-language variants do not need to match the examples word-for-word when the intent is unmistakable.
2. When exactly one workflow clearly applies, consult that protocol before substantive work.
3. When the user explicitly requests multiple workflows in sequence, execute them in the requested order and apply each protocol only to its corresponding phase.
4. Do not silently blend review-only and implementation modes. Validation remains review-only unless the user explicitly authorizes implementation.
5. Do not treat ordinary conversation as a workflow invocation merely because it discusses Codex, Desktop Commander, Git, reviews, or validation.
6. The detailed workflow document is authoritative for how that workflow is executed. This router is authoritative only for workflow selection and routing.
7. A user's explicit instruction in the current request may narrow or override a default inside a workflow when that override is intentional and compatible with higher-priority instructions. Do not infer an override from ambiguity.
8. Repository facts must be verified through the applicable workflow's source-of-truth rules rather than assumed from project conversation history.

## Shared database-access boundary

This distinction applies to every registered workflow:

- **Existing application databases are opt-in.** Do not directly connect to, inspect, modify, migrate, reseed, reset, or otherwise operate on an existing development, staging, production, or other application dataset unless the user explicitly authorizes that database access for the task.
- **Repository-owned isolated test databases are normal validation.** When the repository's established test harness provisions its own disposable, isolated database for a test run—such as a unique temporary PostgreSQL/PostGIS database, Testcontainers database, or equivalent—the agent may create, migrate, query, mutate, and destroy that test database without asking for separate database authorization when those tests are relevant to the task.
- This authorization includes applying the repository's real migrations inside the disposable test database and exercising real EF/database behavior required by integration tests.
- This authorization does **not** permit substituting an arbitrary shared database, reusing an existing application dataset, or treating a convenient connection string as permission to inspect non-test data.
- If it is unclear whether a database is disposable test infrastructure owned by the repository harness or an existing application dataset, verify the test infrastructure first. Ask only if the distinction cannot be resolved from repository configuration and source.

Do not describe a relevant repository integration test as blocked merely because it uses a database when that test owns an isolated disposable database under this rule.

## Adding future workflows

To add another reusable project command:

1. Create one focused Markdown protocol containing the detailed procedure.
2. Add one registry entry here with its trigger intent, authoritative filename, and mode.
3. Keep detailed workflow rules out of Project Instructions.
4. Do not duplicate the same normative rule across this router and the workflow document unless the duplication is necessary to preserve a safety or authority boundary.

The Project Instructions should normally remain unchanged when new workflows are added; update this router instead.
