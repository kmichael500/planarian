# Validate Changes Protocol

**Status:** Normative project review procedure
**Version:** 1.1
**Effective date:** 2026-08-21

## Trigger

Execute this procedure when the user says **“Validate these changes”**, **“Validate current changes”**, **“Validate staged changes”**, **“Validate this branch against main”**, **“Review these changes”**, **“Heavily validate this”**, or gives an unmistakable direct variant requesting an evidence-based repository review.

The user's wording determines the requested comparison scope. Do not silently substitute a different diff.

## Purpose

This procedure is for adversarial validation and review, not implementation.

The goal is to determine whether the reviewed changes are correct, necessary, coherent, appropriately simple, properly ordered, consistent with the actual repository, compatible with relevant dependencies and standards, and unlikely to create avoidable future problems.

A strong review must evaluate both the details and the system:

- what actually changed;
- whether consequential statements and assumptions are true;
- whether each feature or concept is sound as a unit;
- whether related concepts work together;
- whether implementation ordering and prerequisites are correct;
- whether the design is more complicated than necessary;
- whether existing repository or upstream mechanisms solve the problem better;
- whether the change creates avoidable lock-in, migration pain, compatibility problems, security risks, performance problems, or operational debt;
- whether tests and objective evidence actually prove the intended behavior.

Unless the user explicitly asks for fixes, remain in **review-only mode** and do not modify product source or repository state.

---

## 1. Establish current implementation truth

When Desktop Commander is available, use it proactively to inspect the real repository and run appropriate non-destructive validation.

The live repository is **current implementation truth**.

The approved task, plan, specification, or documentation intent is **normative task truth**.

Do not infer repository facts from memory or previous chats when they can be verified directly.

Before reviewing, normally determine:

- repository root and identity;
- current branch;
- `HEAD` commit;
- requested base branch/ref and its exact commit;
- merge base when a branch comparison is involved;
- staged changes;
- unstaged tracked changes;
- relevant untracked files;
- applicable repository instructions;
- relevant build/test/package configuration.

Do not call a comparison “against current main” unless the exact `main` commit used for review is identified and sufficiently verified for that claim.

If only a local base ref is available, identify its exact SHA and state that it is the reviewed baseline.

Do not switch branches, reset, clean, stage, commit, or otherwise mutate repository state merely to perform a review.

---

## 2. Distinguish Git review surfaces

Do not conflate working-tree, index, committed branch, and untracked changes.

### Unstaged changes

For **“validate unstaged changes”**, review the working tree relative to the index, equivalent in meaning to `git diff`.

### Staged changes

For **“validate staged changes”**, review the index relative to `HEAD`, equivalent in meaning to `git diff --staged`.

### Current local changes

For **“validate current changes”**, review:

- staged tracked changes;
- unstaged tracked changes;
- relevant untracked files.

Keep staged and unstaged findings distinguishable.

### Branch against base

For **“validate <branch> against <base>”**, normally review the branch's committed delta from the branch/base merge point, equivalent in intent to a three-dot Git comparison such as:

`git diff <base>...<branch>`

Also inspect the relevant commit history.

Record the merge base.

### Complete current-branch review

If the user asks to heavily validate the **current branch against a base** and local staged, unstaged, or relevant untracked changes exist, review both:

1. the committed branch delta from the merge base; and
2. the local changes.

Clearly identify these as separate review surfaces.

Use the combined effective state from the merge base to the working tree as a cross-check when useful.

Do not double-count one problem merely because it appears in multiple views.

### Explicit scope restrictions

Honor explicit restrictions such as **“staged only,” “committed only,”** or **“do not include working tree.”**

---

## 3. Review architecture before details

Start by understanding:

- the problem being solved;
- the intended behavior or documentation;
- affected subsystems;
- architectural boundaries;
- persistent data or formats affected;
- APIs or external contracts affected;
- security or authorization boundaries affected;
- assumptions made by the change.

Evaluate the overall design and the interactions among its major pieces before focusing mainly on line-level issues.

A fundamental design problem should be surfaced early, but continue far enough to identify other independent material findings unless the invalid premise makes them meaningless.

---

## 4. Review every changed human-authored item

Inspect every changed human-authored file in the requested scope.

Do not skip a file because it is:

- documentation;
- tests;
- configuration;
- a migration;
- a build script;
- “just types” or “just constants.”

Generated files or bulk data may be sampled when appropriate, but verify that their inclusion is expected and that they do not hide consequential changes.

For large reviews, maintain enough of a coverage ledger that files or sections are not silently omitted.

---

## 5. Validate documentation statements by claim type

Documentation changes are executable architecture in the sense that later implementation agents may treat them as truth. Validate consequential statements explicitly.

### Current-state claims

Examples:

- “The repository currently uses X.”
- “This API returns Y.”
- “Data is stored in Z.”
- “This subsystem owns this responsibility.”

Verify against source, tests, configuration, migrations, or runtime behavior.

### Library/framework/platform/standards claims

Verify version-sensitive behavior against authoritative documentation for the version actually used or intentionally targeted.

Prefer:

1. official specifications and standards;
2. official library/framework/platform documentation;
3. upstream source or release notes when documentation is insufficient;
4. authoritative vendor guidance.

Do not validate current behavior against generic or obsolete documentation.

### Proposed future-state claims

Do not reject a proposed requirement because it is not implemented yet.

Instead validate:

- technical feasibility;
- consistency with approved decisions;
- compatibility with the current baseline;
- prerequisites;
- transition/migration needs;
- contradictions;
- whether the proposed implementation sequence is realistic.

### Product and policy decisions

Do not research away an explicitly approved product decision just because alternatives exist.

Validate its technical consequences and internal consistency.

Surface only genuinely unresolved consequential choices requiring user preference.

### Numeric/performance/storage/limit claims

Verify using authoritative documentation, repository evidence, calculations, benchmarks, or clearly labeled estimates.

Distinguish measured facts from projections.

---

## 6. Validate each feature as a coherent unit

For every material feature or architecture concept, review the whole concept, not only the lines that mention it.

As applicable, evaluate:

- purpose and scope;
- fit with current architecture;
- ownership and boundaries;
- identifiers and invariants;
- data model and persistence;
- lifecycle/state transitions;
- API/interface contracts;
- error handling and partial failure;
- retries and idempotency;
- concurrency/consistency;
- caching/invalidation;
- offline/online behavior;
- import/export;
- migrations/backward compatibility;
- performance/scaling;
- security/privacy;
- testability;
- observability;
- implementation prerequisites;
- future-compatibility constraints;
- explicit non-goals and deferred behavior.

Do not declare a feature sound after validating only its happy path.

---

## 7. Validate interacting concepts together

After feature-level review, group related concepts according to actual coupling and test their combined assumptions.

Typical groups include:

- shared data models or identifiers;
- persistence + synchronization;
- caching + invalidation;
- offline selection + storage + import/export;
- sharding/partitioning + replacement/update/sync;
- authorization + sharing + deduplication;
- source formats + conversion + canonical storage;
- UI state + background processing;
- migrations + old/new readers and writers.

For each group ask:

1. Do the pieces make compatible assumptions?
2. Is there one clear source of truth?
3. Are ownership and mutation boundaries clear?
4. Can operations occur in an unsafe order?
5. Are identifiers and invariants stable across the group?
6. Can one feature invalidate another's assumptions?
7. Do retries, replacement, deletion, partial failure, and recovery behave coherently?
8. Are cross-feature performance/storage costs acceptable?
9. Are multiple mechanisms solving the same problem unnecessarily?
10. Would implementing the pieces independently create avoidable rework that a shared primitive should prevent?

Then perform a whole-system pass for interactions that span multiple groups.

---

## 8. Validate implementation order

For plans, architecture docs, and multi-milestone changes, derive the actual dependency order instead of trusting the written order.

For each milestone/step identify:

- prerequisites;
- artifacts or behavior later steps depend on;
- schema or compatibility gates;
- tests enabled by the step;
- external dependencies;
- whether the repository remains valid after the milestone;
- whether work can safely run in parallel.

Look for:

- circular dependencies;
- functionality scheduled before required primitives;
- incompatible reader/writer migration ordering;
- tests delayed until after behavior they should protect;
- abstractions created before requirements justify them;
- irreversible choices made before enough evidence exists;
- “big bang” milestones that should be independently verifiable increments.

Prefer an order where every milestone creates an observable, testable improvement and reduces uncertainty for later work.

---

## 9. Challenge complexity and over-engineering

Explicitly challenge every material abstraction, subsystem, indirection, dependency, custom format, state machine, background mechanism, and future-proofing mechanism.

Ask:

- What present requirement makes this necessary?
- Does the repository already contain a mechanism that solves it?
- Does the relevant library/platform already provide the behavior?
- Is the generality actually required now?
- Does it create another source of truth?
- Does it increase synchronization paths or failure states?
- Is custom machinery replacing a standard primitive?
- What capability would be lost if it were removed?
- Is the implementation/maintenance burden proportional to the benefit?

Prefer the simplest design that satisfies current requirements and **deliberate** future-compatibility constraints.

Do not simplify merely to reduce line count.

Do not justify current complexity with speculative future requirements unless later correction would be genuinely expensive and that compatibility concern is explicitly intended.

---

## 10. Review “easy now, painful later” decisions

Give extra scrutiny to decisions that become expensive to reverse after release or after data accumulates, including:

- persisted schemas;
- identifiers;
- file formats and serialization;
- public APIs;
- sync protocols;
- partitioning/sharding;
- cryptographic choices;
- authorization boundaries;
- ownership models;
- deduplication semantics;
- migration strategy;
- externally visible URLs/keys;
- dependency/vendor lock-in;
- platform-specific assumptions.

For each consequential hard-to-reverse decision:

1. explain why reversal would be expensive;
2. verify that the choice is deliberate;
3. inspect meaningful alternatives;
4. determine whether a small correction now avoids major later rework.

Apply this scrutiny proportionally. Routine implementation choices do not need architecture-decision ceremony.

---

## 11. External research and dependency validation

Research aggressively when a material uncertainty is externally answerable.

Prefer sources in this order:

1. official specifications or standards;
2. official platform/library/framework documentation;
3. upstream source/release notes;
4. repository source/tests/history;
5. authoritative engineering guidance;
6. high-quality secondary sources only when needed.

Identify the actual or targeted version when behavior is version-sensitive.

For dependencies, evaluate when relevant:

- whether a dependency is needed at all;
- whether an existing dependency already solves the problem;
- recommended integration patterns;
- compatibility;
- maintenance status;
- security implications;
- licensing/policy constraints;
- migration/upgrade burden.

Do not apply generic “best practices” without connecting them to the repository's actual constraints.

---

## 12. Security and trust-boundary review

Deepen security analysis when the change affects:

- authentication/authorization;
- secrets/credentials;
- sensitive data;
- remote/untrusted input;
- externally reachable services;
- file parsing/import;
- sandboxing/privilege boundaries;
- sync/sharing;
- dependencies/supply chain.

Check whether the architecture changes a trust boundary even if the document does not label it security-related.

Do not invent security findings without a plausible threat or violated security property.

---

## 13. Tests and objective evidence

Determine what evidence could actually falsify the proposed design or implementation if it were wrong.

As appropriate:

- inspect existing tests;
- run focused unit/integration/regression tests;
- run type checking/lint/builds;
- validate migrations;
- execute schema/architecture validators;
- verify examples and documented commands;
- run small experiments/POCs for uncertain technical assumptions;
- inspect benchmark evidence for performance claims.

### Database-backed validation

Treat database access according to the shared boundary in `PROJECT_PROTOCOLS.md`.

Repository integration tests that provision their own isolated disposable database are normal, non-destructive validation from the user's perspective and may be run when relevant without separate database authorization. This includes temporary PostgreSQL/PostGIS databases, Testcontainers-managed databases, per-test databases, real migrations applied to those temporary databases, and database assertions performed within that isolated test lifecycle.

Do not classify such a test as blocked merely because it uses EF Core, PostgreSQL/PostGIS, migrations, or a connection string internally.

This does **not** authorize direct access to an existing application/development/staging/production database or any other shared dataset. Do not substitute an arbitrary database for the repository-owned isolated test harness. If the repository configuration does not make the distinction clear, inspect the harness/configuration first and ask only if the target database still cannot be classified.

For documentation-only changes, do not run a huge test suite merely for ceremony. Choose validation that proves the changed claims and architecture.

Review tests themselves: a passing test is weak evidence if it could not catch the failure being discussed.

---

## 14. Independent adversarial passes

Do not stop after the first findings.

For substantial reviews, perform these passes:

### Pass A — Baseline and scope
Confirm exactly what is under review.

### Pass B — Architecture and feature coherence
Review overall design, each feature, invariants, ownership, and cross-feature interactions.

### Pass C — Detailed change review
Inspect every changed human-authored file/section in scope.

### Pass D — External validation
Research consequential library/platform/standards claims and assumptions.

### Pass E — Implementation order and transition
Validate prerequisites, sequencing, migrations, compatibility, and incremental milestones.

### Pass F — Simplicity and reversibility
Look specifically for over-engineering, duplicate mechanisms, speculative future-proofing, and hard-to-reverse mistakes.

### Pass G — Verification
Assess tests and objective evidence.

### Pass H — Fresh final pass
Re-read the architecture and major findings after the detailed review. Search for assumptions accepted too early, interactions missed locally, contradictions, and coverage gaps.

When independent read-heavy subagents are available, use them selectively for separable high-risk questions and reconcile their conclusions against primary evidence.

Do not use multiple reviewers as a substitute for resolving contradictions.

---

## 15. Finding quality and severity

Use stable IDs such as `VAL-001`.

Every material finding should state:

- **Severity**
- **Location / affected concept**
- **Problem**
- **Why it matters**
- **Evidence**
- **Recommended correction**
- **Confidence**, when uncertainty is material

Severity:

### BLOCKER
Not safe/correct to accept as written: e.g. data-loss/security-boundary risk, fundamentally invalid architecture, materially false normative documentation, or an unresolved decision required before implementation.

### HIGH
Likely to cause substantial bugs, rework, compatibility problems, operational pain, or a difficult future migration.

### MEDIUM
Meaningful correctness, maintainability, complexity, sequencing, testing, or documentation problem that should normally be fixed.

### LOW
Real but limited improvement with small practical impact.

Do not inflate severity or bury important findings among style nits.

Technical evidence outranks reviewer preference.

---

## 16. Positive validation and coverage ledger

A useful review must show what was actually validated, not only what failed.

For substantial reviews include a concise ledger of:

- diff surfaces reviewed;
- changed files reviewed;
- major features/concepts reviewed;
- interaction groups reviewed;
- external documentation/research checked;
- tests/commands/experiments performed;
- material claims that could not be independently verified.

For important features with no finding, state that they were reviewed and summarize the main invariants/interactions that held.

Do not claim “everything is correct” simply because no issue was found.

---

## 17. Review-only safety

Unless explicitly authorized to implement fixes:

- do not edit product source/documentation;
- do not stage or unstage;
- do not commit or push;
- do not switch/create branches;
- do not reset or clean;
- do not rewrite history;
- do not “fix while reviewing.”

Validation commands may create normal ignored build/test artifacts when required by repository tooling.

If a validation operation unexpectedly modifies tracked files, identify the effect immediately. Restore only changes demonstrably produced by that validation operation when doing so is safe and does not overwrite pre-existing user work.

---

## 18. Re-validation after fixes

When asked to validate again:

1. establish the new exact baseline;
2. determine what changed since the previous review;
3. retest every prior material finding;
4. review the new delta for regressions;
5. rerun affected feature-group and architecture checks;
6. perform a fresh adversarial pass rather than only checking previous findings.

A resolved finding does not prove the replacement is correct.

If asked to continue until no further issues are found, keep performing the applicable defined passes until a fresh pass reveals no new material issues. Do not manufacture speculative findings merely to continue indefinitely.

---

## 19. Completion criteria

Do not declare the review complete until, as applicable:

- exact baseline/scope are known;
- every requested diff surface was inspected;
- every changed human-authored file was reviewed;
- consequential documentation claims were checked against the proper source of truth;
- each material feature was reviewed as a coherent unit;
- interacting concepts were reviewed together;
- implementation order was validated;
- complexity/over-engineering was challenged;
- hard-to-reverse decisions were examined;
- external/library/standards claims were researched;
- security/dependency concerns were considered where relevant;
- tests/objective evidence were assessed or run appropriately;
- findings are evidence-backed and non-duplicative;
- a fresh adversarial pass found no additional unreported material issue;
- review limitations are explicit.

This is an exhaustive review standard, not a guarantee that defects are mathematically impossible.

---

## 20. Final report

Lead with one verdict:

- **PASS** — no material changes required in the reviewed scope.
- **PASS WITH NON-BLOCKING FINDINGS** — direction is acceptable; limited improvements remain.
- **CHANGES REQUIRED** — one or more material findings should be corrected before acceptance.
- **BLOCKED** — required baseline/evidence is unavailable or a genuine unresolved decision prevents judgment.

Then report:

### Baseline and scope
Branch/HEAD, base SHA and merge base if applicable, staged/unstaged/untracked surfaces, and exclusions.

### Findings
Ordered by severity with stable IDs and evidence.

### Feature and architecture validation
What was reviewed together and what held up.

### Implementation-order assessment
Required when the change contains a plan or milestones.

### Simplicity/reversibility assessment
Whether complexity is proportionate and whether avoidable future traps remain.

### Validation evidence
Commands, tests, source inspection, authoritative documentation, experiments, and calculations that materially support the conclusion.

### Coverage and limitations
What was reviewed and anything that could not be independently verified.

If changes are required, provide a concise correction plan when useful, but do not implement it unless explicitly requested.
