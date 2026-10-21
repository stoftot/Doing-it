namespace DoingIt.Core;

public enum RecurrenceKind { Daily, Weekly, Monthly }

public record TaskList
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
}

public record TaskItem
{
    private readonly bool _isCompleted;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string? ListId { get; init; }
    public DateOnly? ScheduledDate { get; init; }
    public bool IsChecklist { get; init; }
    public Subtask[] Subtasks { get; init; } = [];
    public bool IsCompleted
    {
        get => IsChecklist ? Subtasks is { Length: > 0 } && Subtasks.All(task => task is { IsCompleted: true }) : _isCompleted;
        init => _isCompleted = value;
    }
}

public record Subtask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public bool IsCompleted { get; init; }
}

public record RecurringTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public bool IsChecklist { get; init; }
    public Subtask[] Subtasks { get; init; } = [];
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
    public string[] CompletedSubtaskIds { get; init; } = [];
}

public record PlannerData
{
    public int SchemaVersion { get; init; } = 3;
    public TaskList[] Lists { get; init; } = [new() { Id = PlannerStore.GeneralListId, Name = "General" }];
    public TaskItem[] Tasks { get; init; } = [];
    public RecurringTask[] RecurringTasks { get; init; } = [];
    public OccurrenceCompletion[] Completions { get; init; } = [];
}

public record DueTask(string Id, string Title, bool IsRecurring, bool IsCompleted,
    bool IsChecklist = false, int CompletedSubtasks = 0, int TotalSubtasks = 0)
{
    public string ProgressLabel => $"{CompletedSubtasks} of {TotalSubtasks} done";
}
