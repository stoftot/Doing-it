# Architecture

## Boundaries

`DoingIt` owns native MAUI presentation and Android lifecycle integration. Pages use the shared `Ui` palette/components; editors are modal native pages. No WebView or HTML renderer is used.

`DoingIt.Core` owns domain records, recurrence queries, validation, and file persistence. `PlannerStore` is the sole application-state owner. UI code reads snapshots and requests mutations; it does not mutate snapshot arrays. The core has no dependency on MAUI, Android, or external services.

## Data invariants

- The General list (`general`) always exists and cannot be renamed or removed.
- A TaskItem has exactly one of ListId or ScheduledDate; ListId must reference an existing list.
- Custom-list removal moves tasks to General.
- Weekly recurrence requires at least one weekday. Monthly recurrence requires at least one day (1-31) and month (1-12).
- Routines occur only on/after StartDate and while enabled. Nonexistent calendar dates are skipped.
- Completion for list/dated tasks belongs to the task. Completion for routines belongs to `(RecurringTaskId, Date)`; future occurrences remain incomplete.
- A to-do list has at least one subtask with a stable ID and nonblank title. Its completion is derived from all current subtasks; bulk completion APIs reject it. Normal tasks have no subtasks. The type is fixed after creation.
- List/dated subtask checkmarks belong to the task. Recurring definitions keep unchecked templates; occurrence records store completed subtask IDs. Renames preserve progress, removal prunes those IDs, and additions recalculate completion against the current template on every date.
- Editing or pausing a routine retains existing completion records. Date queries use the current schedule, including when viewing earlier dates.

## Persistence and error handling

One `planner.json` in FileSystem.AppDataDirectory contains versioned records. System.Text.Json source-generated metadata supports Android trimming. A semaphore serializes mutations; a new snapshot becomes visible only after a successful save. Saves write a same-directory temporary file then replace the destination, retaining the previous save as `.bak`.

Schema version 2 adds checklist definitions and occurrence subtask progress. Version 1 is upgraded in memory on load and written as version 2 on the next successful mutation, leaving the original file untouched during load. Older app builds cannot read version 2 files.

Existing invalid data causes a visible load failure and is left intact. The UI offers retry without clearing data. Write errors preserve the in-memory snapshot and display an error. No automatic reset or silent backup recovery is performed.

## Presentation flow

MainPage owns three tab bodies and the completed-task overlay. Task/recurrence editors save through PlannerStore then dismiss; the parent refreshes on appearing. CalendarPage queries selected local dates in month/week views. A timer and window resume hook refresh Home at a local date change.

ChecklistPage is a modal card for immediate subtask checking, including completed occurrences. It stays open when the final item is checked. Nested task/routine editors use the shared SubtaskEditor for draft title/item changes; closing without saving discards the draft.

Native views are built in C#; compact snapshot rendering is appropriate for personal lists. There is no backend, dependency injection framework, notification scheduler, or separate view-model layer in this version.
