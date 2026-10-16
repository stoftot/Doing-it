# Agent operating contract

## Orient before changing

- Inspect existing implementation and repository context before nontrivial edits.
- For unfamiliar or substantial work, begin with [PROJECT_MAP](docs/ai/PROJECT_MAP.md).
- Read only the architecture, conventions, decisions, and module facts relevant
  to the task; follow references to existing authoritative documentation.
- Separate observed repository facts from assumptions. Mark unknowns explicitly.
- Preserve unrelated work and avoid unnecessary edits to generated/external areas.

## Choose the workflow

- Adopt a repository or define a new project with
  [initialize-project](.agents/skills/initialize-project/SKILL.md).
- Build features, refactors, configuration changes, and understood fixes with
  [implement-change](.agents/skills/implement-change/SKILL.md).
- Investigate an unclear failure with
  [debug-problem](.agents/skills/debug-problem/SKILL.md).
- Select and execute checks with
  [validate-change](.agents/skills/validate-change/SKILL.md).
- Review requested changes with [review-change](.agents/skills/review-change/SKILL.md).
- After meaningful structural or architectural changes, check
  [maintain-project-context](.agents/skills/maintain-project-context/SKILL.md).

## Work from evidence

- Editing files alone does not complete a task. Execute proportionate validation.
- After meaningful behavior changes, consider durable regression coverage with
  [plan-tests](.agents/skills/plan-tests/SKILL.md); add/update warranted automated
  and manual coverage with [create-tests](.agents/skills/create-tests/SKILL.md).
  Validation reuses relevant existing coverage.
- Reproduce failures and gather evidence before selecting a fix; avoid speculative
  edit/build/retry loops. Keep observations, hypotheses, evidence, and conclusions distinct.
- Prefer repository search, filesystem inspection, and CLI when sufficient.
- Use IDE semantics when symbol/project relationships are materially uncertain.
- Use a source debugger when runtime state matters and cheaper evidence is insufficient.
- Use device tooling for actual device behavior.
- Detect installed commands and currently exposed MCP capabilities before using them.
- Tool descriptions are capability guides, not proof of installation or compatibility.
- Optional tooling must not become a hard dependency of a core workflow.
- If tooling is absent, use available evidence and state any remaining validation gap.
- Do not install a tool merely because a skill mentions it.

## Create meaningful local Git history

- Check whether the working directory belongs to a Git repository before applying
  this policy. Outside Git, continue normally; this is not an error. If Git is
  unavailable, report the limitation and continue the work that remains possible.
- Record the initial Git status/diff, including staged changes, before editing.
  Before each staging/commit operation, inspect status and unstaged/staged diffs
  again and distinguish this task's work from pre-existing or concurrent changes.
  Stage only intended files/hunks; inspect the complete staged diff before committing.
  Never include unrelated user changes, including already-staged work, for convenience.
  Leave unrelated work untouched: do not discard/reset it or use destructive Git
  operations to obtain a clean tree. If intermixed changes cannot safely be separated
  or ownership is uncertain, leave affected work uncommitted and report why.
- Create local commits for completed coherent units with one understandable purpose
  and a sensible repository state. Small tasks usually need one final commit;
  medium tasks need one or a few at natural milestones; large tasks should commit
  independently valid sections progressively instead of waiting until the end.
  Optimize for useful history, not commit count or tiny checkpoints.
- Keep implementation with its regression tests, manual test specifications and
  related docs/infrastructure/context updates. Do not split a logical change by
  file type. An independently valid prerequisite refactor or test infrastructure
  improvement may stand alone. Include warranted release updates with the relevant
  unit under the existing release policy; do not invent context/release housekeeping
  commits unless those changes are independently meaningful.
- Perform proportionate validation before committing where practical. Do not commit
  knowingly broken/unusable intermediate work as completed just for a checkpoint;
  if no independently valid boundary exists, keep working and commit later.
  Intermediate commits do not replace final task validation. Report validation gaps.
- Respect established commit-message style, branch workflow and contribution rules
  without weakening ownership safety. Use concise purpose/outcome messages such as
  `Add session expiration handling`; avoid `update files`, `WIP` or `Codex changes`.
  Do not introduce an elaborate new message convention.
- Default to local commits only. Pushing, creating remote branches, opening pull
  requests, merging, rebasing published/user history, amending existing user commits,
  force-pushing and tagging releases require explicit user instruction or a separately
  defined workflow; none is automatic under this policy.
- The primary task owner coordinates commit boundaries and final staging/commit
  decisions. Subagents must not make overlapping commits independently; delegate
  Git history operations, including to cheaper models, only as explicitly bounded work.

## Keep context small

- Headroom is the default large-output aid when exposed. Use only
  `headroom_compress`, `headroom_retrieve`, and `headroom_stats`.
- Compress unusually large logs/output or text needed later; retrieve original
  details before drawing a conclusion that depends on them.
- Leave ordinary small files/output uncompressed. Without Headroom, use bounded
  searches/excerpts and temporary artifacts; it is not durable repository storage.

## Use delegation deliberately

- When the active environment supports model delegation, use cheaper/faster
  models for worthwhile bounded mechanical, low-risk, exploratory, or parallel work.
- Give each subagent a clear question, limited scope, and expected evidence.
- Keep architecture, ambiguous reasoning, cross-cutting debugging, integration
  decisions, and final synthesis on an appropriately capable primary model.
- Review delegated results. Do not delegate merely for the sake of delegation.
- When delegation is unavailable or coordination costs more than it saves,
  continue normally on the primary model without failing the workflow.

## Preserve durable ownership

- Follow the responsibility split in [docs/ai/README](docs/ai/README.md).
- Update context only when durable facts change; reference canonical owners.
- Use Git history for implementation history, decisions for durable rationale,
  current docs for current truth, and release records for version-worthy outcomes.
- Keep task plans in the current conversation. Do not add permanent task history,
  progress files, AI activity logs, observability, or session telemetry infrastructure.
- After completed validation, assess project-only release relevance using
  [RELEASES](docs/ai/RELEASES.md); template maintenance does not produce project releases.
  Preserve the single pending record and its accumulated notes. Publishing is separate.

## Apply technology modules conditionally

- Technology adapters apply only when their files and corresponding projects exist,
  or when the requested task explicitly concerns that technology's toolchain.
- Discover enabled modules in [PROJECT_BRIEF](docs/ai/PROJECT_BRIEF.md); read their
  facts and procedures only as needed. The core remains technology-neutral.
- Removing a module must not require installing its tools to use the core.

## Shared Android testing (this repository)

This project explicitly opts into the shared Windows AndroidTesting environment.
Before Android build/deploy/UI testing, resolve `ANDROID_TESTING_HOME` (process,
then user environment); if unset, the known host-specific location is
`C:\Users\chris\DevTools\AndroidTesting`. Read its entire `README.md`, `version.json`
and relevant `CHANGELOG.md` entries. Follow [project integration](docs/android-testing.md)
and [Android facts](docs/ai/modules/MAUI_ANDROID.md). Other hosts need their own
verified installation; a missing/invalid configured path is an error, not a reason
to install tools or silently select another installation.

AndroidTestingReviewedRevision: UNREVIEWED

- Keep this as the single review marker. No previous review is claimed by
  `UNREVIEWED`: use `Get-AndroidTestingChanges.ps1` and its explicit
  `-SinceRevision 0` full-history query. For a valid recorded integer, query changes
  with that exact `-SinceRevision` value when it differs from current. Investigate
  invalid/future values and mismatched installations; never lower the marker to
  hide a discrepancy. Review useful improvements even if older commands still work.
- Advance the marker only after relevant review and appropriate validation;
  recheck the current revision immediately before recording completion and review
  any intervening entries. Preserve the previous value while required validation
  is incomplete. The project UI harness and reserved runner validation described
  in [verification](docs/verification.md#android-project-verification) remain pending.
- Reuse existing devices. Use `Invoke-AndroidTests.ps1` for coordinated emulator
  tests with explicit app assertions, worker sessions and allocated ports. Keep
  tests, app IDs and any future device-selection config here. Do not create devices
  automatically, bypass reservations, reset/delete data, or stop shared services
  as routine startup/cleanup. Physical phones need the separate documented route.
- Only modify this repository for project integration. Never modify, copy into
  this repository, install, upgrade, repair or relocate the shared implementation,
  configuration, presets, metadata or documentation. Do not install duplicate
  SDK/JDK/emulator/Appium/driver tooling. Report external-tool problems to the user.
- Documentation maintenance uses static inspection and read-only revision checks;
  do not launch services or change shared state merely to validate instructions.

## Report completion precisely

- Summarize what changed, why, and what was actually executed and observed.
- Distinguish edited, compiled, tests passed, launched, reproduced, and behavior
  verified in runtime/device. Never imply a stronger level than observed.
- Name failures and unavailable checks with reasons and their practical impact.
