using System.Text.Json;

namespace DoingIt.Core;

public sealed class PlannerStore(string filePath, Func<string, bool>? titleFits = null)
{
    public const string GeneralListId = "general";

    private readonly SemaphoreSlim _gate = new(1, 1);
    public PlannerData Data { get; private set; } = new();
    public event EventHandler? Changed;

    public async Task LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
                return;

            PlannerData loaded;
            try
            {
                await using var stream = File.OpenRead(filePath);
                using (var document = await JsonDocument.ParseAsync(stream))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !root.TryGetProperty(nameof(PlannerData.SchemaVersion), out _) ||
                        !root.TryGetProperty(nameof(PlannerData.Lists), out _) ||
                        !root.TryGetProperty(nameof(PlannerData.Tasks), out _) ||
                        !root.TryGetProperty(nameof(PlannerData.RecurringTasks), out _) ||
                        !root.TryGetProperty(nameof(PlannerData.Completions), out _))
                        throw new InvalidDataException("Planner data has missing required fields.");
                }
                stream.Position = 0;
                loaded = await JsonSerializer.DeserializeAsync(stream, PlannerJsonContext.Default.PlannerData)
                    ?? throw new InvalidDataException("Planner data is empty.");
                if (loaded.SchemaVersion == 1)
                {
                    // Version 1 has no checklist fields. Initialize them explicitly for
                    // source-generated deserialization of the older record shape.
                    loaded = loaded with
                    {
                        SchemaVersion = 2,
                        Tasks = loaded.Tasks?.Select(task => task is null ? task! : task with { IsChecklist = false, Subtasks = [] }).ToArray()!,
                        RecurringTasks = loaded.RecurringTasks?.Select(task => task is null ? task! : task with { IsChecklist = false, Subtasks = [] }).ToArray()!,
                        Completions = loaded.Completions?.Select(completion => completion is null ? completion! : completion with { CompletedSubtaskIds = [] }).ToArray()!
                    };
                }
                if (loaded.SchemaVersion == 2)
                {
                    loaded = loaded with
                    {
                        SchemaVersion = 3,
                        Tasks = loaded.Tasks?.Select(task =>
                        {
                            if (task is null) return task!;
                            var text = TaskText.FromLegacy(task.Title, titleFits ?? TaskText.FitsLines);
                            return task with { Title = text.Title, Description = text.Description };
                        }).ToArray()!,
                        RecurringTasks = loaded.RecurringTasks?.Select(task =>
                        {
                            if (task is null) return task!;
                            var text = TaskText.FromLegacy(task.Title, titleFits ?? TaskText.FitsLines);
                            return task with { Title = text.Title, Description = text.Description };
                        }).ToArray()!
                    };
                }
                ValidateData(loaded);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
            {
                throw new InvalidDataException($"Cannot load planner data from '{filePath}': {exception.Message} The file was left unchanged.", exception);
            }

            Data = loaded;
        }
        finally { _gate.Release(); }
    }

    public async Task<TaskList> AddListAsync(string name)
    {
        var cleaned = CleanText(name, 40, "List name");
        return await ChangeAsync(data =>
        {
            EnsureUniqueListName(data, cleaned);
            var list = new TaskList { Name = cleaned };
            return (data with { Lists = [.. data.Lists, list] }, list);
        });
    }

    public Task RenameListAsync(string id, string name)
    {
        var cleaned = CleanText(name, 40, "List name");
        return ChangeAsync(data =>
        {
            var index = FindIndex(data.Lists, id, list => list.Id, "List");
            if (id == GeneralListId)
                throw new InvalidOperationException("The General list cannot be renamed.");
            EnsureUniqueListName(data, cleaned, id);
            var lists = (TaskList[])data.Lists.Clone();
            lists[index] = lists[index] with { Name = cleaned };
            return data with { Lists = lists };
        });
    }

    public Task DeleteListAsync(string id) => ChangeAsync(data =>
    {
        if (id == GeneralListId)
            throw new InvalidOperationException("The General list cannot be deleted.");
        FindIndex(data.Lists, id, list => list.Id, "List");
        return data with
        {
            Lists = data.Lists.Where(list => list.Id != id).ToArray(),
            Tasks = data.Tasks.Select(task => task.ListId == id ? task with { ListId = GeneralListId } : task).ToArray()
        };
    });

    public Task SaveTaskAsync(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ChangeAsync(data =>
        {
            var cleaned = task with
            {
                Title = CleanTitle(task.Title),
                Description = TaskText.CleanDescription(task.Description),
                ListId = task.ListId?.Trim(),
                Subtasks = CleanSubtasks(task.Subtasks)
            };
            ValidateTask(cleaned, data.Lists);
            var index = Array.FindIndex(data.Tasks, existing => existing.Id == cleaned.Id);
            if (index >= 0 && data.Tasks[index].IsChecklist != cleaned.IsChecklist)
                throw new InvalidOperationException("The task type cannot be changed after creation.");
            var tasks = index < 0 ? [.. data.Tasks, cleaned] : (TaskItem[])data.Tasks.Clone();
            if (index >= 0) tasks[index] = cleaned;
            return data with { Tasks = tasks };
        });
    }

    public Task DeleteTaskAsync(string id) => ChangeAsync(data =>
    {
        FindIndex(data.Tasks, id, task => task.Id, "Task");
        return data with { Tasks = data.Tasks.Where(task => task.Id != id).ToArray() };
    });

    public Task SetTaskCompletedAsync(string id, bool completed) => ChangeAsync(data =>
    {
        var index = FindIndex(data.Tasks, id, task => task.Id, "Task");
        if (data.Tasks[index].IsChecklist) throw new InvalidOperationException("Complete the subtasks in this to-do list.");
        var tasks = (TaskItem[])data.Tasks.Clone();
        tasks[index] = tasks[index] with { IsCompleted = completed };
        return data with { Tasks = tasks };
    });

    public Task SetSubtaskCompletedAsync(string id, string subtaskId, bool completed) => ChangeAsync(data =>
    {
        var index = FindIndex(data.Tasks, id, task => task.Id, "Task");
        var task = data.Tasks[index];
        if (!task.IsChecklist) throw new InvalidOperationException("This task is not a to-do list.");
        var subtaskIndex = FindIndex(task.Subtasks, subtaskId, subtask => subtask.Id, "Subtask");
        var subtasks = (Subtask[])task.Subtasks.Clone();
        subtasks[subtaskIndex] = subtasks[subtaskIndex] with { IsCompleted = completed };
        var tasks = (TaskItem[])data.Tasks.Clone();
        tasks[index] = task with { Subtasks = subtasks };
        return data with { Tasks = tasks };
    });

    public Task SaveRecurringAsync(RecurringTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ChangeAsync(data =>
        {
            var cleaned = task with
            {
                Title = CleanTitle(task.Title),
                Description = TaskText.CleanDescription(task.Description),
                Subtasks = CleanSubtasks(task.Subtasks)
            };
            ValidateRecurring(cleaned);
            var index = Array.FindIndex(data.RecurringTasks, existing => existing.Id == cleaned.Id);
            if (index >= 0 && data.RecurringTasks[index].IsChecklist != cleaned.IsChecklist)
                throw new InvalidOperationException("The task type cannot be changed after creation.");
            var tasks = index < 0 ? [.. data.RecurringTasks, cleaned] : (RecurringTask[])data.RecurringTasks.Clone();
            if (index >= 0) tasks[index] = cleaned;
            return data with
            {
                RecurringTasks = tasks,
                Completions = data.Completions.Select(completion => completion.RecurringTaskId == task.Id && task.IsChecklist
                    ? completion with { CompletedSubtaskIds = completion.CompletedSubtaskIds.Where(id => cleaned.Subtasks.Any(subtask => subtask.Id == id)).ToArray() }
                    : completion).ToArray()
            };
        });
    }

    public Task DeleteRecurringAsync(string id) => ChangeAsync(data =>
    {
        FindIndex(data.RecurringTasks, id, task => task.Id, "Recurring task");
        return data with
        {
            RecurringTasks = data.RecurringTasks.Where(task => task.Id != id).ToArray(),
            Completions = data.Completions.Where(completion => completion.RecurringTaskId != id).ToArray()
        };
    });

    public Task SetRecurringEnabledAsync(string id, bool enabled) => ChangeAsync(data =>
    {
        var index = FindIndex(data.RecurringTasks, id, task => task.Id, "Recurring task");
        var tasks = (RecurringTask[])data.RecurringTasks.Clone();
        tasks[index] = tasks[index] with { IsEnabled = enabled };
        return data with { RecurringTasks = tasks };
    });

    public Task SetOccurrenceCompletedAsync(string id, DateOnly date, bool completed) => ChangeAsync(data =>
    {
        var index = FindIndex(data.RecurringTasks, id, task => task.Id, "Recurring task");
        var recurring = data.RecurringTasks[index];
        if (recurring.IsChecklist) throw new InvalidOperationException("Complete the subtasks in this to-do list.");
        if (!recurring.IsEnabled || date < recurring.StartDate || !PlannerQueries.ForDate(data, date).Any(task => task.IsRecurring && task.Id == id))
            throw new InvalidOperationException("This recurring task does not occur on the selected date.");
        var exists = data.Completions.Any(item => item.RecurringTaskId == id && item.Date == date);
        if (exists == completed) return data;
        return data with
        {
            Completions = completed
                ? [.. data.Completions, new OccurrenceCompletion { RecurringTaskId = id, Date = date }]
                : data.Completions.Where(item => item.RecurringTaskId != id || item.Date != date).ToArray()
        };
    });

    public Task SetOccurrenceSubtaskCompletedAsync(string id, DateOnly date, string subtaskId, bool completed) => ChangeAsync(data =>
    {
        var recurring = data.RecurringTasks[FindIndex(data.RecurringTasks, id, task => task.Id, "Recurring task")];
        if (!recurring.IsChecklist) throw new InvalidOperationException("This task is not a to-do list.");
        if (!PlannerQueries.ForDate(data, date).Any(task => task.IsRecurring && task.Id == id))
            throw new InvalidOperationException("This recurring task does not occur on the selected date.");
        FindIndex(recurring.Subtasks, subtaskId, subtask => subtask.Id, "Subtask");
        var previous = data.Completions.FirstOrDefault(item => item.RecurringTaskId == id && item.Date == date);
        var ids = previous?.CompletedSubtaskIds ?? [];
        var nextIds = completed ? ids.Append(subtaskId).Distinct().ToArray() : ids.Where(value => value != subtaskId).ToArray();
        var others = data.Completions.Where(item => item.RecurringTaskId != id || item.Date != date).ToArray();
        return data with
        {
            Completions = nextIds.Length == 0 ? others :
                [.. others, new OccurrenceCompletion { RecurringTaskId = id, Date = date, CompletedSubtaskIds = nextIds }]
        };
    });

    private async Task ChangeAsync(Func<PlannerData, PlannerData> change) =>
        await ChangeAsync(data => (change(data), true));

    private async Task<TResult> ChangeAsync<TResult>(Func<PlannerData, (PlannerData Next, TResult Result)> change)
    {
        await _gate.WaitAsync();
        try
        {
            var (next, result) = change(Data);
            if (ReferenceEquals(next, Data)) return result;
            ValidateData(next);
            await PersistAsync(next);
            Data = next;
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }
        finally { _gate.Release(); }
    }

    private static string CleanText(string? value, int maxLength, string label)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrEmpty(cleaned)) throw new ArgumentException($"{label} is required.");
        if (cleaned.Length > maxLength) throw new ArgumentException($"{label} must be {maxLength} characters or fewer.");
        return cleaned;
    }

    private string CleanTitle(string? value)
    {
        var title = TaskText.CleanTitle(value);
        if (titleFits is not null && !titleFits(title))
            throw new ArgumentException("Task title must fit on two lines. Put additional details in the description.");
        return title;
    }

    private static void EnsureUniqueListName(PlannerData data, string name, string? exceptId = null)
    {
        if (data.Lists.Any(list => list.Id != exceptId && string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A list with this name already exists.");
    }

    private static int FindIndex<T>(T[] items, string id, Func<T, string> idSelector, string label)
    {
        var index = Array.FindIndex(items, item => idSelector(item) == id);
        if (index < 0) throw new InvalidOperationException($"{label} was not found.");
        return index;
    }

    private static void ValidateTask(TaskItem task, TaskList[] lists)
    {
        RequireId(task.Id, "Task");
        TaskText.CleanTitle(task.Title);
        ValidateSubtasks(task.IsChecklist, task.Subtasks);
        if ((task.ListId is not null) == task.ScheduledDate.HasValue)
            throw new ArgumentException("A task must belong to either a list or a scheduled date.");
        if (task.ListId is not null && !lists.Any(list => list.Id == task.ListId))
            throw new ArgumentException("The selected list does not exist.");
    }

    private static void ValidateRecurring(RecurringTask task)
    {
        RequireId(task.Id, "Recurring task");
        TaskText.CleanTitle(task.Title);
        ValidateSubtasks(task.IsChecklist, task.Subtasks);
        if (task.Subtasks.Any(subtask => subtask.IsCompleted))
            throw new ArgumentException("Recurring subtask completion belongs to a date, not the routine.");
        if (task.StartDate == default) throw new ArgumentException("A recurring task needs a start date.");
        if (task.WeekDays is null || task.MonthDays is null || task.Months is null)
            throw new ArgumentException("Recurring schedule selections are missing.");
        switch (task.Kind)
        {
            case RecurrenceKind.Daily: break;
            case RecurrenceKind.Weekly when task.WeekDays.Length > 0 && task.WeekDays.All(Enum.IsDefined): break;
            case RecurrenceKind.Monthly when task.MonthDays.Length > 0 && task.Months.Length > 0 &&
                task.MonthDays.All(day => day is >= 1 and <= 31) && task.Months.All(month => month is >= 1 and <= 12):
                break;
            default: throw new ArgumentException("Select valid weekdays or month days and months for the recurring schedule.");
        }
    }

    private static void RequireId(string? id, string label)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException($"{label} ID is required.");
    }

    private static Subtask[] CleanSubtasks(Subtask[] subtasks)
    {
        if (subtasks is null || subtasks.Any(subtask => subtask is null))
            throw new ArgumentException("Subtasks are missing.");
        return subtasks.Select(subtask => subtask with { Title = CleanText(subtask.Title, 250, "Subtask title") }).ToArray();
    }

    private static void ValidateSubtasks(bool isChecklist, Subtask[] subtasks)
    {
        if (subtasks is null || subtasks.Any(subtask => subtask is null)) throw new ArgumentException("Subtasks are missing.");
        if (isChecklist && subtasks.Length == 0) throw new ArgumentException("Add at least one subtask to the to-do list.");
        if (!isChecklist && subtasks.Length > 0) throw new ArgumentException("Only to-do lists can contain subtasks.");
        foreach (var subtask in subtasks)
        {
            RequireId(subtask.Id, "Subtask");
            if (CleanText(subtask.Title, 250, "Subtask title") != subtask.Title)
                throw new ArgumentException("A subtask title has extra whitespace.");
        }
        if (subtasks.Select(subtask => subtask.Id).Distinct(StringComparer.Ordinal).Count() != subtasks.Length)
            throw new ArgumentException("Subtasks have duplicate IDs.");
    }

    private static void ValidateData(PlannerData data)
    {
        if (data.SchemaVersion != 3) throw new InvalidDataException($"Unsupported planner data version {data.SchemaVersion}.");
        if (data.Lists is null || data.Tasks is null || data.RecurringTasks is null || data.Completions is null)
            throw new InvalidDataException("Planner data has missing collections.");
        if (data.Lists.Any(list => list is null) || data.Tasks.Any(task => task is null) ||
            data.RecurringTasks.Any(task => task is null) || data.Completions.Any(completion => completion is null))
            throw new InvalidDataException("Planner data has missing entries.");
        if (data.Lists.Count(list => list.Id == GeneralListId) != 1)
            throw new InvalidDataException("The General list is missing or duplicated.");
        if (data.Lists.Single(list => list.Id == GeneralListId).Name != "General")
            throw new InvalidDataException("The General list has an invalid name.");
        foreach (var list in data.Lists)
        {
            RequireId(list.Id, "List");
            CleanText(list.Name, 40, "List name");
            if (list.Name != list.Name.Trim()) throw new InvalidDataException("A list name has extra whitespace.");
        }
        if (data.Lists.Select(list => list.Id).Distinct(StringComparer.Ordinal).Count() != data.Lists.Length ||
            data.Lists.Select(list => list.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Lists.Length)
            throw new InvalidDataException("Planner lists have duplicate IDs or names.");
        foreach (var task in data.Tasks) ValidateTask(task, data.Lists);
        if (data.Tasks.Any(task => task.Title != task.Title.Trim()))
            throw new InvalidDataException("A task title has extra whitespace.");
        if (data.Tasks.Select(task => task.Id).Distinct(StringComparer.Ordinal).Count() != data.Tasks.Length)
            throw new InvalidDataException("Planner tasks have duplicate IDs.");
        foreach (var task in data.RecurringTasks) ValidateRecurring(task);
        if (data.RecurringTasks.Any(task => task.Title != task.Title.Trim()))
            throw new InvalidDataException("A recurring task title has extra whitespace.");
        if (data.RecurringTasks.Select(task => task.Id).Distinct(StringComparer.Ordinal).Count() != data.RecurringTasks.Length)
            throw new InvalidDataException("Recurring tasks have duplicate IDs.");
        foreach (var completion in data.Completions)
        {
            if (completion.Date == default) throw new InvalidDataException("A completion has no date.");
            var recurring = data.RecurringTasks.FirstOrDefault(task => task.Id == completion.RecurringTaskId);
            if (recurring is null)
                throw new InvalidDataException("A completion refers to a missing recurring task.");
            if (completion.CompletedSubtaskIds is null ||
                completion.CompletedSubtaskIds.Distinct(StringComparer.Ordinal).Count() != completion.CompletedSubtaskIds.Length ||
                completion.CompletedSubtaskIds.Any(id => !recurring.Subtasks.Any(subtask => subtask.Id == id)))
                throw new InvalidDataException("A completion has invalid subtasks.");
        }
        if (data.Completions.Select(completion => (completion.RecurringTaskId, completion.Date)).Distinct().Count() != data.Completions.Length)
            throw new InvalidDataException("A recurring occurrence has duplicate completions.");
    }

    private async Task PersistAsync(PlannerData data)
    {
        var path = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, data, PlannerJsonContext.Default.PlannerData);
                await stream.FlushAsync();
            }
            if (File.Exists(path)) File.Replace(tempPath, path, path + ".bak");
            else File.Move(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
