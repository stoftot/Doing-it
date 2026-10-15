using DoingIt.Core;
using System.Globalization;

namespace DoingIt.Pages;

public sealed class RecurringEditorPage : EditorPage
{
    private readonly PlannerStore _store;
    private readonly RecurringTask? _existing;
    private readonly Editor _description;
    private readonly ContentView _options = new();
    private readonly Dictionary<RecurrenceKind, Button> _modes = [];
    private readonly HashSet<DayOfWeek> _weekDays;
    private readonly HashSet<int> _monthDays;
    private readonly HashSet<int> _months;
    private RecurrenceKind _kind;

    public RecurringEditorPage(PlannerStore store, RecurringTask? existing = null)
        : base(existing is null ? "A little routine" : "Edit routine", existing is null ? "Create routine" : "Save changes")
    {
        _store = store;
        _existing = existing;
        _kind = existing?.Kind ?? RecurrenceKind.Daily;
        _weekDays = existing is { WeekDays.Length: > 0 } ? [.. existing.WeekDays] : [DateTime.Today.DayOfWeek];
        _monthDays = existing is { MonthDays.Length: > 0 } ? [.. existing.MonthDays] : [DateTime.Today.Day];
        _months = existing is { Months.Length: > 0 } ? [.. existing.Months] : [.. Enumerable.Range(1, 12)];
        _description = Ui.Description(existing?.Title ?? "");
        Body.Add(Ui.Text("Give the good habits a place to grow.", 14, Ui.Muted));
        Body.Add(Ui.Field("TASK DESCRIPTION", Ui.Card(_description)));
        var modes = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 7 };
        var kinds = new[] { RecurrenceKind.Daily, RecurrenceKind.Weekly, RecurrenceKind.Monthly };
        for (var i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            var button = Ui.Button(kind.ToString(), () => { _kind = kind; RenderOptions(); return Task.CompletedTask; }, false, "Repeat" + kind);
            button.Padding = new Thickness(5, 10);
            _modes[kind] = button;
            modes.Add(button, i);
        }
        Body.Add(Ui.Field("HOW OFTEN?", modes));
        Body.Add(_options);
        RenderOptions();
        Body.Add(Ui.Text(existing is null ? "Your routine starts today. Each day gets its own checkmark." : $"Started {existing.StartDate:d MMM yyyy}. Each day has its own checkmark.", 12, Ui.Muted));
        if (existing is not null)
        {
            var delete = Ui.Quiet("Delete routine", () => Delete(() => _store.DeleteRecurringAsync(existing.Id), "This routine and its completion history will be removed. You can pause it on the Recurring tab instead."));
            delete.TextColor = Ui.Danger;
            Body.Add(delete);
        }
    }

    private void RenderOptions()
    {
        foreach (var (kind, button) in _modes)
        {
            button.BackgroundColor = kind == _kind ? Ui.Teal : Ui.Pale;
            button.TextColor = kind == _kind ? Colors.White : Ui.Teal;
        }
        if (_kind == RecurrenceKind.Daily)
        {
            _options.Content = Ui.Card(Ui.Empty("A moment, every day", "This task will appear in Today's tasks each day.", "repeat"), Ui.Pale);
            return;
        }
        if (_kind == RecurrenceKind.Weekly)
        {
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
            _options.Content = Ui.Field("ON THESE DAYS", Choices(days, 4, d => d.ToString()[..3], _weekDays), "Choose one or more days of the week.");
            return;
        }
        _options.Content = Ui.Stack(
            Ui.Field("DAYS OF THE MONTH", Choices(Enumerable.Range(1, 31), 7, d => d.ToString(), _monthDays)),
            Ui.Field("IN THESE MONTHS", Choices(Enumerable.Range(1, 12), 4, m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m), _months)),
            Ui.Text("Choose one or more dates and months. Dates that don't exist in a month (like February 31) are skipped.", 12, Ui.Muted));
    }

    private static Grid Choices<T>(IEnumerable<T> values, int columns, Func<T, string> label, HashSet<T> selected) where T : notnull
    {
        var grid = new Grid { ColumnSpacing = 5, RowSpacing = 7 };
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new(GridLength.Star));
        var index = 0;
        foreach (var value in values)
        {
            if (index % columns == 0) grid.RowDefinitions.Add(new(GridLength.Auto));
            Button? button = null;
            void Style()
            {
                button!.BackgroundColor = selected.Contains(value) ? Ui.Teal : Colors.White;
                button.TextColor = selected.Contains(value) ? Colors.White : Ui.Ink;
                SemanticProperties.SetDescription(button, label(value) + (selected.Contains(value) ? ", selected" : ", not selected"));
            }
            button = Ui.Button(label(value), () =>
            {
                if (!selected.Add(value)) selected.Remove(value);
                Style();
                return Task.CompletedTask;
            }, false);
            button.Padding = new Thickness(0);
            button.FontSize = 12;
            button.MinimumHeightRequest = 44;
            button.CornerRadius = 12;
            Style();
            grid.Add(button, index % columns, index / columns);
            index++;
        }
        return grid;
    }

    protected override Task Persist() => _store.SaveRecurringAsync(new RecurringTask
    {
        Id = _existing?.Id ?? Guid.NewGuid().ToString("N"),
        Title = _description.Text ?? "",
        Kind = _kind,
        WeekDays = [.. _weekDays.OrderBy(d => ((int)d + 6) % 7)],
        MonthDays = [.. _monthDays.Order()],
        Months = [.. _months.Order()],
        IsEnabled = _existing?.IsEnabled ?? true,
        StartDate = _existing?.StartDate ?? DateOnly.FromDateTime(DateTime.Today)
    });
}
