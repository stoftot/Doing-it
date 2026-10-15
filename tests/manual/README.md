# Reusable manual regression tests

Store human-readable manual test specifications here, grouped by coherent feature
or domain. Prefer several related cases in one document over a file per case.
This template supplies only the format; add actual cases when adopting projects
need durable coverage.

Each case describes repeatable behavior verification. Use stable identifiers
unique within its document so validation can refer to a document path and case ID;
preserve IDs when updating a case and do not reuse retired IDs for other behavior.
Make preconditions, inputs, steps and observable outcomes concrete. Include reset
or cleanup instructions when necessary for safe, repeatable execution.

Copy this generic structure into an appropriate feature/domain document:

```markdown
## <DOMAIN>-001 — <Behavior to verify>

### Purpose
<Behavior or regression this case protects.>

### Preconditions
- <Required environment, account/state and test data.>

### Steps
1. <Action with concrete input.>
2. <Next action or observation.>

### Expected result
- <Observable outcome, including relevant resulting state.>
```

[plan-tests](../../.agents/skills/plan-tests/SKILL.md) identifies coverage needs;
[create-tests](../../.agents/skills/create-tests/SKILL.md) maintains these cases.
[validate-change](../../.agents/skills/validate-change/SKILL.md) selects relevant
cases and performs them through available tooling where possible, identifying
those still requiring a human. Tool-assisted execution of a manual case does not
by itself create a durable automated test.

Keep execution results and temporary plans in the current conversation. These
files contain no run logs, implementation journals or task history.
