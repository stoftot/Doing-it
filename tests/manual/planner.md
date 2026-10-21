# Planner device regression cases

Use a test installation of the Android app. Dates in these cases are relative to the device's current local date. Give each created item a distinctive, run-unique `QA` prefix, and remove only that run's items after the case unless a later case explicitly uses them. Account for pre-existing items when checking counts; do not delete earlier `QA` data unless ownership is established. Clearing app data requires separate explicit authorization even for a dedicated test installation; never reset shared data automatically to satisfy a case.

Follow [shared Android integration](../../docs/android-testing.md) for emulator reservations, suite/session ownership and diagnostics. These are manual specifications, not an existing Appium harness. Preserve shared services and report unmet preconditions/skipped cases. Use a separately coordinated physical phone for OEM behavior; Google APIs emulator evidence does not establish Nothing OS coverage.

## PLANNER-001 — First launch and main navigation

### Purpose
Confirm a fresh planner has its permanent list and all three main sections are reachable.

### Preconditions
- Use an already empty dedicated test installation, or separately obtain explicit authorization to clear only that installation's app data. Otherwise skip the fresh-state assertions and report the unmet precondition. Launch the app.

### Steps
1. Observe Home and its Today's tasks and Your lists sections.
2. Tap Recurring, then Lists, then Home in the bottom navigation.
3. Open the calendar from Home and close it.

### Expected result
- Home shows an empty day, the General list, and no task rows.
- Recurring shows zero active routines and an invitation to create one.
- Lists shows General and no custom lists or tasks.
- Each navigation tab displays its own section; the calendar opens and closes without losing the selected tab or creating data.

## PLANNER-002 — Create, rename, and delete a custom list

### Purpose
Verify list names, management controls, task migration, and protection of General.

### Preconditions
- Open Lists in a test installation containing only General, or account for existing lists and use unique test names. Remove earlier fixtures only if they belong to this run.

### Steps
1. Tap `+ New list`, enter `QA Errands`, and tap Create.
2. In the new list, tap `+ Add task`, enter `QA Buy paper`, leave `In a list` selected, and tap Add task.
3. Tap Manage on `QA Errands`, choose Rename list, change the name to `QA Shopping`, and save.
4. Tap Manage on `QA Shopping`, choose Delete list, and confirm Delete list.
5. Observe General in Lists and Home; look for a Manage control on General.

### Expected result
- The list creation prompt asks only for a name; the new list appears once.
- Rename changes the list heading without losing its task.
- Deleting the custom list removes its card and moves `QA Buy paper` to General.
- General remains present and has no Manage control for renaming or deletion.

### Cleanup
- Edit `QA Buy paper`, choose Delete task, and confirm.

## PLANNER-003 — Edit, complete, and undo a list task

### Purpose
Confirm list tasks persist in their list until completed and remain recoverable.

### Preconditions
- Start with an empty General list on the dedicated test installation.

### Steps
1. In General, tap `+ Add task`, enter `QA File receipt` as the task title, leave `In a list` selected with General, and tap Add task.
2. Tap the task's edit icon, change its title to `QA File invoice`, and save changes.
3. Check the task's completion box.
4. Tap the `✓ 1 done` control for General, tap Undo beside the task, and close the Completed popup.
5. Open Home and inspect the General card.

### Expected result
- The edited title appears in General and the task is also visible in General on Home.
- Checking the task hides it from the active list and raises its completed count.
- The Completed popup shows the task; Undo returns it to the active list and resets the count.
- No dated task is created in Today's tasks or the calendar.

### Cleanup
- Delete `QA File invoice` through its edit page.

## PLANNER-004 — A dated task belongs to exactly one calendar day

### Purpose
Confirm a scheduled task appears on its selected date, without entering a list.

### Preconditions
- The device's local date is correct. General has no `QA` tasks.

### Steps
1. On Home, tap `+ Add a task for today`, enter `QA Today` as the task title, and tap Add task.
2. Open the calendar and select today. Then select tomorrow.
3. On tomorrow, tap `+ Add task for this day`, enter `QA Tomorrow` as the task title, verify `On a date` and tomorrow are selected, and tap Add task.
4. Switch between today and tomorrow in the calendar, close it, and inspect Home and General.

### Expected result
- `QA Today` appears in Home's Today's tasks and on today's calendar date only.
- `QA Tomorrow` appears on tomorrow's calendar date only and does not appear in Today's tasks today.
- Neither task appears in General or another list.

### Cleanup
- Delete both tasks from their respective calendar dates.

## PLANNER-005 — Calendar month and week navigation

### Purpose
Verify period navigation, date selection, and return to today.

### Preconditions
- Open the calendar from Home or Lists.

### Steps
1. In Month mode, note the month heading; tap Next period, then Previous period.
2. Select a visible day and inspect the date heading and planned task rows below the grid.
3. Tap Week; tap Next period and Previous period.
4. Tap Today, then switch back to Month.

### Expected result
- Month arrows move by one month and return to the original month; Week arrows move by one week and return to the original week.
- The selected date and its task rows stay in sync in both views.
- Today selects the device's current local date; Month and Week remain usable afterward.

## PLANNER-006 — Daily, weekly, and monthly schedules

### Purpose
Confirm each recurrence type appears only on its chosen dates, including nonexistent month days.

### Preconditions
- Open Recurring with no `QA` routines. Use the device's correct local date as the start date.

### Steps
1. Create a routine titled `QA Daily` with Daily selected.
2. Create a routine titled `QA Weekly` with Weekly selected; choose Monday and Wednesday and leave the other weekdays unselected.
3. Create a routine titled `QA Monthly` with Monthly selected; choose day 31 and months February and March only.
4. Use the calendar to inspect the next Monday, Wednesday, and Thursday, plus the next February 28 or 29 and March 31 after today.
5. Inspect today and tomorrow for `QA Daily`.

### Expected result
- `QA Daily` appears every day from its creation date onward.
- `QA Weekly` appears on Mondays and Wednesdays from its creation date onward and is absent on Thursdays.
- `QA Monthly` appears on March 31 and is absent throughout February, including its last day; it is not shifted to a different day.
- Recurring rows are identified as recurring and do not appear in list cards.

### Cleanup
- Delete the three `QA` routines through Edit routine.

## PLANNER-007 — Required task titles and schedule choices

### Purpose
Confirm invalid entries show readable errors and create no partial items.

### Preconditions
- Open Lists and Recurring in the dedicated test installation.

### Steps
1. In Lists, tap `+ New list`, enter only spaces, and tap Create. Dismiss the error.
2. Create a list named `QA Unique`; then try to create `qa unique` and dismiss the error.
3. Open `+ New task`, leave the title blank, and tap Add task. Dismiss the error and close the editor.
4. On Recurring, open `+ Create recurring task`, leave the title blank, and tap Create routine. Dismiss the error.
5. Enter `QA Invalid weekly` as the routine title, choose Weekly, deselect every weekday, and try Create routine. Dismiss the error.
6. Choose Monthly, deselect every selected month, and try Create routine. Then select one month, deselect every selected day, and try again.

### Expected result
- Each invalid save shows a readable `Couldn't save` message and leaves the editor open where applicable.
- No blank or duplicate list, blank task, or invalid routine appears after closing the editor.
- The original `QA Unique` list remains unchanged.

### Cleanup
- Delete `QA Unique`.

## PLANNER-008 — Edit, pause, resume, and complete separate occurrences

### Purpose
Confirm a routine's schedule can change and each date keeps its own completion state.

### Preconditions
- Open Recurring with no `QA` routines. Keep the device date unchanged during the case.

### Steps
1. Create a routine titled `QA Stretch` with Daily selected.
2. Tap Edit routine, change its title to `QA Stretch gently`, and save changes.
3. In the calendar, complete its occurrence today; select tomorrow and complete that occurrence too.
4. Return to today, show completed tasks with the `done` control, and undo only today's occurrence.
5. Verify tomorrow remains completed. On Recurring, turn off the `Enable QA Stretch gently` switch, inspect today and tomorrow in the calendar, then turn it on again.

### Expected result
- The renamed routine appears once, with its daily schedule intact.
- Undoing today's occurrence restores only today's active row; tomorrow remains completed.
- Pausing shows the routine as Paused and removes its occurrences from the calendar while disabled.
- Resuming restores the occurrences with their prior per-date completion states.

### Cleanup
- Delete `QA Stretch gently` through Edit routine.

## PLANNER-009 — Cold launch keeps saved data without duplication

### Purpose
Verify a process restart reads existing planner data once and retains edits and completion state.

### Preconditions
- Use a test installation without `QA Persist` items.

### Steps
1. Create list `QA Persist`; add a list task titled `QA Note`, a dated task titled `QA Appointment` for tomorrow, and a daily routine titled `QA Check in`.
2. Complete `QA Note` and today's occurrence of `QA Check in`.
3. Fully stop the app process, then launch it again without clearing app data.
4. Inspect Home, Lists, Recurring, and today and tomorrow in the calendar.

### Expected result
- Each list, task, and routine appears exactly once in its appropriate place.
- `QA Note` and today's `QA Check in` occurrence remain completed; tomorrow's `QA Appointment` remains on tomorrow only.
- General remains present exactly once.

### Cleanup
- Undo `QA Note` if needed, delete `QA Note`, `QA Appointment`, and `QA Check in`, then delete `QA Persist`.

## PLANNER-010 — Android back, keyboard, and readable layouts

### Purpose
Check device interaction and layout with constrained space and larger system text.

### Preconditions
- Use a small-screen Android device or emulator and a test installation with one list task and one routine. Record the original system font size so it can be restored.

### Steps
1. Open a task editor, focus the optional details field, type a long sentence, dismiss the keyboard with Android Back, and inspect the Save button. Reopen the keyboard and scroll the editor.
2. Close the editor with its Close icon; open it again and use Android Back to dismiss it.
3. Open General's Completed popup and use Android Back to close it. From Lists, use Android Back to return Home.
4. Open the calendar, switch Month and Week, and use Android Back or Close to return.
5. Set the system font size to a large accessibility setting, relaunch the app, and inspect Home, Recurring, Lists, the task editor, routine day/month choices, and calendar.

### Expected result
- Keyboard focus does not permanently obscure Save or trap scrolling; long text remains editable.
- Back dismisses the keyboard, popup, modal pages, and Lists tab in the expected order without losing saved data.
- At small width and large text, headings, task titles, controls, schedule choices, calendar dates, and error messages remain readable and operable; bottom navigation and save controls remain reachable.

### Cleanup
- Restore the original system font size and remove the test task and routine.

## PLANNER-011 — Checklist editing, progress, recurrence, and restart

### Purpose
Verify checklist creation and editing in list, dated, and recurring tasks, including partial progress, reopening, future occurrences, and persistence.

### Preconditions
- Use a test installation with no `QA Checklist` items and a correct device local date. Preserve unrelated existing data; use the unique `QA` names below.

### Steps
1. In Lists, create a list named `QA Checklist`; add a checklist task with title `QA Trip`, optional details, and subtasks `Book ticket` and `Pack bag`. Open its interactive checklist with the list/progress control and complete only `Book ticket`.
2. Confirm the partly complete task remains active and shows `1 of 2 done`. From its checklist, tap Edit to-do list, change the task title and first subtask title, add `Check weather`, and remove `Pack bag`; close the editor without saving. Reopen the checklist with the list/progress control and confirm the saved title, subtasks, and checked state are unchanged.
3. Edit the to-do list again, save the task title `QA Trip ready`, rename `Book ticket` to `Reserve ticket`, add `Check weather`, and remove `Pack bag`. Complete all remaining subtasks. Open the list's completed popup, use the list/progress control to open the interactive checklist, and uncheck one subtask.
4. Create a dated checklist with title `QA Appointment` and optional details for tomorrow, with subtasks `Bring form` and `Bring ID`. Complete one subtask and inspect the calendar for today and tomorrow.
5. Create a daily recurring checklist with title `QA Daily prep` starting today, optional details, and subtasks `Review plan` and `Prepare bag`. Complete both subtasks for today. In the calendar, open the interactive checklist with its list/progress control, inspect tomorrow, and complete only `Review plan` there.
6. Open today's `QA Daily prep` checklist with the list/progress control and tap Edit routine. Rename `Review plan`, add `Check weather`, remove `Prepare bag`, and save. Inspect today's occurrence and tomorrow's occurrence. Complete `Check weather` for today, then fully stop and relaunch the app without clearing its data.
7. Reopen the calendar and inspect today's, tomorrow's, and the day-after-tomorrow's occurrences. Open a completed checklist from the completed popup with its list/progress control and uncheck one subtask.

### Expected result
- The checklist view shows each subtask's checked state and an accurate completed/total counter. The Edit to-do list and Edit routine forms change titles and membership; closing without saving leaves those edits unapplied, and saving preserves progress for matching subtasks.
- A partially checked checklist remains active and shows an accurate completed/total counter. Completing every current subtask marks the parent complete; clearing a checked subtask reopens it in the active list while retaining the other subtask's progress.
- The dated checklist appears only tomorrow. Its partial progress does not affect any other date or checklist.
- Each recurring date has independent progress. A future occurrence starts unchecked. After editing the routine, completion remains attached to matching subtask IDs, a renamed subtask keeps its progress, and a newly added subtask reopens a previously complete occurrence until it is checked.
- After restart, task, subtask, and per-date completion state remain saved. Today's recurring checklist stays complete; tomorrow retains its own partial progress, and the day after tomorrow's occurrence remains fresh and incomplete.

### Cleanup
- Delete `QA Trip ready`, `QA Appointment`, and `QA Daily prep` through their edit screens, then delete `QA Checklist`.

## PLANNER-012 — Today progress widgets show dated task counts

### Purpose
Confirm both widget sizes report the same completion counts as Home's dated tasks for the device's local day.

### Preconditions
- Install a build containing the `Tasks left today`, `Today’s progress`, and Create task widgets on a launcher that supports app widgets.
- Use a test installation with no `QA Widget` items and the correct device local date.
- Start with no tasks due today so the initial widget state can be confirmed without clearing or resetting planner data.
- Place one 1x1 `Tasks left today` widget and one 2x1 `Today’s progress` widget where both are visible.

### Steps
1. Confirm the empty-day widget state. Create ten tasks dated today with titles prefixed by the run's unique `QA Widget` marker. Also create one `QA Widget` task in General, one dated yesterday, and one dated tomorrow.
2. Complete six of today's ten dated tasks. Leave the other four incomplete, then inspect both widgets and Home's Today's tasks section.
3. Complete the remaining four dated tasks and inspect all three places again.
4. Create one active daily routine titled `QA Widget` starting today. Inspect the widgets, complete today's routine occurrence, and inspect again. Then undo one dated task and inspect once more.
5. Remove only this run's dated, list, and recurring items. Confirm the empty-day state after cleanup.

### Expected result
- Before creating any due items, both widgets show `No tasks today` rather than the all-done presentation.
- With six of ten dated tasks complete and no routine yet, the 1x1 `Tasks left today` widget shows `4` and `left today`; the 2x1 `Today’s progress` widget shows `6/10`.
- Both counts include each checklist parent as one task, counting it complete only when every subtask is checked. They exclude General/list tasks and tasks dated on other days.
- An active routine due today contributes one task. Paused routines, routines before their start date, and weekly routines on a nonselected weekday do not contribute.
- Completing all ten dated tasks changes both widgets to the all-done presentation (checkmark and `All done`). Adding the active routine changes the count to 10/11 complete, with `1` left today; completing its occurrence returns both widgets to all-done. Undoing one dated task returns them to 10/11 complete, with `1` left today.
- After cleanup, a date with no due tasks shows `No tasks today`, not the all-done presentation. Home's dated section and widget totals agree after each change.

### Cleanup
- Delete only the run's `QA Widget` tasks and routine, then remove the temporary launcher widgets if desired.

## PLANNER-013 — Today widgets refresh after planner changes and date transitions

### Purpose
Verify every instance of the widgets refreshes after task changes and eventually follows the device's local date across lifecycle events.

### Preconditions
- Install the widget-enabled app and place at least two instances of each available size on the launcher.
- Use a test installation with no `QA Widget refresh` items; note the device's original date, time zone, and font size.

### Steps
1. Create a dated task titled `QA Widget refresh` for today, then add a second task with a distinct title from another app tab or modal editor.
2. Complete the first task, undo it, edit the second task, and delete it. Observe every widget instance after each change.
3. Create a dated task titled `QA Widget tomorrow` for tomorrow. Advance the device date past local midnight using the approved test-device procedure, then return to the launcher and app.
4. Restart the device, reopen the launcher, and allow normal app/widget refresh time. If the test environment supports it, repeat with a different local time zone and restore the original setting afterward.

### Expected result
- All instances of each widget size eventually show matching counts after add, edit, delete, complete, and undo actions, including actions performed from another tab or modal editor.
- At the new local date, yesterday's tasks no longer count and the task dated for the new day counts. A routine occurrence uses that date's independent completion state.
- Counts recover after device restart and eventually reflect the restored time zone. No task is duplicated or moved to a different date by refresh.
- Record delayed refresh or launcher-specific failures with the widget size and launcher details; do not treat a stale frame immediately after a change as a final result.

### Cleanup
- Restore the original device date/time zone and remove only this run's `QA Widget refresh` tasks and routines.

## PLANNER-014 — Create task widget opens the existing task editor

### Purpose
Confirm the 1x1 Create task widget reuses the app's dated task editor safely from cold and warm starts.

### Preconditions
- Install the widget-enabled app and place one 1x1 Create task widget on the launcher.
- Use a test installation with no `QA Create widget` task. App data is readable and can be loaded normally.

### Steps
1. Terminate the background app process through the approved test route without using Android Settings' Force stop action, which can disable widget PendingIntents on Android 15 and later. Tap the Create task widget and inspect the opened editor and selected destination/date.
2. Enter a unique `QA Create widget` title, save, and verify the new task appears once on Home for today and in the widget count.
3. Return to the launcher, tap the widget again, and use Close; repeat and use Android Back. Confirm no task is added by either dismissal.
4. Tap repeatedly while the editor is opening, enter an unsaved distinctive title, and tap the widget again. Confirm there is still one editor with the typed text intact. Save once and inspect the app for duplicate editors or duplicate tasks.
5. With the app already open on another tab, tap the widget, save another uniquely named task, and confirm Home is shown with the saved task.
6. On a test installation with a deliberately unreadable planner file prepared through the approved test fixture process, cold-launch through the widget and inspect the recovery flow. Do not modify or clear the unreadable data.

### Expected result
- A cold or warm widget tap opens the existing new-task editor with today's date selected and the dated-task destination active.
- Saving creates exactly one task and returns to Home with the updated task count. Close and Back dismiss without saving.
- Repeated taps do not stack duplicate editors or save duplicate tasks.
- When planner loading fails, the app presents its normal recovery option and preserves the unreadable data; the widget flow does not permit an unsafe edit or silent reset.

### Cleanup
- Delete only the `QA Create widget` tasks created in this run.

## PLANNER-015 — Widget placement, sizing, and readable text

### Purpose
Check Android launcher placement and accessibility readability for both fixed widget sizes.

### Preconditions
- Use an Android device or emulator with a launcher that supports the app's widgets.
- Note the original system font size and launcher grid settings. Create a dated task count that produces a nonzero remaining count and a separate all-done state.

### Steps
1. In the launcher widget picker, find `Tasks left today` and `Today’s progress`. Add the offered 1x1 and 2x1 sizes and inspect their fixed dimensions and content; do not expect resizing.
2. Move each widget to another launcher page. Tap the 1x1 widget from a cold app start, then tap the 2x1 widget while the app is warm on another tab. Also open a modal editor, return to the launcher, tap a count widget, and confirm navigation returns to Home.
3. Add the 1x1 Create task widget from the picker. Tap it from a cold start and save one unique task; verify Home appears with the new count. From another tab, open a modal editor, return to the launcher, and tap Create task; save another unique task and confirm it returns to Home.
4. Increase system font size to a large accessibility setting and inspect both count widget sizes in remaining, all-done, and empty-day states.
5. Restore the original font size and launcher placement after recording results.

### Expected result
- The picker offers the documented 1x1 and 2x1 fixed sizes. Widgets remain legible at large system font settings and do not clip the count or status text.
- Moving a widget does not change planner data or its counts. A count widget tap opens Home with today's tasks selected, including from another tab or after a modal editor; a Create task tap opens the existing dated editor and returns Home after save.
- Empty-day text and the all-done presentation remain distinguishable from an incomplete count.

### Cleanup
- Restore the original font size and remove the temporary launcher widgets and test tasks.

## PLANNER-016 — Task titles, optional details, and read-only task details

### Purpose
Verify that task titles remain short and readable while unlimited optional details stay attached to their task, and that opening task details never changes planner state.

### Preconditions
- Use a test installation with room to create unique `QA Text` tasks in General, on a calendar date, and as a recurring task. Include one checklist with at least two subtasks.
- Prepare a Description longer than 250 characters with several paragraphs. Keep the device font size and display size unchanged during the case.

### Steps
1. From Home, create a dated task with a short one-line title and long multiline text in the Description field. Save it, reopen Edit task, and confirm both fields retain their contents. Clear Description and save; reopen once more.
2. Try saving a task with a blank title. Dismiss the validation message, enter a title with three explicit lines, and try saving again. Then enter a long unbroken word and a multiword title that each wrap to more than two rendered lines at this device width; try saving each. Finally enter a valid title that renders on two lines or fewer and save.
3. Repeat the long Description save and edit on a General list task and on a recurring task. Create a checklist in General with long optional Description text and two subtasks.
4. Open the dated task's details from Home, then from its date in Calendar. Open the list task's details from Lists. Open the recurring task's details from the Recurring tab, including while it is paused or has no current occurrence, and from a scheduled occurrence. Also open a task from the completed-task popup.
5. In each details popup, tap inside the content and verify it stays open. Scroll the long details from top to bottom, then close with the close control, tap outside, and use Android Back in separate openings.
6. For the checklist, tap its title and then close the details popup. Tap the list/progress control to open its interactive checklist and inspect the subtasks; return without changing them.
7. Reopen each task and routine editor and compare the saved title, details, completion, and checklist progress with the values from before the read-only inspections.

### Expected result
- A title is required, has at most two explicit lines, and renders on at most two lines at the current device width. Blank titles, titles with three explicit lines, long unbroken words, and multiword titles that wrap beyond two rendered lines show a readable validation message, keep the editor open, and retain typed text for correction.
- Description text longer than 250 characters, including line breaks and paragraph spacing, saves and reopens exactly as entered for normal, checklist, and recurring tasks. Clearing Description saves successfully and displays no Description content.
- Details are available from Home, Lists, Calendar, completed tasks, the Recurring tab, and scheduled recurring occurrences. Recurring details show the routine's schedule in every entry point. They do not show a date or list destination.
- Tapping inside the details content does not dismiss it. Long details scroll fully, remain within the visible app area, and leave a visible margin around the popup. Close, outside tap, and Android Back each dismiss it.
- Tapping a checklist task title opens its details. Its list icon/progress control opens the interactive checklist. Opening and closing details changes no title, optional details, completion, subtask state, or recurring schedule.

### Cleanup
- Delete only the `QA Text` tasks and routine created in this run, then remove the checklist and its subtasks through the task editor.
