using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class ChecklistTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public async Task ChecklistTask_TracksPartialCompleteAndUncheckedSubtasks_AfterReload()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        await store.LoadAsync();
        var task = new TaskItem
        {
            Title = "Pack",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "Keys" }, new() { Title = "Wallet" }]
        };
        await store.SaveTaskAsync(task);

        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, true);
        Assert.False(store.Data.Tasks.Single().IsCompleted);
        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[1].Id, true);
        Assert.True(store.Data.Tasks.Single().IsCompleted);
        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, false);
        Assert.False(store.Data.Tasks.Single().IsCompleted);

        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        var saved = Assert.Single(reopened.Data.Tasks);
        Assert.True(saved.IsChecklist);
        Assert.False(saved.IsCompleted);
        Assert.Equal(task.Subtasks.Select(item => item.Id), saved.Subtasks.Select(item => item.Id));
        await reopened.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, true);
        await reopened.SetSubtaskCompletedAsync(task.Id, task.Subtasks[1].Id, true);
        Assert.True(reopened.Data.Tasks.Single().IsCompleted);
    }

    [Fact]
    public async Task ChecklistTask_QueryReportsProgressAndKeepsCompletedTaskAvailable()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var checklist = new TaskItem
        {
            Title = "Prepare",
            ScheduledDate = Today,
            IsChecklist = true,
            Subtasks = [new() { Title = "Draft" }, new() { Title = "Review" }, new() { Title = "Send" }]
        };
        var otherDate = new TaskItem { Title = "Tomorrow", ScheduledDate = Today.AddDays(1) };
        await store.SaveTaskAsync(checklist);
        await store.SaveTaskAsync(otherDate);
        await store.SetSubtaskCompletedAsync(checklist.Id, checklist.Subtasks[0].Id, true);

        var partial = Assert.Single(PlannerQueries.ForDate(store.Data, Today));
        Assert.Equal(checklist.Id, partial.Id);
        Assert.True(partial.IsChecklist);
        Assert.Equal(1, partial.CompletedSubtasks);
        Assert.Equal(3, partial.TotalSubtasks);
        Assert.False(partial.IsCompleted);
        Assert.DoesNotContain(PlannerQueries.ForDate(store.Data, Today.AddDays(1)), item => item.Id == checklist.Id);

        await store.SetSubtaskCompletedAsync(checklist.Id, checklist.Subtasks[1].Id, true);
        await store.SetSubtaskCompletedAsync(checklist.Id, checklist.Subtasks[2].Id, true);
        var completed = Assert.Single(PlannerQueries.ForDate(store.Data, Today));
        Assert.Equal(checklist.Id, completed.Id);
        Assert.True(completed.IsCompleted);
        Assert.Equal(3, completed.CompletedSubtasks);
    }

    [Fact]
    public async Task DeletingChecklistTask_RemovesItsSubtasksAndCompletedHistory()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var task = new TaskItem
        {
            Title = "Discard",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "Part one" }, new() { Title = "Part two" }]
        };
        await store.SaveTaskAsync(task);
        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, true);
        await store.DeleteTaskAsync(task.Id);

        Assert.Empty(store.Data.Tasks);
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Empty(reopened.Data.Tasks);
    }

    [Fact]
    public async Task DeletingChecklistList_MovesTaskToGeneralAndPreservesProgress()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var list = await store.AddListAsync("Errands");
        var task = new TaskItem
        {
            Title = "Shop",
            ListId = list.Id,
            IsChecklist = true,
            Subtasks = [new() { Title = "Bread" }, new() { Title = "Milk" }]
        };
        await store.SaveTaskAsync(task);
        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, true);

        await store.DeleteListAsync(list.Id);
        var moved = Assert.Single(store.Data.Tasks);
        Assert.Equal(PlannerStore.GeneralListId, moved.ListId);
        Assert.False(moved.IsCompleted);
        Assert.Equal(1, PlannerQueries.FromTask(moved).CompletedSubtasks);
        await store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[1].Id, true);
        Assert.True(store.Data.Tasks.Single().IsCompleted);
    }

    [Fact]
    public async Task EditingChecklistDestination_PreservesChecklistTypeAndProgress()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var checklist = new TaskItem
        {
            Title = "Prepare",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "Draft" }, new() { Title = "Review" }]
        };
        await store.SaveTaskAsync(checklist);
        await store.SetSubtaskCompletedAsync(checklist.Id, checklist.Subtasks[0].Id, true);

        await store.SaveTaskAsync(store.Data.Tasks.Single() with { ListId = null, ScheduledDate = Today });

        var moved = Assert.Single(store.Data.Tasks);
        Assert.Null(moved.ListId);
        Assert.Equal(Today, moved.ScheduledDate);
        Assert.True(moved.IsChecklist);
        Assert.Equal(1, PlannerQueries.FromTask(moved).CompletedSubtasks);
    }

    [Fact]
    public async Task RecurringChecklist_TracksIndependentDatesAndStartsFutureDatesFresh()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var routine = new RecurringTask
        {
            Title = "Travel prep",
            Kind = RecurrenceKind.Weekly,
            WeekDays = [DayOfWeek.Wednesday],
            StartDate = Today,
            IsChecklist = true,
            Subtasks = [new() { Title = "Check route" }, new() { Title = "Pack bag" }]
        };
        await store.SaveRecurringAsync(routine);
        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, routine.Subtasks[0].Id, true);
        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today.AddDays(7), routine.Subtasks[0].Id, true);
        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, routine.Subtasks[1].Id, true);

        var today = Assert.Single(PlannerQueries.ForDate(store.Data, Today));
        Assert.True(today.IsChecklist);
        Assert.True(today.IsCompleted);
        Assert.Equal(2, today.CompletedSubtasks);
        Assert.False(Assert.Single(PlannerQueries.ForDate(store.Data, Today.AddDays(7))).IsCompleted);
        var futureOccurrence = Assert.Single(PlannerQueries.ForDate(store.Data, Today.AddDays(14)));
        Assert.False(futureOccurrence.IsCompleted);
        Assert.Equal(0, futureOccurrence.CompletedSubtasks);

        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.True(Assert.Single(PlannerQueries.ForDate(reopened.Data, Today)).IsCompleted);
        Assert.Equal(1, Assert.Single(PlannerQueries.ForDate(reopened.Data, Today.AddDays(7))).CompletedSubtasks);
        Assert.False(Assert.Single(PlannerQueries.ForDate(reopened.Data, Today.AddDays(7))).IsCompleted);
        await reopened.DeleteRecurringAsync(routine.Id);
        Assert.Empty(reopened.Data.Completions);
    }

    [Fact]
    public async Task EditingRecurringChecklist_PreservesMatchingProgressAndRecalculatesCompletion()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var first = new Subtask { Title = "First" };
        var second = new Subtask { Title = "Second" };
        var routine = new RecurringTask
        {
            Title = "Routine",
            Kind = RecurrenceKind.Daily,
            StartDate = Today,
            IsChecklist = true,
            Subtasks = [first, second]
        };
        await store.SaveRecurringAsync(routine);
        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, first.Id, true);
        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, second.Id, true);
        var originalSnapshot = store.Data;

        var added = new Subtask { Title = "Third" };
        await store.SaveRecurringAsync(routine with
        {
            Title = "Renamed routine",
            Subtasks = [first with { Title = "First renamed" }, added]
        });
        var afterAdd = Assert.Single(PlannerQueries.ForDate(store.Data, Today));
        Assert.False(afterAdd.IsCompleted);
        Assert.Equal(1, afterAdd.CompletedSubtasks);
        Assert.Equal(2, afterAdd.TotalSubtasks);
        Assert.Equal("First", originalSnapshot.RecurringTasks.Single().Subtasks[0].Title);
        Assert.Equal(2, originalSnapshot.Completions.Single().CompletedSubtaskIds.Length);

        await store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, added.Id, true);
        Assert.True(Assert.Single(PlannerQueries.ForDate(store.Data, Today)).IsCompleted);
        await store.SaveRecurringAsync(store.Data.RecurringTasks.Single() with { Subtasks = [first with { Title = "First renamed again" }] });

        var saved = Assert.Single(PlannerQueries.ForDate(store.Data, Today));
        Assert.Equal("Renamed routine", saved.Title);
        Assert.True(saved.IsCompleted);
        Assert.Equal(1, saved.CompletedSubtasks);
        Assert.Equal(1, saved.TotalSubtasks);
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.True(Assert.Single(PlannerQueries.ForDate(reopened.Data, Today)).IsCompleted);
    }

    [Fact]
    public async Task ChecklistValidationAndInvalidSubtaskUpdates_LeaveSnapshotAndFileUnchanged()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var valid = new TaskItem
        {
            Title = "Valid",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "One" }]
        };
        await store.SaveTaskAsync(valid);
        var before = store.Data;
        var fileBefore = await File.ReadAllTextAsync(fixture.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { Subtasks = [] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { Subtasks = [new() { Title = "  " }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { Subtasks = [new() { Title = new string('x', 251) }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { Subtasks = [new() { Id = " ", Title = "No ID" }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { Subtasks = [new() { Id = "duplicate", Title = "One" }, new() { Id = "duplicate", Title = "Two" }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(valid with { IsChecklist = false }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetSubtaskCompletedAsync(valid.Id, "missing", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetTaskCompletedAsync(valid.Id, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveTaskAsync(valid with { IsChecklist = false, Subtasks = [] }));

        Assert.Same(before, store.Data);
        Assert.Equal(fileBefore, await File.ReadAllTextAsync(fixture.Path));
    }

    [Fact]
    public async Task SubtaskWriteFailure_DoesNotPublishChangedSnapshot()
    {
        using var fixture = new StoreFixture();
        var task = new TaskItem
        {
            Title = "Valid",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "One" }]
        };
        await fixture.Store.SaveTaskAsync(task);
        var before = fixture.Store.Data;
        File.Delete(fixture.Path);
        Directory.CreateDirectory(fixture.Path);

        await Assert.ThrowsAnyAsync<IOException>(() => fixture.Store.SetSubtaskCompletedAsync(task.Id, task.Subtasks[0].Id, true));

        Assert.Same(before, fixture.Store.Data);
        Assert.False(fixture.Store.Data.Tasks.Single().Subtasks.Single().IsCompleted);
    }

    [Fact]
    public async Task InvalidRecurringChecklistDatesAndIds_LeaveSnapshotAndFileUnchanged()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var routine = new RecurringTask
        {
            Title = "Monthly prep",
            Kind = RecurrenceKind.Monthly,
            MonthDays = [31],
            Months = [2],
            StartDate = Today,
            IsChecklist = true,
            Subtasks = [new() { Title = "Check" }]
        };
        await store.SaveRecurringAsync(routine);
        var before = store.Data;
        var fileBefore = await File.ReadAllTextAsync(fixture.Path);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetOccurrenceSubtaskCompletedAsync(routine.Id, new DateOnly(2024, 2, 29), routine.Subtasks[0].Id, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetOccurrenceSubtaskCompletedAsync(routine.Id, Today, "missing", true));
        var repeatedId = routine.Subtasks[0].Id;
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveRecurringAsync(routine with { Subtasks = [new() { Id = repeatedId, Title = "Check" }, new() { Id = repeatedId, Title = "Duplicate" }] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveRecurringAsync(routine with { IsChecklist = false, Subtasks = [] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetOccurrenceCompletedAsync(routine.Id, Today, true));

        Assert.Same(before, store.Data);
        Assert.Equal(fileBefore, await File.ReadAllTextAsync(fixture.Path));
    }

    [Fact]
    public async Task VersionOneFile_MigratesOldTasksAndCompletionsOnNextSave()
    {
        using var fixture = new StoreFixture();
        const string versionOne = """
            {"SchemaVersion":1,"Lists":[{"Id":"general","Name":"General"}],"Tasks":[{"Id":"old-task","Title":"Old list task","ListId":"general","IsCompleted":true},{"Id":"old-date","Title":"Old dated task","ScheduledDate":"2024-02-28","IsCompleted":false}],"RecurringTasks":[{"Id":"old-routine","Title":"Old routine","Kind":0,"WeekDays":[],"MonthDays":[],"Months":[],"IsEnabled":true,"StartDate":"2024-02-28"}],"Completions":[{"RecurringTaskId":"old-routine","Date":"2024-02-28"}]}
            """;
        await File.WriteAllTextAsync(fixture.Path, versionOne);

        var store = new PlannerStore(fixture.Path);
        await store.LoadAsync();
        Assert.Equal(versionOne, await File.ReadAllTextAsync(fixture.Path));
        Assert.False(File.Exists(fixture.Path + ".bak"));
        Assert.True(store.Data.Tasks.Single(task => task.Id == "old-task").IsCompleted);
        Assert.Equal("Old dated task", PlannerQueries.ForDate(store.Data, Today).Single(task => !task.IsRecurring).Title);
        Assert.True(PlannerQueries.ForDate(store.Data, Today).Single(task => task.IsRecurring).IsCompleted);
        await store.AddListAsync("After migration");

        using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(fixture.Path));
        Assert.Equal(3, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(versionOne, await File.ReadAllTextAsync(fixture.Path + ".bak"));
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.True(reopened.Data.Tasks.Single(task => task.Id == "old-task").IsCompleted);
        Assert.True(PlannerQueries.ForDate(reopened.Data, Today).Single(task => task.IsRecurring).IsCompleted);
        Assert.Single(reopened.Data.Completions);
    }

    [Fact]
    public async Task LoadFailure_PreservesCorruptChecklistFileAndCurrentSnapshot()
    {
        using var fixture = new StoreFixture();
        var task = new TaskItem
        {
            Title = "Valid",
            ListId = PlannerStore.GeneralListId,
            IsChecklist = true,
            Subtasks = [new() { Title = "One" }]
        };
        await fixture.Store.SaveTaskAsync(task);
        var snapshot = fixture.Store.Data;
        const string corrupt = """
            {"SchemaVersion":2,"Lists":[{"Id":"general","Name":"General"}],"Tasks":[{"Id":"bad","Title":"Bad","ListId":"general","IsChecklist":true,"Subtasks":[]}],"RecurringTasks":[],"Completions":[]}
            """;
        await File.WriteAllTextAsync(fixture.Path, corrupt);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.LoadAsync());
        Assert.Equal(corrupt, await File.ReadAllTextAsync(fixture.Path));
        Assert.Same(snapshot, fixture.Store.Data);
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "doing-it-checklist-tests-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public PlannerStore Store { get; }

        public StoreFixture()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "planner.json");
            Store = new PlannerStore(Path);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
