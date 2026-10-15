using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class PlannerTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public void ForDate_SeparatesListAndScheduledTasks_AndKeepsInsertionOrder()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "List", ListId = PlannerStore.GeneralListId },
                new() { Title = "Today", ScheduledDate = Today, IsCompleted = true },
                new() { Title = "Tomorrow", ScheduledDate = Today.AddDays(1) },
                new() { Title = "Also today", ScheduledDate = Today }
            ],
            RecurringTasks = [new() { Title = "Daily", Kind = RecurrenceKind.Daily, StartDate = Today }]
        };

        var due = PlannerQueries.ForDate(data, Today);

        Assert.Equal(["Today", "Also today", "Daily"], due.Select(task => task.Title));
        Assert.Equal([true, false, false], due.Select(task => task.IsCompleted));
        Assert.Equal([false, false, true], due.Select(task => task.IsRecurring));
    }

    [Fact]
    public void ForDate_RespectsAllRecurrenceRules()
    {
        var data = new PlannerData
        {
            RecurringTasks =
            [
                new() { Title = "Daily", Kind = RecurrenceKind.Daily, StartDate = Today.AddDays(1) },
                new() { Title = "Weekly", Kind = RecurrenceKind.Weekly, WeekDays = [DayOfWeek.Thursday], StartDate = Today },
                new() { Title = "Monthly", Kind = RecurrenceKind.Monthly, MonthDays = [29, 31], Months = [2], StartDate = Today },
                new() { Title = "Other month", Kind = RecurrenceKind.Monthly, MonthDays = [29], Months = [3], StartDate = Today },
                new() { Title = "Disabled", Kind = RecurrenceKind.Daily, StartDate = Today, IsEnabled = false }
            ]
        };

        Assert.Empty(PlannerQueries.ForDate(data, Today));
        Assert.Equal(["Daily", "Weekly", "Monthly"], PlannerQueries.ForDate(data, new DateOnly(2024, 2, 29)).Select(task => task.Title));
        Assert.Equal(["Daily"], PlannerQueries.ForDate(data, new DateOnly(2025, 2, 28)).Select(task => task.Title));
        Assert.Equal(["Daily", "Other month"], PlannerQueries.ForDate(data, new DateOnly(2025, 3, 29)).Select(task => task.Title));
    }

    [Fact]
    public void ScheduleLabel_IsReadable()
    {
        Assert.Equal("Every day", PlannerQueries.ScheduleLabel(new RecurringTask { Kind = RecurrenceKind.Daily }));
        Assert.Equal("Mon, Wed", PlannerQueries.ScheduleLabel(new RecurringTask { Kind = RecurrenceKind.Weekly, WeekDays = [DayOfWeek.Wednesday, DayOfWeek.Monday] }));
        Assert.Equal("Days 1,15 • Jan,Feb", PlannerQueries.ScheduleLabel(new RecurringTask { Kind = RecurrenceKind.Monthly, MonthDays = [15, 1], Months = [2, 1] }));
    }

    [Fact]
    public async Task Store_RoundTripsEditsAndIndependentCompletions()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        await store.LoadAsync();
        var list = await store.AddListAsync(" Work ");
        await store.RenameListAsync(list.Id, "Projects");
        var listTask = new TaskItem { Title = " Plan ", ListId = list.Id };
        var datedTask = new TaskItem { Title = "Call", ScheduledDate = Today };
        await store.SaveTaskAsync(listTask);
        await store.SaveTaskAsync(datedTask);
        await store.SetTaskCompletedAsync(listTask.Id, true);
        var recurring = new RecurringTask { Title = "Review", Kind = RecurrenceKind.Daily, StartDate = Today };
        await store.SaveRecurringAsync(recurring);
        await store.SetOccurrenceCompletedAsync(recurring.Id, Today, true);
        await store.SetOccurrenceCompletedAsync(recurring.Id, Today.AddDays(1), true);
        await store.SetOccurrenceCompletedAsync(recurring.Id, Today, false);

        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Equal("Projects", reopened.Data.Lists.Single(item => item.Id == list.Id).Name);
        Assert.Equal("Plan", reopened.Data.Tasks.Single(item => item.Id == listTask.Id).Title);
        Assert.True(reopened.Data.Tasks.Single(item => item.Id == listTask.Id).IsCompleted);
        Assert.False(PlannerQueries.ForDate(reopened.Data, Today).Single(item => item.IsRecurring).IsCompleted);
        Assert.True(PlannerQueries.ForDate(reopened.Data, Today.AddDays(1)).Single().IsCompleted);
        Assert.Single(reopened.Data.Completions);

        await reopened.SetRecurringEnabledAsync(recurring.Id, false);
        Assert.Empty(PlannerQueries.ForDate(reopened.Data, Today.AddDays(1)));
        await reopened.SetRecurringEnabledAsync(recurring.Id, true);
        await reopened.DeleteRecurringAsync(recurring.Id);
        await reopened.DeleteTaskAsync(datedTask.Id);
        Assert.Empty(reopened.Data.Completions);
        Assert.DoesNotContain(reopened.Data.Tasks, item => item.Id == datedTask.Id);
    }

    [Fact]
    public async Task DeleteList_MovesTasksToGeneral_AndProtectsGeneral()
    {
        using var fixture = new StoreFixture();
        var list = await fixture.Store.AddListAsync("Custom");
        var task = new TaskItem { Title = "Task", ListId = list.Id, IsCompleted = true };
        await fixture.Store.SaveTaskAsync(task);
        await fixture.Store.DeleteListAsync(list.Id);
        Assert.Equal(PlannerStore.GeneralListId, fixture.Store.Data.Tasks.Single().ListId);
        Assert.True(fixture.Store.Data.Tasks.Single().IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.DeleteListAsync(PlannerStore.GeneralListId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.RenameListAsync(PlannerStore.GeneralListId, "Changed"));
    }

    [Fact]
    public async Task Store_RejectsInvalidValuesWithoutChangingData()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var original = store.Data;
        await Assert.ThrowsAsync<ArgumentException>(() => store.AddListAsync("  "));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AddListAsync("General"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(new TaskItem { Title = "  ", ListId = PlannerStore.GeneralListId }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(new TaskItem { Title = "Both", ListId = PlannerStore.GeneralListId, ScheduledDate = Today }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(new TaskItem { Title = "Neither" }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveTaskAsync(new TaskItem { Title = "Orphan", ListId = "missing" }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveRecurringAsync(new RecurringTask { Title = "Weekly", Kind = RecurrenceKind.Weekly, StartDate = Today }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveRecurringAsync(new RecurringTask { Title = "Monthly", Kind = RecurrenceKind.Monthly, MonthDays = [32], Months = [13], StartDate = Today }));
        Assert.Same(original, store.Data);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task LoadFailure_PreservesCorruptFileAndCurrentData()
    {
        using var fixture = new StoreFixture();
        var data = fixture.Store.Data;
        await File.WriteAllTextAsync(fixture.Path, "{broken");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.LoadAsync());
        Assert.Equal("{broken", await File.ReadAllTextAsync(fixture.Path));
        Assert.Same(data, fixture.Store.Data);
    }

    [Fact]
    public async Task ReplacingDataFile_KeepsPreviousVersionAsBackup()
    {
        using var fixture = new StoreFixture();
        await fixture.Store.AddListAsync("First");
        var previous = await File.ReadAllTextAsync(fixture.Path);
        await fixture.Store.AddListAsync("Second");

        Assert.Equal(previous, await File.ReadAllTextAsync(fixture.Path + ".bak"));
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Equal(["General", "First", "Second"], reopened.Data.Lists.Select(list => list.Name));
    }

    [Fact]
    public async Task LoadFailure_PreservesStructurallyInvalidData()
    {
        using var fixture = new StoreFixture();
        const string invalid = "{\"SchemaVersion\":1,\"Lists\":[],\"Tasks\":[],\"RecurringTasks\":[],\"Completions\":[]}";
        await File.WriteAllTextAsync(fixture.Path, invalid);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.LoadAsync());
        Assert.Equal(invalid, await File.ReadAllTextAsync(fixture.Path));
        Assert.Single(fixture.Store.Data.Lists);
    }

    [Fact]
    public async Task LoadFailure_DoesNotTreatMissingFieldsAsFreshData()
    {
        using var fixture = new StoreFixture();
        await File.WriteAllTextAsync(fixture.Path, "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.LoadAsync());
        Assert.Equal("{}", await File.ReadAllTextAsync(fixture.Path));
    }

    [Fact]
    public async Task WriteFailure_DoesNotMutateCurrentData()
    {
        using var fixture = new StoreFixture(pathIsDirectory: true);
        var before = fixture.Store.Data;
        await Assert.ThrowsAnyAsync<IOException>(() => fixture.Store.AddListAsync("New"));
        Assert.Same(before, fixture.Store.Data);
        Assert.Single(fixture.Store.Data.Lists);
    }

    [Fact]
    public async Task EditingTask_ChangesDestinationWithoutDuplicatingOrMutatingPriorSnapshot()
    {
        using var fixture = new StoreFixture();
        var task = new TaskItem { Title = "List task", ListId = PlannerStore.GeneralListId };
        await fixture.Store.SaveTaskAsync(task);
        var original = fixture.Store.Data;

        await fixture.Store.SaveTaskAsync(task with { Title = "Dated task", ListId = null, ScheduledDate = Today });
        Assert.Single(fixture.Store.Data.Tasks);
        Assert.Equal(PlannerStore.GeneralListId, original.Tasks.Single().ListId);
        Assert.Equal("Dated task", PlannerQueries.ForDate(fixture.Store.Data, Today).Single().Title);
        Assert.Empty(PlannerQueries.ForDate(fixture.Store.Data, Today.AddDays(1)));

        await fixture.Store.SaveTaskAsync(task with { Title = "Back in list" });
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Single(reopened.Data.Tasks);
        Assert.Equal("Back in list", reopened.Data.Tasks.Single().Title);
        Assert.Empty(PlannerQueries.ForDate(reopened.Data, Today));
    }

    [Fact]
    public async Task EditingRoutine_ChangesScheduleWithoutDuplicatingOrLosingMatchingCompletion()
    {
        using var fixture = new StoreFixture();
        var routine = new RecurringTask { Title = "Daily", Kind = RecurrenceKind.Daily, StartDate = Today };
        await fixture.Store.SaveRecurringAsync(routine);
        await fixture.Store.SetOccurrenceCompletedAsync(routine.Id, Today, true);
        await fixture.Store.SaveRecurringAsync(routine with
        {
            Title = "Wednesday",
            Kind = RecurrenceKind.Weekly,
            WeekDays = [DayOfWeek.Wednesday]
        });

        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Single(reopened.Data.RecurringTasks);
        var occurrence = Assert.Single(PlannerQueries.ForDate(reopened.Data, Today));
        Assert.Equal("Wednesday", occurrence.Title);
        Assert.True(occurrence.IsCompleted);
        Assert.Empty(PlannerQueries.ForDate(reopened.Data, Today.AddDays(1)));
        Assert.False(PlannerQueries.ForDate(reopened.Data, Today.AddDays(7)).Single().IsCompleted);
    }

    [Fact]
    public async Task ConcurrentSaves_PreserveEveryTaskAfterReload()
    {
        using var fixture = new StoreFixture();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(i => fixture.Store.SaveTaskAsync(
            new TaskItem { Title = $"Task {i}", ListId = PlannerStore.GeneralListId })));

        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();
        Assert.Equal(16, reopened.Data.Tasks.Length);
        Assert.Equal(16, reopened.Data.Tasks.Select(task => task.Title).Distinct().Count());
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "doing-it-tests-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public PlannerStore Store { get; }

        public StoreFixture(bool pathIsDirectory = false)
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "planner.json");
            if (pathIsDirectory) Directory.CreateDirectory(Path);
            Store = new PlannerStore(Path);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
