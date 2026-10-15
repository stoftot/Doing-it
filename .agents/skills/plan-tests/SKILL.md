---
name: plan-tests
description: Design durable automated and manual regression coverage after observable behavior is added, removed, fixed or materially changed. Inspect existing coverage and hand off gaps to create-tests; skip dedicated planning for nonbehavioral or already-covered mechanical edits.
---

# Plan durable regression coverage

Use after meaningful implementation changes, including business logic, API,
authentication/session, browser or device UI/navigation, persistence, error handling
and boundary behavior. Comment/formatting edits, local renames and mechanical
changes adequately covered by unchanged tests normally need no dedicated pass.
This skill designs coverage; [create-tests](../create-tests/SKILL.md) creates it and
[validate-change](../validate-change/SKILL.md) selects and executes checks now.

## Determine useful coverage

1. Identify the observable behavior added, removed, fixed or changed and its
   acceptance criteria. Inspect the affected implementation and relevant boundaries;
   distinguish confirmed requirements from assumptions.
2. Inspect relevant automated tests and existing manual specifications in
   [tests/manual](../../../tests/manual/README.md). Identify sufficient existing
   coverage before proposing additions; update existing cases where clearer.
3. Identify important regression risks: happy and negative paths, meaningful edge
   or boundary cases, state transitions, integration boundaries and the original
   failure for a bug fix. Prioritize cases that distinguish correct from incorrect
   observable behavior, not tests that mirror implementation details.
4. Classify each useful case as automated, manual or already sufficiently covered.
   Choose the appropriate level: unit, integration, application/runtime, browser,
   or device/UI/manual. Consider repeatability, available infrastructure and
   maintenance cost; avoid redundant cases that add no useful confidence.
5. State inputs/preconditions and observable expected results precisely enough for
   implementation. Resolve material ambiguity with capable reasoning or the user;
   do not invent requirements. Use applicable retained technology adapters for
   feasibility and mechanics, never as dependencies of this core skill.

## Temporary handoff

Keep a concise handoff in the current conversation, including existing test paths
and manual case identifiers where available:

```text
AUTOMATED
- Tests to add/update, behavior each proves, inputs/expectations and preferred level.

MANUAL
- Cases to add/update, preconditions and observable expected behavior.

EXISTING COVERAGE
- Relevant existing tests/cases that remain sufficient and why.

NOT COVERED / LIMITATIONS
- Intentional omissions, unresolved questions or constraints and their reasons.
```

Use None where appropriate. If no additions or updates are warranted, explain why
and proceed to normal validation. Otherwise pass the handoff to `create-tests`.
Do not save test-plan history, progress files or execution logs. Durable outputs
are the automated tests and reusable manual specifications, not this reasoning.

## Model choice

Prefer the capable primary model for substantive test design, prioritization,
edge-case discovery and deciding useful coverage versus noise. Keep ambiguous
requirements and cross-component reasoning on the stronger model; do not delegate
them automatically to a cheaper model. Trivial planning can stay with the active
model without spawning an agent.
