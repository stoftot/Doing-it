using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class TodayProgressTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public void ForDate_CountsCompletedAndRemainingDatedTasks()
    {
        var data = new PlannerData
        {
            Tasks = Enumerable.Range(0, 10)
                .Select(index => new TaskItem
                {
                    Title = $"Task {index}",
                    ScheduledDate = Today,
                    IsCompleted = index < 6
                })
                .ToArray()
        };

        var progress = TodayProgress.ForDate(data, Today);

        Assert.Equal(6, progress.Completed);
        Assert.Equal(10, progress.Total);
        Assert.Equal(4, progress.Remaining);
        Assert.False(progress.IsAllDone);
    }

    [Fact]
    public void ForDate_AllDueTasksCompleted_SetsAllDone()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "Done", ScheduledDate = Today, IsCompleted = true },
                new() { Title = "Also done", ScheduledDate = Today, IsCompleted = true }
            ]
        };

        var progress = TodayProgress.ForDate(data, Today);

        Assert.Equal(2, progress.Completed);
        Assert.Equal(2, progress.Total);
        Assert.Equal(0, progress.Remaining);
        Assert.True(progress.IsAllDone);
    }

    [Fact]
    public void ForDate_NoDueTasks_HasZeroProgressAndIsNotAllDone()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "List task", ListId = PlannerStore.GeneralListId, IsCompleted = true },
                new() { Title = "Yesterday", ScheduledDate = Today.AddDays(-1), IsCompleted = true },
                new() { Title = "Tomorrow", ScheduledDate = Today.AddDays(1) }
            ]
        };

        var progress = TodayProgress.ForDate(data, Today);

        Assert.Equal(0, progress.Completed);
        Assert.Equal(0, progress.Total);
        Assert.Equal(0, progress.Remaining);
        Assert.False(progress.IsAllDone);
    }

    [Fact]
    public void ForDate_CountsOnlyScheduledTasksAndOccurrencesDueOnRequestedDate()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "List task", ListId = PlannerStore.GeneralListId, IsCompleted = true },
                new() { Title = "Today", ScheduledDate = Today, IsCompleted = true },
                new() { Title = "Tomorrow", ScheduledDate = Today.AddDays(1) }
            ],
            RecurringTasks =
            [
                new() { Title = "Active today", Kind = RecurrenceKind.Daily, StartDate = Today },
                new() { Title = "Paused", Kind = RecurrenceKind.Daily, StartDate = Today, IsEnabled = false },
                new() { Title = "Not started", Kind = RecurrenceKind.Daily, StartDate = Today.AddDays(1) },
                new() { Title = "Wrong weekday", Kind = RecurrenceKind.Weekly, WeekDays = [DayOfWeek.Friday], StartDate = Today }
            ]
        };

        var progress = TodayProgress.ForDate(data, Today);

        Assert.Equal(1, progress.Completed);
        Assert.Equal(2, progress.Total);
        Assert.Equal(1, progress.Remaining);
    }

    [Fact]
    public void ForDate_CountsChecklistAsOneTaskAndCompletionRequiresEverySubtask()
    {
        var checklist = new TaskItem
        {
            Title = "Checklist",
            ScheduledDate = Today,
            IsChecklist = true,
            Subtasks =
            [
                new() { Title = "First", IsCompleted = true },
                new() { Title = "Second", IsCompleted = false }
            ]
        };
        var data = new PlannerData { Tasks = [checklist] };

        var partial = TodayProgress.ForDate(data, Today);
        var complete = TodayProgress.ForDate(data with
        {
            Tasks = [checklist with { Subtasks = checklist.Subtasks.Select(subtask => subtask with { IsCompleted = true }).ToArray() }]
        }, Today);

        Assert.Equal(0, partial.Completed);
        Assert.Equal(1, partial.Total);
        Assert.Equal(1, partial.Remaining);
        Assert.False(partial.IsAllDone);
        Assert.Equal(1, complete.Completed);
        Assert.Equal(1, complete.Total);
        Assert.Equal(0, complete.Remaining);
        Assert.True(complete.IsAllDone);
    }

    [Fact]
    public void ForDate_RecurringChecklistCompletionIsScopedToEachDate()
    {
        var routine = new RecurringTask
        {
            Id = "daily-checklist",
            Title = "Daily checklist",
            Kind = RecurrenceKind.Daily,
            StartDate = Today,
            IsChecklist = true,
            Subtasks = [new() { Id = "first", Title = "First" }, new() { Id = "second", Title = "Second" }]
        };
        var data = new PlannerData
        {
            RecurringTasks = [routine],
            Completions =
            [
                new() { RecurringTaskId = routine.Id, Date = Today, CompletedSubtaskIds = ["first", "second"] },
                new() { RecurringTaskId = routine.Id, Date = Today.AddDays(1), CompletedSubtaskIds = ["first"] }
            ]
        };

        var today = TodayProgress.ForDate(data, Today);
        var tomorrow = TodayProgress.ForDate(data, Today.AddDays(1));

        Assert.Equal((1, 1, 0, true), (today.Completed, today.Total, today.Remaining, today.IsAllDone));
        Assert.Equal((0, 1, 1, false), (tomorrow.Completed, tomorrow.Total, tomorrow.Remaining, tomorrow.IsAllDone));
    }
}
