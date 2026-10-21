# Architecture

## Boundaries

`DoingIt` owns native MAUI presentation and Android lifecycle integration. Pages use the shared `Ui` palette/components; editors are modal native pages. No WebView or HTML renderer is used.

`DoingIt.Core` owns domain records, recurrence queries, validation, and file persistence. `PlannerStore` is the sole application-state owner. UI code reads snapshots and requests mutations; it does not mutate snapshot arrays. The core has no dependency on MAUI, Android, or external services.

## Data invariants

- The General list (`general`) always exists and cannot be renamed or removed.
- Tasks and routines require a title and may have an unlimited-length description. Descriptions keep their paragraph formatting and appear only in editors and read-only details. Whitespace-only descriptions become absent.
- Core validation limits titles to two explicit lines. The Android store supplies `TaskTitleLayout.Fits` for save validation and legacy migration: it measures Open Sans at the task-row font size, system text scale and the narrowest portrait row width. Rows cap display at two lines if device settings later change; loading version 3 never rejects data merely because its typography no longer fits.
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

Schema version 2 added checklist definitions and occurrence subtask progress. Schema version 3 separates titles from optional descriptions. Versions 1 and 2 are upgraded in memory on load and written as version 3 on the next successful mutation, leaving the original file untouched during load. Legacy `Title` held the old description: text that fits two rendered lines becomes title-only; longer text stays intact in Description with the longest fitting whole-word prefix as Title. If the first word alone is oversized, the title uses its longest fitting Unicode text-element prefix. Older app builds cannot read version 3 files.

Existing invalid data causes a visible load failure and is left intact. The UI offers retry without clearing data. Write errors preserve the in-memory snapshot and display an error. No automatic reset or silent backup recovery is performed.

## Presentation flow

MainPage owns three tab bodies and the completed-task overlay. Task/recurrence editors save through PlannerStore then dismiss; the parent refreshes on appearing. CalendarPage queries selected local dates in month/week views. A timer and window resume hook refresh Home at a local date change.

MainPage and CalendarPage host TaskDetailsOverlay above their existing content, keeping the app visible behind a dimmed backdrop. Task titles open this read-only card, including completed tasks and routine definitions; it shows title, optional description, and either date/list or the routine schedule. The card bounds its scrollable content to the available height. Backdrop, close button and Android Back dismiss it without mutating state. Checklist icons/progress retain the separate interactive checklist action.

ChecklistPage is a modal card for immediate subtask checking, including completed occurrences. It stays open when the final item is checked. Nested task/routine editors use the shared SubtaskEditor for draft title/item changes; closing without saving discards the draft.

Native app views are built in C#; compact snapshot rendering is appropriate for personal lists. There is no backend, dependency injection framework, notification scheduler, or separate view-model layer in this version.

## Android launcher widgets

Three fixed-size AppWidgetProvider entries expose remaining tasks (1×1), completed/total (2×1), and create task (1×1). Android XML RemoteViews layouts use the app’s palette; widget_edit.xml mirrors edit.svg as a native vector for crisp scaling. TodayProgress counts PlannerQueries.ForDate, including checklist parents once and excluding list-only tasks.

PlannerStore.Changed fires after a successful persisted mutation. App schedules an asynchronous refresh; the widget updater serializes read-only loads of planner.json and updates every placed instance. Providers can run without a MAUI window. Unreadable data displays an open-app state rather than zero/completed tasks. Periodic updates, an inexact non-waking local-midnight alarm, reboot/package updates and date/time-zone changes refresh counts; Android idle policy can delay background execution. Removing the last count widget cancels the alarm.

Immutable explicit activity PendingIntents distinguish Home and Create task. MainActivity consumes the action once; MainPage queues navigation until loading succeeds, returns to Home, then optionally opens today’s TaskEditorPage. Repeated create taps reuse that shortcut’s already-open editor. Load failure keeps the request pending for retry and never opens an editor over empty fallback data.
