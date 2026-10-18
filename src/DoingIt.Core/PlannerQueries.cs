namespace DoingIt.Core;

public static class PlannerQueries
{
    private static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public static IReadOnlyList<DueTask> ForDate(PlannerData data, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(data);
        var due = new List<DueTask>();
        foreach (var task in data.Tasks)
            if (task.ScheduledDate == date)
                due.Add(FromTask(task));

        foreach (var task in data.RecurringTasks)
        {
            if (!task.IsEnabled || date < task.StartDate || !OccursOn(task, date))
                continue;

            var completion = data.Completions.FirstOrDefault(item => item.RecurringTaskId == task.Id && item.Date == date);
            var done = task.Subtasks.Count(subtask => completion?.CompletedSubtaskIds.Contains(subtask.Id) == true);
            due.Add(new DueTask(task.Id, task.Title, true,
                task.IsChecklist ? task.Subtasks.Length > 0 && done == task.Subtasks.Length : completion is not null,
                task.IsChecklist, done, task.Subtasks.Length));
        }
        return due;
    }

    public static DueTask FromTask(TaskItem task) => new(task.Id, task.Title, false, task.IsCompleted,
        task.IsChecklist, task.Subtasks.Count(subtask => subtask.IsCompleted), task.Subtasks.Length);

    public static string ScheduleLabel(RecurringTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.Kind switch
        {
            RecurrenceKind.Daily => "Every day",
            RecurrenceKind.Weekly => string.Join(", ", task.WeekDays.Distinct().OrderBy(day => day).Select(day => DayNames[(int)day])),
            RecurrenceKind.Monthly => $"Days {string.Join(",", task.MonthDays.Distinct().Order())} • " +
                (task.Months.Distinct().Count() == 12 ? "Every month" :
                    string.Join(",", task.Months.Distinct().Order().Select(month => MonthNames[month - 1]))),
            _ => throw new ArgumentException("Unknown recurrence type.", nameof(task))
        };
    }

    private static bool OccursOn(RecurringTask task, DateOnly date) => task.Kind switch
    {
        RecurrenceKind.Daily => true,
        RecurrenceKind.Weekly => task.WeekDays.Contains(date.DayOfWeek),
        RecurrenceKind.Monthly => task.MonthDays.Contains(date.Day) && task.Months.Contains(date.Month),
        _ => false
    };
}
