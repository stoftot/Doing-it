using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class CalendarPage : ContentPage
{
    private readonly PlannerStore _store;
    private readonly VerticalStackLayout _body = new() { Spacing = 20, Padding = new Thickness(22, 4, 22, 28) };
    private readonly Grid _root = new() { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
    private TaskDetailsOverlay? _details;
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Today);
    private bool _month = true;
    private bool _showCompleted;
    private bool _busy;

    public CalendarPage(PlannerStore store)
    {
        _store = store;
        Ui.ConfigurePage(this);
        var header = Ui.ModalHeader("A little look ahead", () => _busy ? Task.CompletedTask : ModalNavigation.CloseAsync(this));
        header.Margin = new Thickness(22, 12, 16, 12);
        _root.Add(header);
        _root.Add(new ScrollView { Content = _body }, 0, 1);
        Content = _root;
    }

    protected override void OnAppearing() { base.OnAppearing(); Render(); }

    private void Render()
    {
        _body.Clear();
        _body.Add(Ui.Text("A little planning. A little peace of mind.", 13, Ui.Muted));
        var modes = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        modes.Add(Ui.Button("Month", () => { _month = true; Render(); return Task.CompletedTask; }, _month, "MonthView"));
        modes.Add(Ui.Button("Week", () => { _month = false; Render(); return Task.CompletedTask; }, !_month, "WeekView"), 1);
        modes.Add(Ui.Quiet("Today", () => { _selected = DateOnly.FromDateTime(DateTime.Today); Render(); return Task.CompletedTask; }), 2);
        _body.Add(modes);
        var calendar = Ui.Stack();
        var arrows = new HorizontalStackLayout { Spacing = 0 };
        arrows.Add(Ui.Icon("previous", "Previous period", () => Move(-1)));
        arrows.Add(Ui.Icon("next", "Next period", () => Move(1)));
        calendar.Add(Ui.Between(Ui.Text(_selected.ToString("MMMM yyyy"), 19, bold: true), arrows));
        var grid = new Grid { ColumnSpacing = 3, RowSpacing = 5 };
        for (var c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new(GridLength.Star));
        grid.RowDefinitions.Add(new(GridLength.Auto));
        var weekdays = new[] { "M", "T", "W", "T", "F", "S", "S" };
        for (var c = 0; c < 7; c++)
        {
            var dayLabel = Ui.Text(weekdays[c], 11, Ui.Muted, true);
            dayLabel.HorizontalTextAlignment = TextAlignment.Center;
            dayLabel.Margin = new Thickness(0, 0, 0, 8);
            grid.Add(dayLabel, c);
        }
        var first = _month ? new DateOnly(_selected.Year, _selected.Month, 1) : _selected;
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var count = _month ? (int)Math.Ceiling((((int)first.DayOfWeek + 6) % 7 + DateTime.DaysInMonth(first.Year, first.Month)) / 7d) * 7 : 7;
        for (var i = 0; i < count; i++)
        {
            if (i % 7 == 0) grid.RowDefinitions.Add(new(GridLength.Auto));
            var day = start.AddDays(i);
            var selected = day == _selected;
            var today = day == DateOnly.FromDateTime(DateTime.Today);
            var hasTasks = PlannerQueries.ForDate(_store.Data, day).Count > 0;
            var number = Ui.Text(day.Day.ToString(), 14, selected ? Colors.White : day.Month != _selected.Month && _month ? Ui.Muted : Ui.Ink, today || selected);
            number.HorizontalTextAlignment = TextAlignment.Center;
            var dot = Ui.Text(hasTasks ? "•" : " ", 10, selected ? Ui.Blush : Ui.Teal);
            dot.HorizontalTextAlignment = TextAlignment.Center;
            var cellBody = Ui.Stack(number, dot);
            cellBody.Spacing = 0;
            var cell = Ui.Card(cellBody, selected ? Ui.Teal : today ? Ui.Pale : Colors.Transparent, 12, new Thickness(0, 7));
            cell.MinimumHeightRequest = 48;
            SemanticProperties.SetDescription(cell, day.ToString("dddd d MMMM yyyy") + (hasTasks ? ", tasks planned" : "") + (selected ? ", selected" : ""));
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { _selected = day; Render(); };
            cell.GestureRecognizers.Add(tap);
            grid.Add(cell, i % 7, i / 7 + 1);
        }
        calendar.Add(grid);
        calendar.Add(Ui.Text("•  Tasks planned", 11, Ui.Muted));
        _body.Add(Ui.Card(calendar, padding: new Thickness(14)));

        var due = PlannerQueries.ForDate(_store.Data, _selected);
        var completed = due.Count(t => t.IsCompleted);
        var heading = Ui.Stack(Ui.Eyebrow(_selected == DateOnly.FromDateTime(DateTime.Today) ? "TODAY" : _selected.ToString("dddd")), Ui.Text(_selected.ToString("d MMMM"), 24, bold: true));
        heading.Spacing = 3;
        _body.Add(Ui.Between(heading, Ui.Quiet($"✓ {completed} done", () => { _showCompleted = !_showCompleted; Render(); return Task.CompletedTask; }, "CalendarCompleted")));
        var tasks = Ui.Stack();
        var displayed = due.Where(t => t.IsCompleted == _showCompleted).ToArray();
        if (_showCompleted) tasks.Add(Ui.Eyebrow("COMPLETED TASKS"));
        if (displayed.Length == 0)
            tasks.Add(Ui.Empty(_showCompleted ? "Nothing completed yet" : "A little breathing room", _showCompleted ? "Check off a task to see it here." : "No tasks planned for this day."));
        foreach (var task in displayed)
        {
            if (task.IsChecklist)
            {
                tasks.Add(MainPage.ChecklistRow(task, () => ModalNavigation.PushAsync(this, () => new ChecklistPage(_store, task.Id, task.IsRecurring, _selected)), () => ShowDetails(task)));
                continue;
            }
            var check = new CheckBox { Color = Ui.Teal, IsChecked = task.IsCompleted, WidthRequest = 44, HeightRequest = 48 };
            SemanticProperties.SetDescription(check, (task.IsCompleted ? "Undo " : "Complete ") + task.Title);
            check.CheckedChanged += async (_, args) => await Toggle(task, args.Value);
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 4 };
            row.Add(check);
            var text = Ui.Stack(Ui.TaskTitle(task.Title, () => ShowDetails(task), task.IsCompleted ? Ui.Muted : Ui.Ink));
            text.VerticalOptions = LayoutOptions.Center;
            text.Spacing = 2;
            if (task.IsRecurring) text.Add(Ui.Text("Recurring", 10, Ui.Muted));
            row.Add(text, 1);
            row.Add(Ui.Icon("edit", "Edit " + task.Title, () => task.IsRecurring
                ? ModalNavigation.PushAsync(this, () => new RecurringEditorPage(_store, _store.Data.RecurringTasks.First(t => t.Id == task.Id)))
                : ModalNavigation.PushAsync(this, () => new TaskEditorPage(_store, _store.Data.Tasks.First(t => t.Id == task.Id)))), 2);
            tasks.Add(row);
        }
        _body.Add(Ui.Card(tasks));
        _body.Add(Ui.Button("+  Add task for this day", () => ModalNavigation.PushAsync(this, () => new TaskEditorPage(_store, date: _selected)), id: "AddCalendarTask"));
        _body.Add(Ui.Text("List tasks live in your lists. Only dated tasks and active routines appear here.", 12, Ui.Muted));
    }

    private Task Move(int direction)
    {
        _selected = _month ? _selected.AddMonths(direction) : _selected.AddDays(direction * 7);
        Render();
        return Task.CompletedTask;
    }

    private Task ShowDetails(DueTask task)
    {
        if (_details is not null) return Task.CompletedTask;
        foreach (var child in _root.Children.OfType<View>()) child.IsEnabled = false;
        _details = new TaskDetailsOverlay(_store.Data, task.Id, task.IsRecurring, CloseDetails);
        _root.Add(_details);
        Grid.SetRowSpan(_details, 2);
        return Task.CompletedTask;
    }

    private void CloseDetails()
    {
        if (_details is null) return;
        _root.Remove(_details);
        _details = null;
        foreach (var child in _root.Children.OfType<View>()) child.IsEnabled = true;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_busy) return true;
        if (_details is null) return ModalNavigation.HandleBack(this);
        CloseDetails();
        return true;
    }

    private async Task Toggle(DueTask task, bool completed)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (task.IsRecurring) await _store.SetOccurrenceCompletedAsync(task.Id, _selected, completed);
            else await _store.SetTaskCompletedAsync(task.Id, completed);
        }
        catch (Exception ex) { await Ui.Error(this, ex); }
        finally { _busy = false; Render(); }
    }
}
