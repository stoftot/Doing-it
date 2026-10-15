namespace DoingIt.Core;

public enum RecurrenceKind { Daily, Weekly, Monthly }

public record TaskList
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
}

public record TaskItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string? ListId { get; init; }
    public DateOnly? ScheduledDate { get; init; }
    public bool IsCompleted { get; init; }
}

public record RecurringTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public RecurrenceKind Kind { get; init; }
    public DayOfWeek[] WeekDays { get; init; } = [];
    public int[] MonthDays { get; init; } = [];
    public int[] Months { get; init; } = [];
    public bool IsEnabled { get; init; } = true;
    public DateOnly StartDate { get; init; }
}

public record OccurrenceCompletion
{
    public string RecurringTaskId { get; init; } = "";
    public DateOnly Date { get; init; }
}

public record PlannerData
{
    public int SchemaVersion { get; init; } = 1;
    public TaskList[] Lists { get; init; } = [new() { Id = PlannerStore.GeneralListId, Name = "General" }];
    public TaskItem[] Tasks { get; init; } = [];
    public RecurringTask[] RecurringTasks { get; init; } = [];
    public OccurrenceCompletion[] Completions { get; init; } = [];
}

public record DueTask(string Id, string Title, bool IsRecurring, bool IsCompleted);
