---
name: create-tests
description: Create or update durable automated tests and reusable manual regression specifications from a sufficiently clear test plan. Reuse existing test patterns, run changed tests where practical, and escalate ambiguous expectations or production defects rather than changing behavior to make tests pass.
---

# Create durable test coverage

Consume the temporary handoff from [plan-tests](../plan-tests/SKILL.md), or an
equally precise supplied plan. Missing test-design decisions return to planning;
this skill does not silently redefine the behavior to be protected.

## Implement the handoff

1. Inspect the affected tests, fixtures, framework, test targets and repository
   conventions. Reuse established patterns rather than inventing a parallel test
   architecture. Use applicable installed technology adapters for mechanics;
   core work does not require a particular framework, tool or adapter.
2. Add or update the planned automated tests at the chosen level. Prefer updating
   an existing test when that avoids duplication. Assert the specified observable
   outcomes with repeatable inputs and state.
3. Run the tests created or changed where practical. Inspect results and fix test
   implementation mistakes; report unavailable dependencies and unexecuted tests
   explicitly. A written test is not evidence that it passed.
4. Create or update the planned manual cases using
   [tests/manual guidance](../../../tests/manual/README.md). Group by coherent
   feature/domain and preserve stable case IDs when updating behavior. Include
   repeatable preconditions, steps and observable expectations; no task journals
   or execution-result history in these specifications.
5. Return changed test paths/manual case IDs, what they protect, execution results
   and unresolved gaps to the primary workflow. Defer completed-change validation
   and selection of existing manual cases to
   [validate-change](../validate-change/SKILL.md).

## Production behavior and ambiguity

Do not alter production behavior merely to make a test pass, weaken a planned
assertion to accept a defect, or silently replace the requirement with current
behavior. If implementation appears incorrect, expectations conflict with
requirements, the architecture cannot reasonably support the proposed test, or
important behavior is ambiguous, return the specific evidence/question to capable
primary reasoning. Use planning for coverage decisions and the implementation or
diagnostic workflow for production changes; continue independent clear cases.

## Delegation

Prefer cheaper/faster subagents when available for bounded translation of a clear
plan into existing test patterns, straightforward cases, repetitive variants or
structured manual descriptions. Give each agent its precise expectations, allowed
files and required evidence; the primary reviews the returned coverage and results.

Keep or escalate substantial test architecture, ambiguous plans, conflicts between
expected and production behavior, cross-component reasoning and architectural
problems to a stronger model. If delegation is unavailable or its coordination
cost exceeds the benefit, continue normally with the active model.
