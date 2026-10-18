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
1. In General, tap `+ Add task`, enter `QA File receipt`, leave `In a list` selected with General, and tap Add task.
2. Tap the task's edit icon, change its description to `QA File invoice`, and save changes.
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
1. On Home, tap `+ Add a task for today`, enter `QA Today`, and tap Add task.
2. Open the calendar and select today. Then select tomorrow.
3. On tomorrow, tap `+ Add task for this day`, enter `QA Tomorrow`, verify `On a date` and tomorrow are selected, and tap Add task.
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
1. Create `QA Daily` with Daily selected.
2. Create `QA Weekly` with Weekly selected; choose Monday and Wednesday and leave the other weekdays unselected.
3. Create `QA Monthly` with Monthly selected; choose day 31 and months February and March only.
4. Use the calendar to inspect the next Monday, Wednesday, and Thursday, plus the next February 28 or 29 and March 31 after today.
5. Inspect today and tomorrow for `QA Daily`.

### Expected result
- `QA Daily` appears every day from its creation date onward.
- `QA Weekly` appears on Mondays and Wednesdays from its creation date onward and is absent on Thursdays.
- `QA Monthly` appears on March 31 and is absent throughout February, including its last day; it is not shifted to a different day.
- Recurring rows are identified as recurring and do not appear in list cards.

### Cleanup
- Delete the three `QA` routines through Edit routine.

## PLANNER-007 — Required descriptions and schedule choices

### Purpose
Confirm invalid entries show readable errors and create no partial items.

### Preconditions
- Open Lists and Recurring in the dedicated test installation.

### Steps
1. In Lists, tap `+ New list`, enter only spaces, and tap Create. Dismiss the error.
2. Create a list named `QA Unique`; then try to create `qa unique` and dismiss the error.
3. Open `+ New task`, leave the description blank, and tap Add task. Dismiss the error and close the editor.
4. On Recurring, open `+ Create recurring task`, leave the description blank, and tap Create routine. Dismiss the error.
5. Enter `QA Invalid weekly`, choose Weekly, deselect every weekday, and try Create routine. Dismiss the error.
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
1. Create `QA Stretch` as a Daily routine.
2. Tap Edit routine, change the description to `QA Stretch gently`, and save changes.
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
1. Create list `QA Persist`, add list task `QA Note`, create tomorrow's dated task `QA Appointment`, and create daily routine `QA Check in`.
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
1. Open a task editor, focus the description, type a long sentence, dismiss the keyboard with Android Back, and inspect the Save button. Reopen the keyboard and scroll the editor.
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
1. In Lists, create a list named `QA Checklist`; add a checklist task named `QA Trip` with subtasks `Book ticket` and `Pack bag`. Open its checklist and complete only `Book ticket`.
2. Confirm the partly complete task remains active and shows `1 of 2 done`. From its checklist, tap Edit to-do list, rename the task and first subtask, add `Check weather`, and remove `Pack bag`; close the editor without saving. Reopen the checklist and confirm the saved title, subtasks, and checked state are unchanged.
3. Edit the to-do list again, save the title `QA Trip ready`, rename `Book ticket` to `Reserve ticket`, add `Check weather`, and remove `Pack bag`. Complete all remaining subtasks. Open the list's completed popup, open the checklist, and uncheck one subtask.
4. Create a dated checklist `QA Appointment` for tomorrow with subtasks `Bring form` and `Bring ID`. Complete one subtask and inspect the calendar for today and tomorrow.
5. Create a daily recurring checklist `QA Daily prep` starting today with subtasks `Review plan` and `Prepare bag`. Complete both subtasks for today. In the calendar, inspect tomorrow and complete only `Review plan` there.
6. Open today's `QA Daily prep` checklist and tap Edit routine. Rename `Review plan`, add `Check weather`, remove `Prepare bag`, and save. Inspect today's occurrence and tomorrow's occurrence. Complete `Check weather` for today, then fully stop and relaunch the app without clearing its data.
7. Reopen the calendar and inspect today's, tomorrow's, and the day-after-tomorrow's occurrences. Open a completed checklist from the completed popup and uncheck one subtask.

### Expected result
- The checklist view shows each subtask's checked state and an accurate completed/total counter. The Edit to-do list and Edit routine forms change titles and membership; closing without saving leaves those edits unapplied, and saving preserves progress for matching subtasks.
- A partially checked checklist remains active and shows an accurate completed/total counter. Completing every current subtask marks the parent complete; clearing a checked subtask reopens it in the active list while retaining the other subtask's progress.
- The dated checklist appears only tomorrow. Its partial progress does not affect any other date or checklist.
- Each recurring date has independent progress. A future occurrence starts unchecked. After editing the routine, completion remains attached to matching subtask IDs, a renamed subtask keeps its progress, and a newly added subtask reopens a previously complete occurrence until it is checked.
- After restart, task, subtask, and per-date completion state remain saved. Today's recurring checklist stays complete; tomorrow retains its own partial progress, and the day after tomorrow's occurrence remains fresh and incomplete.

### Cleanup
- Delete `QA Trip ready`, `QA Appointment`, and `QA Daily prep` through their edit screens, then delete `QA Checklist`.
