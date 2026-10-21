using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class DailyNotificationTests
{
    private static readonly DateOnly Today = new(2024, 2, 28);

    [Fact]
    public void ForDate_IncludesScheduledTasksAndActiveRecurringOccurrencesOnly()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "List task", ListId = PlannerStore.GeneralListId },
                new() { Title = "Yesterday", ScheduledDate = Today.AddDays(-1) },
                new() { Title = "Today", ScheduledDate = Today },
                new() { Title = "Tomorrow", ScheduledDate = Today.AddDays(1) }
            ],
            RecurringTasks =
            [
                new() { Title = "Daily active", Kind = RecurrenceKind.Daily, StartDate = Today },
                new() { Title = "Weekly active", Kind = RecurrenceKind.Weekly, WeekDays = [Today.DayOfWeek], StartDate = Today },
                new() { Title = "Monthly active", Kind = RecurrenceKind.Monthly, MonthDays = [Today.Day], Months = [Today.Month], StartDate = Today },
                new() { Title = "Paused", Kind = RecurrenceKind.Daily, StartDate = Today, IsEnabled = false },
                new() { Title = "Not started", Kind = RecurrenceKind.Daily, StartDate = Today.AddDays(1) },
                new() { Title = "Wrong weekday", Kind = RecurrenceKind.Weekly, WeekDays = [DayOfWeek.Friday], StartDate = Today }
            ]
        };

        var summary = DailyNotificationSummary.ForDate(data, Today);

        Assert.Equal(Today, summary.Date);
        Assert.Equal(4, summary.Total);
        Assert.Equal(["Today", "Daily active", "Weekly active", "Monthly active"], summary.Remaining.Select(task => task.Title));
    }

    [Fact]
    public void ForDate_ExcludesCompletedTasksButKeepsPartiallyCheckedChecklistWithProgress()
    {
        var data = new PlannerData
        {
            Tasks =
            [
                new() { Title = "Completed", ScheduledDate = Today, IsCompleted = true },
                new()
                {
                    Title = "Pack\nfor trip",
                    ScheduledDate = Today,
                    IsChecklist = true,
                    Subtasks = [new() { Title = "Keys", IsCompleted = true }, new() { Title = "Wallet" }, new() { Title = "Tickets" }]
                }
            ]
        };

        var summary = DailyNotificationSummary.ForDate(data, Today);

        var remaining = Assert.Single(summary.Remaining);
        Assert.True(remaining.IsChecklist);
        Assert.Equal("• Pack for trip (1 of 3 done)", summary.Body);
        Assert.Equal(2, summary.Total);
        Assert.Equal("1 task left today", summary.Title);
        Assert.Equal("1 of 2 tasks remaining.", summary.Text);
    }

    [Fact]
    public void ForDate_RecurringCompletionIsDateSpecificAndChecklistProgressChangesPerOccurrence()
    {
        var routine = new RecurringTask
        {
            Id = "daily-checklist",
            Title = "Daily prep",
            Kind = RecurrenceKind.Daily,
            StartDate = Today,
            IsChecklist = true,
            Subtasks = [new() { Id = "keys", Title = "Keys" }, new() { Id = "wallet", Title = "Wallet" }]
        };
        var data = new PlannerData
        {
            RecurringTasks = [routine],
            Completions =
            [
                new() { RecurringTaskId = routine.Id, Date = Today, CompletedSubtaskIds = ["keys", "wallet"] },
                new() { RecurringTaskId = routine.Id, Date = Today.AddDays(1), CompletedSubtaskIds = ["keys"] }
            ]
        };

        var completedToday = DailyNotificationSummary.ForDate(data, Today);
        var partialTomorrow = DailyNotificationSummary.ForDate(data, Today.AddDays(1));

        Assert.Empty(completedToday.Remaining);
        Assert.Equal("1 task left today", partialTomorrow.Title);
        Assert.Equal("• Daily prep (1 of 2 done)", partialTomorrow.Body);
        Assert.False(partialTomorrow.Remaining.Single().IsCompleted);
    }

    [Fact]
    public void ForDate_DistinguishesNoTasksFromAllTasksCompleted()
    {
        var empty = DailyNotificationSummary.ForDate(new PlannerData(), Today);
        var allDone = DailyNotificationSummary.ForDate(new PlannerData
        {
            Tasks = [new() { Title = "Finished", ScheduledDate = Today, IsCompleted = true }]
        }, Today);

        Assert.Equal(0, empty.Total);
        Assert.Equal("No tasks today", empty.Title);
        Assert.Equal("Nothing is scheduled for today.", empty.Text);
        Assert.Equal(empty.Text, empty.Body);
        Assert.Equal(1, allDone.Total);
        Assert.Empty(allDone.Remaining);
        Assert.Equal("All done for today", allDone.Title);
        Assert.Equal("All 1 task completed.", allDone.Text);
        Assert.Equal(allDone.Text, allDone.Body);
    }

    [Fact]
    public void Body_WhenTooLong_OmitsWholeLinesAndReportsOmittedCount()
    {
        var tasks = Enumerable.Range(0, 30)
            .Select(index => new TaskItem { Title = $"{index:D2} {new string('x', 220)}", ScheduledDate = Today })
            .ToArray();

        var body = DailyNotificationSummary.ForDate(new PlannerData { Tasks = tasks }, Today).Body;

        Assert.True(body.Length <= 3500);
        Assert.Contains($"… and {30 - body.Split(Environment.NewLine).Length + 1} more. Open Doing it to see all tasks.", body);
        Assert.DoesNotContain("29 ", body);
    }

    [Fact]
    public void Body_WhenManyShortTasks_ReportsTasksOutsideTheExpandedPreview()
    {
        var data = new PlannerData
        {
            Tasks = Enumerable.Range(1, 9).Select(index => new TaskItem { Title = $"Task {index}", ScheduledDate = Today }).ToArray()
        };
        var summary = DailyNotificationSummary.ForDate(data, Today);

        Assert.Equal(9, summary.Remaining.Count);
        Assert.Contains("• Task 6", summary.Body);
        Assert.DoesNotContain("• Task 7", summary.Body);
        Assert.EndsWith("… and 3 more. Open Doing it to see all tasks.", summary.Body);
    }

    [Fact]
    public void NextMidnight_UsesProvidedLocalTimeZoneAndMovesForwardWhenAlreadyAtMidnight()
    {
        var zone = CreateEuropeanDstZone();
        var now = new DateTimeOffset(2024, 2, 28, 23, 0, 0, TimeSpan.Zero); // 00:00 on Feb 29 in the supplied zone.

        var next = DailyNotificationSchedule.NextMidnight(now, zone);

        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeZoneInfo.ConvertTime(next, zone).DateTime);
        Assert.True(next > now);
    }

    [Fact]
    public void NextMidnight_AccountsForSpringAndFallDstDayLengths()
    {
        var zone = CreateEuropeanDstZone();
        var beforeSpringMidnight = new DateTimeOffset(2024, 3, 30, 22, 30, 0, TimeSpan.Zero);
        var springMidnight = DailyNotificationSchedule.NextMidnight(beforeSpringMidnight, zone);
        var afterSpringMidnight = DailyNotificationSchedule.NextMidnight(springMidnight.AddSeconds(1), zone);
        var beforeFallMidnight = new DateTimeOffset(2024, 10, 26, 21, 30, 0, TimeSpan.Zero);
        var fallMidnight = DailyNotificationSchedule.NextMidnight(beforeFallMidnight, zone);
        var afterFallMidnight = DailyNotificationSchedule.NextMidnight(fallMidnight.AddSeconds(1), zone);

        Assert.Equal(TimeSpan.FromHours(23), afterSpringMidnight - springMidnight);
        Assert.Equal(TimeSpan.FromHours(25), afterFallMidnight - fallMidnight);
        Assert.Equal(TimeSpan.FromHours(2), afterSpringMidnight.Offset);
        Assert.Equal(TimeSpan.FromHours(1), afterFallMidnight.Offset);
    }

    [Fact]
    public void NextMidnight_WhenLocalMidnightIsInvalid_UsesFirstValidLocalInstant()
    {
        var zone = CreateMidnightGapZone();
        var now = new DateTimeOffset(2024, 3, 30, 23, 0, 0, TimeSpan.Zero);

        var next = DailyNotificationSchedule.NextMidnight(now, zone);

        Assert.Equal(new DateTime(2024, 3, 31, 1, 0, 0, DateTimeKind.Unspecified), TimeZoneInfo.ConvertTime(next, zone).DateTime);
    }

    [Fact]
    public void NextMidnight_WhenLocalMidnightIsAmbiguous_ChoosesEarliestFutureInstant()
    {
        var zone = CreateMidnightOverlapZone();
        var now = new DateTimeOffset(2024, 10, 26, 21, 0, 0, TimeSpan.Zero);

        var next = DailyNotificationSchedule.NextMidnight(now, zone);

        Assert.Equal(TimeSpan.FromHours(2), next.Offset);
        Assert.Equal(new DateTime(2024, 10, 27, 0, 0, 0, DateTimeKind.Unspecified), TimeZoneInfo.ConvertTime(next, zone).DateTime);
    }

    private static TimeZoneInfo CreateEuropeanDstZone()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("DoingIt-Europe-DST", TimeSpan.FromHours(1), "DoingIt Europe", "DoingIt Standard", "DoingIt Daylight", [rule]);
    }

    private static TimeZoneInfo CreateMidnightGapZone()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 31);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 27);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("DoingIt-Midnight-Gap", TimeSpan.Zero, "DoingIt Gap", "DoingIt Standard", "DoingIt Daylight", [rule]);
    }

    private static TimeZoneInfo CreateMidnightOverlapZone()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 31);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 27);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("DoingIt-Midnight-Overlap", TimeSpan.FromHours(1), "DoingIt Overlap", "DoingIt Standard", "DoingIt Daylight", [rule]);
    }
}
