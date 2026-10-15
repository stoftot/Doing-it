---
name: validate-change
description: Select and execute automated checks and relevant existing manual regression cases for a change or claimed fix, distinguishing static, test, runtime, browser, device and human-required evidence. Use for verification; investigate unclear causes with debug-problem and implement with implement-change.
---

# Validate a change

This skill owns validation selection and evidence reporting. Technology adapters
own build/launch/browser/device mechanics and read their project-specific facts.
Do not require tools or adapters that the project does not use.

## Select checks

1. Inspect the request, changed areas and expected observable outcome. For
   unfamiliar work start at [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md); read
   the relevant test layout, conventions and applicable module targets only.
2. Identify what could regress, available checks, required environments and the
   evidence needed to support completion. Separate observed setup from assumptions.
   Detect commands/MCP capabilities before selecting checks that depend on them.
3. Inspect existing manual specifications in
   [tests/manual](../../../tests/manual/README.md). Select cases covering affected
   behavior and likely regressions; identify each by document path and case ID.
   Distinguish cases executable with available tooling from those needing a human.
   If coverage is missing, report the gap to the requesting workflow; use
   [plan-tests](../plan-tests/SKILL.md) and [create-tests](../create-tests/SKILL.md)
   when durable additions are warranted. This skill owns selection and execution
   of existing cases, not a separate test-chooser workflow.
4. Climb this conceptual ladder only as far as the behavior and risk require:

   Targeted checks → relevant automated tests → build/static validation →
   runtime verification → browser/device/UI verification.

   This is an evidence ladder, not a rigid execution order; build first if a test
   or launch needs artifacts. Not every change needs every level.

| Change | Typical useful evidence |
| --- | --- |
| Documentation / workflow | Correct links/metadata, coherent ownership and scenarios, no invented facts |
| Formatting / style | Applicable formatter/static checks; do not add meaningless behavior tests |
| Internal C# logic (when applicable) | Targeted tests and relevant build |
| ASP.NET UI interaction (when applicable) | Relevant automated tests plus real app/browser interaction |
| MAUI device behavior (when applicable) | Build, relevant tests and actual target/runtime evidence |
| Runtime bug | Repeat original failing scenario and appropriate regression checks |

## Execute and interpret

1. Run selected checks and relevant automated tests using canonical repository
   targets or the applicable adapter. Run the full practical automated suite by
   default when execution time
   and environment requirements are reasonable. For expensive suites, use targeted
   tests during iteration and broader regression validation before completion where
   practical. Consider external dependencies, browser/device availability and
   environmental cost, not model-token cost alone. Explain omitted scope.
   Record command/scenario, target/configuration and outcome in the conversation;
   wait for completion and inspect exit status/output.
2. Treat application launch as distinct from behavior verification. For browser or
   device changes exercise the affected interaction against real application state.
   A tooling probe alone validates the tool, not the changed application.
   Perform selected manual cases automatically where available technology tooling
   can exercise their steps and verify expected outcomes. Report partial execution
   precisely; unsupported steps or subjective observations remain for a human.
   Tool-assisted execution does not by itself create durable automated coverage.
3. On failure, identify whether the change, pre-existing code or environment is
   responsible. Return change-caused failures to implementation; use
   [debug-problem](../debug-problem/SKILL.md) when cause is unclear.
4. After corrections, rerun affected checks and complete the planned practical
   regression scope. Once it passes, broaden or repeat only for new changes,
   failures or unresolved concerns; avoid full-suite repetition that adds no evidence.
5. Stop/clean up processes, browser/debugger sessions and disposable fixtures
   created for the check. Preserve user-owned running sessions and data.

## Report / hand off

Give an evidence summary (table or short list) with separate categories:

- **Executed:** exact commands/scenarios and their scope.
- **Passed:** observed results, including concrete behavior where checked.
- **Failed:** observed failure and whether attributed to change or baseline.
- **Browser / device checks:** cases actually performed, target and observed outcome;
  distinguish browser evidence from device evidence and from launch alone.
- **Human execution required:** manual document paths/case IDs, remaining steps,
  expected outcomes and why a human is needed. Never count pending cases as passed.
- **Not executed / blocked:** omitted automated/manual scope, unavailable dependencies,
  reasons and remaining confidence gap.

Say None for a category with no entries. Never claim tests, compilation, launch,
reproduction or runtime/browser/device verification that did not occur. Return
results to the requesting workflow; implementation owns release relevance and
context handoff, not this skill.
