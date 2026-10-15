---
name: implement-change
description: Implement features, refactors, configuration changes, or fixes with an understood cause, including durable regression coverage, validation, context maintenance and release relevance. Route unexplained failures to debug-problem first.
---

# Implement a change

Use for end-to-end implementation. For initialization use `initialize-project`;
for a review-only request use `review-change`. Keep planning in the current
conversation; no persistent task plans or progress files.

## Scope and implementation

1. Establish the intended outcome and observable acceptance criteria from the
   request. Identify important compatibility constraints and separate assumptions
   from confirmed requirements.
2. For unfamiliar/substantial work, start with
   [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md). Read only relevant architecture,
   conventions, decisions and enabled-module facts, following authoritative links.
3. Inspect current code/configuration and related tests. If a failure's cause is
   unclear, use [debug-problem](../debug-problem/SKILL.md) before selecting edits.
   Before editing, detect Git; when present, capture the starting revision (if any) and
   working-tree/index state under [AGENTS.md's Git policy](../../../AGENTS.md#create-meaningful-local-git-history).
4. Choose the smallest coherent change, affected boundaries and proportionate
   validation. Include an observable behavior check when source/build evidence
   alone cannot establish the requested outcome. Use applicable installed adapters
   for technology execution; core work does not require any adapter.
5. Implement within the authorized scope. Respect generated files and unrelated work.
6. After adding, removing, fixing or materially changing observable behavior, use
   [plan-tests](../plan-tests/SKILL.md) to determine needed durable automated/manual
   regression coverage, then [create-tests](../create-tests/SKILL.md) for warranted
   additions or updates. If existing coverage is sufficient, continue to validation.
   Nonbehavioral edits and already-covered mechanical changes normally need no
   dedicated planning pass. Keep the handoff in the conversation, not a plan file.

## Validate, commit and finish

Apply steps 1-4 as each coherent unit is completed, using the global Git policy
for task-size boundaries and ownership safety. This allows progressive commits
on large tasks and normally one final commit on small tasks.

1. Execute [validate-change](../validate-change/SKILL.md). Fix failures caused by
   the change and rerun affected checks. For ambiguous failures, return to evidence
   gathering; do not repeatedly guess/edit/build. Identify pre-existing failures
   and unavailable dependencies explicitly instead of concealing them.
2. Check [maintain-project-context](../maintain-project-context/SKILL.md) for durable
   structural, architectural, convention or module changes. Update only affected
   canonical owners and validate any changed references.
3. **Only after the outcome is completed and sufficiently validated**, assess its
   release relevance under [RELEASES](../../../docs/ai/RELEASES.md), applying its
   project-only scope. Skip release updates for template-only work; for mixed work,
   assess only the project outcomes. If worthy, follow its pending-record procedure:
   identify latest released and current pending
   records, classify the bump, create/escalate the single pending record, preserve
   every accumulated outcome, and recheck its invariants. RELEASES owns the rules;
   do not maintain another version algorithm here. Incomplete attempts do not
   become release notes. Material outstanding behavior verification may mean the
   outcome is not yet sufficiently validated.
4. Inspect the final change set (or compare intended files when Git is unavailable).
   Check for unrelated/generated edits, broken references and unfinished procedure
   scaffolding. Do not publish, tag or mark a release as released in this workflow.
   In Git, stage and commit the completed unit according to the global policy,
   including its warranted tests, context and release updates. Verify the resulting
   commit and remaining status; report any work left uncommitted and why.
5. After all units, perform final task validation and inspect the full task change
   set, including earlier commits against the captured baseline when in Git, while
   distinguishing unrelated concurrent work. Process resulting fixes through steps 1-4;
   intermediate checks do not replace this pass.
   For a single unit, the checks just completed can satisfy this final pass.
6. Report the result, affected areas, actual commands/scenarios and outcomes, any
   remaining uncertainty, and whether release/context maintenance was warranted.
   Distinguish edits, compilation, passing tests, launch, reproduction and observed
   runtime/browser/device behavior; do not collapse them into a generic verified claim.
