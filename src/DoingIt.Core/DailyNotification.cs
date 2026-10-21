using System.Text;

namespace DoingIt.Core;

public sealed record DailyNotificationSummary(DateOnly Date, int Total, IReadOnlyList<DueTask> Remaining)
{
    private const int MaximumBodyLength = 3500;
    private const int MaximumVisibleTasks = 6;

    public string Title => Total == 0
        ? "No tasks today"
        : Remaining.Count == 0
            ? "All done for today"
            : $"{Remaining.Count} task{(Remaining.Count == 1 ? "" : "s")} left today";

    public string Text => Total == 0
        ? "Nothing is scheduled for today."
        : Remaining.Count == 0
            ? $"All {Total} task{(Total == 1 ? "" : "s")} completed."
            : $"{Remaining.Count} of {Total} task{(Total == 1 ? "" : "s")} remaining.";

    public string Body
    {
        get
        {
            if (Remaining.Count == 0)
                return Text;

            var lines = Remaining.Select(FormatTask).ToArray();
            var prefixLength = 0;
            var bestIncludedCount = 0;
            var bestBodyLength = int.MaxValue;

            // Android bounds expanded notification height. Keep a compact list
            // and explicitly point to Home when there are more tasks to read.
            for (var count = 0; count <= Math.Min(lines.Length, MaximumVisibleTasks); count++)
            {
                if (count > 0)
                    prefixLength += lines[count - 1].Length + (count > 1 ? Environment.NewLine.Length : 0);

                var omitted = lines.Length - count;
                var suffixLength = omitted == 0
                    ? 0
                    : Environment.NewLine.Length + OmittedTasksLine(omitted).Length;
                var candidateLength = prefixLength + suffixLength;
                if (candidateLength <= MaximumBodyLength)
                {
                    bestIncludedCount = count;
                    bestBodyLength = candidateLength;
                }
            }

            var body = new StringBuilder(bestBodyLength);
            for (var index = 0; index < bestIncludedCount; index++)
            {
                if (index > 0)
                    body.AppendLine();
                body.Append(lines[index]);
            }

            var includedCount = lines.Length - bestIncludedCount;
            if (includedCount > 0)
            {
                if (body.Length > 0)
                    body.AppendLine();
                body.Append(OmittedTasksLine(includedCount));
            }

            return body.ToString();
        }
    }

    public static DailyNotificationSummary ForDate(PlannerData data, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(data);
        var due = PlannerQueries.ForDate(data, date);
        return new(date, due.Count, due.Where(task => !task.IsCompleted).ToArray());
    }

    private static string FormatTask(DueTask task)
    {
        var title = task.Title.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return task.IsChecklist
            ? $"• {title} ({task.ProgressLabel})"
            : $"• {title}";
    }

    private static string OmittedTasksLine(int count) =>
        $"… and {count} more. Open Doing it to see all tasks.";
}

public static class DailyNotificationSchedule
{
    public static DateTimeOffset NextMidnight(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var nextDate = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        var midnight = nextDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(midnight))
            midnight = FirstValidTimeAtOrAfter(zone, midnight);

        if (zone.IsAmbiguousTime(midnight))
        {
            var futureInstants = zone.GetAmbiguousTimeOffsets(midnight)
                .Select(offset => new DateTimeOffset(midnight, offset))
                .Where(candidate => candidate > now)
                .OrderBy(candidate => candidate.UtcDateTime)
                .ToArray();

            if (futureInstants.Length > 0)
                return futureInstants[0];
        }

        var utcOffset = zone.GetUtcOffset(midnight);
        return new DateTimeOffset(midnight, utcOffset);
    }

    private static DateTime FirstValidTimeAtOrAfter(TimeZoneInfo zone, DateTime invalidTime)
    {
        var upper = invalidTime;
        do
        {
            upper = upper.AddMinutes(1);
        } while (zone.IsInvalidTime(upper));

        var lowerTicks = invalidTime.Ticks;
        var upperTicks = upper.Ticks;
        while (upperTicks - lowerTicks > 1)
        {
            var middleTicks = lowerTicks + ((upperTicks - lowerTicks) / 2);
            if (zone.IsInvalidTime(new DateTime(middleTicks, DateTimeKind.Unspecified)))
                lowerTicks = middleTicks;
            else
                upperTicks = middleTicks;
        }

        return new DateTime(upperTicks, DateTimeKind.Unspecified);
    }
}
