namespace DoingIt.Core;

public sealed record TodayProgress(int Completed, int Total)
{
    public int Remaining => Total - Completed;
    public bool IsAllDone => Total > 0 && Remaining == 0;

    public static TodayProgress ForDate(PlannerData data, DateOnly date)
    {
        var tasks = PlannerQueries.ForDate(data, date);
        return new(tasks.Count(task => task.IsCompleted), tasks.Count);
    }
}
