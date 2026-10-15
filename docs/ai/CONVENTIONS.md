# Conventions

- Application namespaces are `DoingIt` and `DoingIt.Pages`; platform-independent logic lives in `DoingIt.Core`.
- UI uses native C# MAUI controls with `Ui` helpers; colors and typography belong there. Icons are SVG source assets compiled by MAUI.
- Read PlannerStore.Data as an immutable snapshot. Change state only through its asynchronous methods; show persistence failures and keep forms open.
- DateOnly expresses all scheduling dates. Use the device local date at UI boundaries; core tests supply deterministic dates.
- A task is assigned to one list OR one date. Do not introduce hidden list/date coupling in views.
- Core xUnit tests use isolated temporary files. Device regression specifications belong in `tests/manual/planner.md`; execution logs stay out of source control.
- Use the SDK formatter on changed C# projects. Generated `bin/obj`, APKs, machine paths, and local tooling receipts are not committed.
- The local build script owns the Windows Unicode-path workaround. Do not hardcode a developer machine path into project files.

See [ARCHITECTURE](ARCHITECTURE.md) for state invariants and [README](../../README.md) for build commands.
