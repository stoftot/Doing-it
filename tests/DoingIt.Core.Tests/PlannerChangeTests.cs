using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class PlannerChangeTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public async Task Changed_FiresAfterSuccessfulSaveCompleteUndoAndDelete()
    {
        using var fixture = new StoreFixture();
        var store = fixture.Store;
        var task = new TaskItem
        {
            Id = "changed-task",
            Title = "Today",
            ScheduledDate = Today
        };
        var observedData = new List<PlannerData>();
        var observedFiles = new List<string>();
        store.Changed += (_, _) =>
        {
            observedData.Add(store.Data);
            observedFiles.Add(File.ReadAllText(fixture.Path));
        };

        await store.SaveTaskAsync(task);
        await store.SetTaskCompletedAsync(task.Id, true);
        await store.SetTaskCompletedAsync(task.Id, false);
        await store.DeleteTaskAsync(task.Id);

        Assert.Equal(4, observedData.Count);
        Assert.Equal(4, observedFiles.Count);
        Assert.Equal(
            [false, true, false],
            observedData.Take(3).Select(data => data.Tasks.Single().IsCompleted));
        Assert.Empty(observedData[3].Tasks);
        Assert.Equal("Today", observedData[0].Tasks.Single().Title);
        Assert.Equal("Today", observedData[1].Tasks.Single().Title);
        Assert.Equal("Today", observedData[2].Tasks.Single().Title);

        using var saved = System.Text.Json.JsonDocument.Parse(observedFiles[0]);
        using var completed = System.Text.Json.JsonDocument.Parse(observedFiles[1]);
        using var undone = System.Text.Json.JsonDocument.Parse(observedFiles[2]);
        using var deleted = System.Text.Json.JsonDocument.Parse(observedFiles[3]);
        Assert.Single(saved.RootElement.GetProperty("Tasks").EnumerateArray());
        Assert.True(completed.RootElement.GetProperty("Tasks")[0].GetProperty("IsCompleted").GetBoolean());
        Assert.False(undone.RootElement.GetProperty("Tasks")[0].GetProperty("IsCompleted").GetBoolean());
        Assert.Empty(deleted.RootElement.GetProperty("Tasks").EnumerateArray());
    }

    [Fact]
    public async Task Changed_DoesNotFireForRejectedValidationOrPersistenceFailure()
    {
        using var validationFixture = new StoreFixture();
        var validationStore = validationFixture.Store;
        var validationBefore = validationStore.Data;
        var validationEvents = 0;
        validationStore.Changed += (_, _) => validationEvents++;

        await Assert.ThrowsAsync<ArgumentException>(() => validationStore.SaveTaskAsync(
            new TaskItem { Title = " ", ListId = PlannerStore.GeneralListId }));

        Assert.Equal(0, validationEvents);
        Assert.Same(validationBefore, validationStore.Data);
        Assert.False(File.Exists(validationFixture.Path));

        using var persistenceFixture = new StoreFixture(pathIsDirectory: true);
        var persistenceStore = persistenceFixture.Store;
        var persistenceBefore = persistenceStore.Data;
        var persistenceEvents = 0;
        persistenceStore.Changed += (_, _) => persistenceEvents++;

        await Assert.ThrowsAnyAsync<IOException>(() => persistenceStore.SaveTaskAsync(
            new TaskItem { Title = "Cannot persist", ListId = PlannerStore.GeneralListId }));

        Assert.Equal(0, persistenceEvents);
        Assert.Same(persistenceBefore, persistenceStore.Data);
        Assert.Empty(persistenceStore.Data.Tasks);
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "doing-it-change-tests-" + Guid.NewGuid().ToString("N"));

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
