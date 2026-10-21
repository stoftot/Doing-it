# Project Brief

## Purpose and users

Doing it is a personal Android to-do app for organizing today's tasks, persistent lists, and recurring routines. The UI follows the supplied rounded-card sketches with teal `#1D4C5E` and blush `#D5BBB1`.

## Goals and capabilities

- Three bottom navigation destinations: Home, Recurring, and Lists.
- Home contains tasks for the local calendar date plus all personal lists.
- Task completion hides active rows; completed popups support Undo.
- Required two-line task titles, optional unlimited descriptions, and read-only details popups that keep the app visible behind them.
- Daily, selected-weekday, and selected-day/selected-month recurrence; routines can be edited or paused.
- Shared month/week calendar for dated tasks and routine occurrences.
- Permanent General list, custom named lists, task/list editing.
- To-do list tasks with subtasks, completed/total progress and a checklist popup, available in lists, on dates and as recurring routines.
- Data survives app restarts and is available offline.
- Android launcher widgets for today’s remaining count (1×1), completed/total (2×1), and quick task creation (1×1).
- Opt-in silent daily task summaries, local-midnight delivery, live completion updates and optional sticky lock-screen notifications within Android's permission/privacy limits.

## Constraints and non-goals

Native .NET MAUI Android only. No web development, server, account, synchronization, or iOS/desktop targets in this version. All data is local to the Android app. Dates use the phone's local calendar; there are no times of day or time zones attached to tasks. Notification delivery uses the phone's current local midnight.

## Enabled technology modules

| Module | Enabled | Facts |
| --- | --- | --- |
| .NET | Yes | [DOTNET](modules/DOTNET.md) |
| MAUI/Android | Yes | [MAUI_ANDROID](modules/MAUI_ANDROID.md) |

## Product choices

- Dated tasks and list tasks are mutually exclusive.
- Recurring tasks start on their creation date. Missing monthly dates are skipped.
- Removing custom lists moves their tasks to General, preserving completion state.
- English UI, light palette, local-only storage. These are initial implementation choices, not requirements for future versions.

The root [README](../../README.md) owns current usage and build instructions.
