using System.Text.Json;
using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class TaskTextTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public void FromLegacy_UsesExactFitThenWholeWordAndTextElementFallbacks()
    {
        Assert.Equal(("short title", null), TaskText.FromLegacy("short title", title => title.Length <= 11));
        Assert.Equal(("make bed and", "make bed and stretch"),
            TaskText.FromLegacy("make bed and stretch", title => title.Length <= 12));
        Assert.Equal(("😀", "😀abcdef next"),
            TaskText.FromLegacy("😀abcdef next", title => title.Length <= 2));
    }

    [Fact]
    public async Task SaveTaskAndRoutine_PreserveLongDescriptionsAndAllowClearingThem()
    {
        using var fixture = new StoreFixture();
        var description = string.Join("\n", Enumerable.Repeat("Detailed notes that exceed the old limit.", 20));
        var task = new TaskItem
        {
            Id = "long-task",
            Title = "Plan trip",
            Description = description,
            ListId = PlannerStore.GeneralListId
        };
        var routine = new RecurringTask
        {
            Id = "long-routine",
            Title = "Daily review",
            Description = description,
            Kind = RecurrenceKind.Daily,
            StartDate = Today
        };

        await fixture.Store.SaveTaskAsync(task);
        await fixture.Store.SaveRecurringAsync(routine);
        var reopened = new PlannerStore(fixture.Path);
        await reopened.LoadAsync();

        Assert.Equal(description, Assert.Single(reopened.Data.Tasks).Description);
        Assert.Equal(description, Assert.Single(reopened.Data.RecurringTasks).Description);

        await reopened.SaveTaskAsync(reopened.Data.Tasks[0] with { Description = "  \n  " });
        await reopened.SaveRecurringAsync(reopened.Data.RecurringTasks[0] with { Description = null });
        var afterEdit = new PlannerStore(fixture.Path);
        await afterEdit.LoadAsync();
        Assert.Null(Assert.Single(afterEdit.Data.Tasks).Description);
        Assert.Null(Assert.Single(afterEdit.Data.RecurringTasks).Description);
    }

    [Fact]
    public async Task SaveTask_RejectsMissingTooManyLinesAndNonfittingTitlesAtomically()
    {
        using var fixture = new StoreFixture(titleFits: title => title.Length <= 8);
        var valid = new TaskItem
        {
            Id = "valid-title",
            Title = "One\nTwo",
            ListId = PlannerStore.GeneralListId
        };
        var validRoutine = new RecurringTask
        {
            Id = "valid-routine-title",
            Title = "Daily",
            Kind = RecurrenceKind.Daily,
            StartDate = Today
        };
        await fixture.Store.SaveTaskAsync(valid);
        await fixture.Store.SaveRecurringAsync(validRoutine);
        var before = fixture.Store.Data;
        var fileBefore = await File.ReadAllTextAsync(fixture.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.SaveTaskAsync(
            valid with { Title = "\r\n  " }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.SaveTaskAsync(
            valid with { Title = "One\nTwo\nThree" }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.SaveTaskAsync(
            valid with { Title = "Long word" }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.SaveRecurringAsync(
            validRoutine with { Title = "Long word" }));

        Assert.Same(before, fixture.Store.Data);
        Assert.Equal(fileBefore, await File.ReadAllTextAsync(fixture.Path));
    }

    [Fact]
    public async Task VersionThreeLoad_DoesNotRevalidateTitleAgainstChangedVisualWidth()
    {
        using var fixture = new StoreFixture(titleFits: title => title.Length <= 30);
        var task = new TaskItem { Id = "saved-title", Title = "Previously fitting title", ListId = PlannerStore.GeneralListId };
        await fixture.Store.SaveTaskAsync(task);

        var reopened = new PlannerStore(fixture.Path, title => title.Length <= 5);
        await reopened.LoadAsync();

        Assert.Equal("Previously fitting title", Assert.Single(reopened.Data.Tasks).Title);
        Assert.Null(reopened.Data.Tasks[0].Description);
    }

    [Fact]
    public async Task VersionTwoMigration_PreservesLegacyTextChecklistAndOccurrenceStateUntilNextSave()
    {
        using var fixture = new StoreFixture(titleFits: title => title.Length <= 12);
        const string versionTwo = """
            {"SchemaVersion":2,"Lists":[{"Id":"general","Name":"General"}],"Tasks":[{"Id":"task-id","Title":"A very long\nlegacy description","Description":null,"ListId":"general","IsChecklist":false,"Subtasks":[],"IsCompleted":true},{"Id":"checklist-id","Title":"Packing list","ListId":"general","IsChecklist":true,"Subtasks":[{"Id":"subtask-id","Title":"Passport","IsCompleted":true}]}],"RecurringTasks":[{"Id":"routine-id","Title":"Stretch before bed","Kind":0,"WeekDays":[],"MonthDays":[],"Months":[],"IsEnabled":false,"StartDate":"2024-02-28","IsChecklist":true,"Subtasks":[{"Id":"routine-subtask-id","Title":"Stretch","IsCompleted":false}]}],"Completions":[{"RecurringTaskId":"routine-id","Date":"2024-02-28","CompletedSubtaskIds":["routine-subtask-id"]}]}
            """;
        await File.WriteAllTextAsync(fixture.Path, versionTwo);

        var store = new PlannerStore(fixture.Path, title => title.Length <= 12);
        await store.LoadAsync();

        Assert.Equal(versionTwo, await File.ReadAllTextAsync(fixture.Path));
        Assert.False(File.Exists(fixture.Path + ".bak"));
        var migratedTask = store.Data.Tasks.Single(task => task.Id == "task-id");
        Assert.Equal("A very long", migratedTask.Title);
        Assert.Equal("A very long\nlegacy description", migratedTask.Description);
        Assert.True(migratedTask.IsCompleted);
        var checklist = store.Data.Tasks.Single(task => task.Id == "checklist-id");
        Assert.Equal("Packing list", checklist.Title);
        Assert.Equal("subtask-id", Assert.Single(checklist.Subtasks).Id);
        Assert.True(checklist.Subtasks[0].IsCompleted);
        var routine = Assert.Single(store.Data.RecurringTasks);
        Assert.Equal("routine-id", routine.Id);
        Assert.Equal("Stretch", routine.Title);
        Assert.Equal("Stretch before bed", routine.Description);
        Assert.False(routine.IsEnabled);
        Assert.Equal("routine-subtask-id", Assert.Single(routine.Subtasks).Id);
        var completion = Assert.Single(store.Data.Completions);
        Assert.Equal("routine-id", completion.RecurringTaskId);
        Assert.Equal(Today, completion.Date);
        Assert.Equal(["routine-subtask-id"], completion.CompletedSubtaskIds);

        await store.AddListAsync("After migration");
        Assert.Equal(versionTwo, await File.ReadAllTextAsync(fixture.Path + ".bak"));
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(fixture.Path)))
            Assert.Equal(3, document.RootElement.GetProperty("SchemaVersion").GetInt32());

        var reopened = new PlannerStore(fixture.Path, title => title.Length <= 12);
        await reopened.LoadAsync();
        Assert.Equal(migratedTask.Title, reopened.Data.Tasks.Single(task => task.Id == "task-id").Title);
        Assert.Equal(migratedTask.Description, reopened.Data.Tasks.Single(task => task.Id == "task-id").Description);
        Assert.Equal(routine.Title, reopened.Data.RecurringTasks.Single().Title);
        Assert.Equal(routine.Description, reopened.Data.RecurringTasks.Single().Description);
        Assert.Equal(completion.CompletedSubtaskIds, reopened.Data.Completions.Single().CompletedSubtaskIds);
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "doing-it-text-tests-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public PlannerStore Store { get; }

        public StoreFixture(Func<string, bool>? titleFits = null)
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "planner.json");
            Store = new PlannerStore(Path, titleFits);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
