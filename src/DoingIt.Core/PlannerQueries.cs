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
                due.Add(new DueTask(task.Id, task.Title, false, task.IsCompleted));

        foreach (var task in data.RecurringTasks)
        {
            if (!task.IsEnabled || date < task.StartDate || !OccursOn(task, date))
                continue;

            due.Add(new DueTask(task.Id, task.Title, true,
                data.Completions.Any(completion => completion.RecurringTaskId == task.Id && completion.Date == date)));
        }
        return due;
    }

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
