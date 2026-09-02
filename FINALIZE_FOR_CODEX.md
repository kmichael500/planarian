# Finalize for Codex Protocol

**Status:** Normative project procedure
**Version:** 1.0
**Effective date:** 2026-08-14
**Trigger:** The user says **“Finalize this for Codex”** or gives an unmistakable direct variant of that command.

## Authority and activation

This document is the authoritative project procedure whenever the trigger is invoked.

The purpose of finalization is not merely to produce a polished implementation plan. The purpose is to maximize the probability that a capable OpenAI Codex agent can correctly implement the intended change in one autonomous pass while detecting stale assumptions before they cause bad changes.

When this protocol is active:

1. Treat the conversation’s approved product and architecture decisions as **normative task truth**.
2. Treat the exact inspected repository, branch, commit, working tree, archive, or uploaded source snapshot as **current implementation truth**.
3. Independently inspect and research anything that can reasonably be resolved without user preference.
4. Do not call the handoff implementation-ready while a blocking product, UX, policy, security, or architecture decision remains unresolved.
5. Produce the smallest sufficient handoff.
6. End with exactly the artifacts the user must attach to Codex, followed by one clearly identified **Paste into Codex** prompt.

This procedure governs the finalization workflow. It does not authorize overriding higher-priority platform instructions or an explicit later user instruction that deliberately changes the task.

---

## 1. Research and validation before finalization

Before producing the final handoff, independently resolve every question that can reasonably be answered from:

- the source code or source snapshot provided or made available;
- repository documentation;
- tests and existing implementation patterns;
- authoritative product, library, framework, and platform documentation;
- relevant standards;
- current primary research where appropriate.

Prefer primary and authoritative sources for technical decisions.

Use available repository, filesystem, GitHub, connected-source, and web-research tools as appropriate before asking the user anything.

Do not ask the user questions that further repository inspection or reasonable research can answer.

If multiple technically valid outcomes remain and choosing among them is genuinely a product, UX, policy, security-boundary, compatibility, or architecture decision requiring human preference, surface that decision instead of silently inventing one.

Do not call a handoff implementation-ready while it contains an unresolved blocking product or architecture decision.

---

## 2. Source-of-truth discipline

Treat the exact repository, branch, commit, working tree, archive, or uploaded source snapshot used for the analysis as the implementation baseline.

Record the baseline precisely in the handoff.

When available and relevant, record:

- repository identity;
- branch;
- commit SHA;
- working-tree state;
- relevant uncommitted changes;
- source snapshot filename;
- archive or artifact fingerprint, preferably SHA-256;
- retrieval or inspection date.

If the source is an uploaded archive or snapshot, identify and fingerprint the exact artifact when possible.

Distinguish explicitly between:

- **Current implementation truth** — what the inspected source tree currently does.
- **Normative task truth** — what the approved handoff requires it to become.

Validate the final plan against the actual baseline immediately before delivering it.

Do not invent filenames, symbols, APIs, data structures, tests, commands, package versions, or architecture from memory when they can be verified from the source.

If the source available for finalization is incomplete, say exactly what was inspected and limit claims accordingly. Do not represent an inferred or partial baseline as a verified complete repository baseline.

---

## 3. Choose the smallest sufficient handoff

Choose the handoff class automatically.

### 3.1 Inline handoff

Use for focused, well-understood changes where all necessary information can be safely carried in one reasonably sized standalone Codex prompt.

**Deliverable:** only the final standalone Codex prompt. No attachment is required.

### 3.2 ExecPlan handoff

Use for substantial multi-file features, refactors, migrations, or difficult fixes where a standalone execution plan materially improves reliability and restartability.

**Deliverables:**

- one self-contained execution-plan artifact, normally Markdown; and
- one short Codex starter prompt telling the agent how to use that exact artifact.

Prefer a descriptive filename such as `CODEX_EXEC_PLAN_<task>.md`. Use the actual generated filename in the starter prompt.

### 3.3 Research package

Use for cross-cutting architecture, data-model changes, security-sensitive work, new subsystems, major technology decisions, multi-milestone work, or anything whose research and specification would unnecessarily crowd the implementation prompt.

**Deliverables:**

- one structured ZIP package containing the final authoritative package; and
- one short Codex starter prompt referring to that exact ZIP filename.

Do not create a large package merely for ceremony.

---

## 4. Context discipline

For large handoffs, optimize for progressive disclosure.

Do not instruct Codex to load every file into context before beginning.

A research package must contain a small `00_START_HERE.md` that identifies:

- the task;
- the authoritative sources;
- the mandatory initial reading set;
- conditional documents and exactly when each becomes relevant;
- informational or research material that should not be preloaded.

Keep normative instructions distinct from supporting research.

Research is evidence. It does not override an approved product or architecture decision.

Avoid duplicating the same normative information manually across many documents.

Where both machine-readable and human-readable forms exist:

- designate one canonical source;
- mark derived representations as derived;
- ensure generated views do not become competing sources of truth.

---

## 5. Authority and conflicts

The handoff must define an explicit authority model appropriate to its size.

Within a substantial task package, distinguish at minimum:

- task scope;
- applicable repository instructions;
- fixed decisions;
- invariants;
- requirements and constraints;
- acceptance criteria;
- execution-plan implementation guidance;
- research and supporting evidence.

If repository guidance and the task handoff appear to conflict, determine whether the handoff explicitly supersedes the repository default for this task.

Do not silently resolve a consequential conflict.

Minor implementation drift is not automatically a blocker.

Codex may adapt stale incidental details such as exact private file placement, helper organization, or test location when the current source proves the plan stale, provided the adaptation preserves the approved intent.

Codex must not silently alter:

- fixed decisions;
- requirements;
- externally observable behavior;
- data semantics;
- security boundaries;
- compatibility constraints;
- non-goals;
- deliberately deferred scope.

When a consequential conflict requires human judgment, Codex should stop and report the conflict precisely rather than inventing a resolution.

---

## 6. Requirement quality

For substantial work, give normative requirements stable identifiers.

Requirements must be:

- singular;
- clear;
- independently understandable;
- traceable to their rationale or source;
- verifiable.

Distinguish concepts instead of calling everything a requirement. Use appropriate classifications such as:

- requirement;
- constraint;
- fixed decision;
- invariant;
- assumption;
- non-goal;
- deferred feature;
- future-compatibility constraint;
- open decision.

A future-compatibility requirement must state clearly that the future feature is **not** being implemented now.

Do not create speculative negative requirements merely because something happened to be removed during earlier development.

Include negative requirements and non-goals only when they guard a realistic ambiguity, regression, compatibility boundary, or scope-creep risk.

---

## 7. Traceability

For consequential work, maintain useful traceability through the chain:

**research or source rationale → approved decision → requirement/invariant → implementation milestone or task → verification/acceptance evidence**

Do not impose bureaucratic traceability on trivial changes.

Traceability may be represented as a concise table, structured Markdown, or canonical machine-readable data when the package warrants it.

---

## 8. Research records

When research materially informs a decision, record enough provenance to understand it later:

- research question;
- conclusion;
- authoritative source;
- relevant product/library/framework/version/date;
- retrieval date when freshness matters;
- confidence or remaining uncertainty;
- decision, invariant, constraint, or requirement informed by the finding.

Summarize relevant findings instead of dumping large source documents into agent context.

If a finding is version-sensitive, mark it accordingly.

Prefer links, identifiers, versions, document titles, and concise conclusions over copied source text.

---

## 9. Architecture decisions

Create an ADR only for consequential decisions that are expensive, risky, security-relevant, compatibility-sensitive, or confusing to reverse.

Do not create ADRs for routine implementation choices.

An ADR should identify:

- decision;
- context;
- meaningful alternatives considered;
- rationale;
- consequences and tradeoffs;
- relevant requirement or invariant IDs.

---

## 10. Execution-plan quality

A Codex execution plan must be implementation-ready rather than aspirational.

Use repository-relative paths and exact symbols where useful and verified.

Explain how affected subsystems fit together sufficiently for implementation.

Separate required work from optional improvements.

State explicitly what is out of scope when scope ambiguity is realistic.

Use incremental milestones when the task benefits from them.

Each milestone must produce an observable, independently verifiable improvement.

Include migration, retry, rollback, recovery, compatibility, or data-transition handling when the task actually requires it.

Prefer safe and idempotent procedures.

Do not have Codex redesign already-settled architecture merely because another approach is possible.

When exact implementation details have been verified from the baseline, include them. When incidental details may reasonably drift, describe the required behavior and the verified current location without turning incidental placement into a fixed decision.

---

## 11. Testing and objective evidence

Define how correctness will be demonstrated **before implementation begins**.

Use the verification methods appropriate to the task, including as applicable:

- focused unit tests;
- integration tests;
- regression tests;
- end-to-end behavior;
- type checking;
- linting;
- builds;
- migration validation;
- performance measurement;
- security checks;
- manual or device acceptance where automation cannot prove behavior;
- architectural inspection for non-runtime compatibility constraints.

For bug fixes, reproduce the relevant failure first when practical.

Do not equate “it compiles” with successful validation.

Identify exact validation commands when they can be determined from the repository.

For every material validation step, define the expected successful behavior or evidence, not merely the command to run.

If repository-wide validation is impractical or known to contain unrelated failures, distinguish:

- task-specific required validation;
- broader validation attempted;
- pre-existing or unrelated failures.

---

## 12. Security and dependencies

Include a threat model, dependency review, security workstream, or supply-chain analysis only when the task warrants one.

Investigate explicitly when the change affects any of the following:

- trust boundaries;
- credentials or secrets;
- remote or untrusted input;
- authentication or authorization;
- sensitive data;
- externally reachable services;
- cryptography;
- sandboxing or privilege boundaries;
- new third-party dependencies;
- dependency upgrades with material behavioral or security implications.

Do not generate security paperwork merely because a template exists.

If a dependency is proposed or changed, verify the relevant current version, compatibility, licensing or policy constraints when material, and whether the repository already has an established alternative.

---

## 13. Agent execution behavior

The final Codex starter prompt must make the task mode explicit: implementation, review-only, planning-only, investigation, or another clearly stated mode.

For an approved **implementation** handoff, Codex should:

1. read the mandatory entry documents;
2. verify package integrity when applicable;
3. verify the repository or source baseline and applicable repository instructions;
4. perform a preflight against the current working tree;
5. stop only for a genuinely blocking conflict requiring human judgment;
6. otherwise proceed directly with implementation without asking for routine confirmation;
7. validate each meaningful milestone;
8. run the complete required validation;
9. inspect the final diff for regressions, accidental scope, generated junk, and unintended repository changes;
10. report objective evidence, baseline differences, and any plan deviations.

Do not ask the user for “next steps” between ordinary milestones.

Use subagents when available for genuinely independent, read-heavy work such as:

- architecture exploration;
- test-gap analysis;
- security review;
- documentation or standards verification;
- final review.

Avoid unnecessary parallel write-heavy implementation when overlapping edits would create coordination risk.

---

## 14. Scope control

Do not opportunistically implement unrelated cleanup or speculative improvements.

If useful improvements are discovered but are not required for the approved task, record them separately rather than silently expanding scope.

Do not add handoff artifacts, research files, validators, manifests, or temporary planning documents to the product repository unless the task explicitly calls for repository documentation changes.

Preserve any task-specific Git restrictions exactly.

Do not commit, push, open a pull request, alter branches, rewrite history, or modify unrelated working-tree changes unless the task explicitly authorizes those actions.

---

## 15. Large-package structure

When a research package is warranted, use a structure approximately like:

```text
00_START_HERE.md
01_TASK.md
02_FIXED_DECISIONS.md
03_INVARIANTS.md
04_NON_GOALS_AND_DEFERRED.md
05_EXECUTION_PLAN.md
06_ACCEPTANCE.md
07_RISKS.md

requirements/
architecture/
research/
security/
machine/
```

Include only the files and directories actually useful to the task. Do not target a fixed number of files.

For sufficiently consequential packages, include machine-readable integrity and traceability material as useful, such as:

- manifest;
- canonical requirements data;
- traceability output;
- schema validation;
- package-validation script;
- SHA-256 checksums.

Derived files must not become independent competing sources of truth.

The package validator, when included, should verify package-internal integrity and references without depending on the product repository unless that dependency is explicitly required and documented.

---

## 16. Final readiness gate

Before delivering the handoff, review it adversarially.

Confirm all applicable points:

- the baseline is correct and precisely identified;
- the plan matches the actual current source;
- no blocking decision remains unresolved;
- fixed decisions and implementation flexibility are clearly separated;
- requirements are not contradictory;
- relevant edge cases are covered;
- verification proves the requested behavior;
- future compatibility is not confused with present scope;
- package references resolve;
- unnecessary context and duplicated instructions have been removed;
- no implementation step depends on an unstated assumption that could reasonably have been researched;
- repository instructions and task-specific supersessions are explicit;
- Codex will know exactly when it is done.

For large packages:

1. run the included handoff validator, if one is warranted;
2. freeze package contents;
3. generate final checksums after the content is frozen;
4. verify the archive contains the intended files and no accidental temporary material.

Do not mark the handoff implementation-ready if this gate fails.

---

## 17. Required final delivery to the user

When the handoff is ready, do not make the user reconstruct what to do from the discussion.

Give the user exactly:

1. the artifact or artifacts they must attach to Codex, if any; and
2. one clearly identified **Paste into Codex** prompt.

The Codex prompt must:

- be standalone and task-specific;
- identify every required attached artifact by its actual filename;
- be short when the package already contains the implementation detail;
- not duplicate the full execution plan;
- not require Codex to repeat research already completed and captured authoritatively in the handoff;
- state the execution mode;
- require baseline/preflight verification;
- instruct Codex to proceed without routine confirmation unless a genuine blocking conflict requiring human judgment is found;
- require complete validation, final diff inspection, and an evidence-based completion report.

If no attachment is required, put everything Codex actually needs into the standalone prompt.

The user should be able to attach the provided artifact, copy the one prompt verbatim, and start the Codex task without composing or interpreting additional instructions.

---

## 18. Final output discipline

For an **Inline handoff**, deliver only the standalone Codex prompt unless the user explicitly asks for commentary.

For an **ExecPlan handoff** or **Research package**, keep explanatory framing minimal. Present the downloadable attachment(s), then the single **Paste into Codex** prompt.

Do not include competing starter prompts, alternate prompt variants, or a second set of “optional instructions.”

Do not call an artifact “final” or “implementation-ready” until the readiness gate has passed.
