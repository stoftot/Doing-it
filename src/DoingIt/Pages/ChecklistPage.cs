using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class ChecklistPage : ContentPage
{
    private readonly PlannerStore _store;
    private readonly string _id;
    private readonly bool _recurring;
    private readonly DateOnly? _date;
    private readonly Grid _root = new()
    {
        RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)],
        RowSpacing = 20,
        Padding = new Thickness(24, 12, 24, 18)
    };
    private VerticalStackLayout _rows = null!;
    private readonly Dictionary<string, (CheckBox Check, Label Title, Border Card)> _items = [];
    private Label _progress = null!;
    private Label _hint = null!;
    private ProgressBar _progressBar = null!;
    private bool _busy;
    private bool _refreshing;
    private bool _canCheck;

    public ChecklistPage(PlannerStore store, string id, bool recurring = false, DateOnly? date = null)
    {
        _store = store;
        _id = id;
        _recurring = recurring;
        _date = date;
        Ui.ConfigurePage(this);
        Content = _root;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        _root.Clear();
        _items.Clear();
        _rows = new VerticalStackLayout { Spacing = 8 };
        _progress = Ui.Text("", 16, Ui.Teal, true);
        _progress.AutomationId = "ChecklistProgress";
        _hint = Ui.Text("", 13, Ui.Muted);
        _hint.AutomationId = "ChecklistHint";
        _progressBar = new ProgressBar { ProgressColor = Ui.Teal, BackgroundColor = Ui.Line, HeightRequest = 6, AutomationId = "ChecklistProgressBar" };
        var routine = _recurring ? _store.Data.RecurringTasks.FirstOrDefault(task => task.Id == _id) : null;
        var task = !_recurring ? _store.Data.Tasks.FirstOrDefault(item => item.Id == _id) : null;
        if (routine is null && task is null)
        {
            _root.Add(Ui.Card(Ui.Stack(Ui.Text("This task was deleted."), Ui.Button("Close", Close)), padding: new Thickness(20)));
            return;
        }
        var title = routine?.Title ?? task!.Title;
        var completion = _recurring && _date.HasValue
            ? _store.Data.Completions.FirstOrDefault(item => item.RecurringTaskId == _id && item.Date == _date) : null;
        var subtasks = routine?.Subtasks ?? task!.Subtasks;
        bool Done(Subtask subtask) => _recurring ? completion?.CompletedSubtaskIds.Contains(subtask.Id) == true : subtask.IsCompleted;
        _canCheck = !_recurring || _date.HasValue && PlannerQueries.ForDate(_store.Data, _date.Value).Any(item => item.Id == _id && item.IsRecurring);
        foreach (var subtask in subtasks)
        {
            var check = new CheckBox { IsChecked = Done(subtask), Color = Ui.Teal, WidthRequest = 48, HeightRequest = 48, IsEnabled = _canCheck };
            check.AutomationId = "CheckSubtask" + subtask.Id;
            check.CheckedChanged += async (_, args) => await Toggle(subtask.Id, args.Value);
            var label = Ui.Text(subtask.Title, 16);
            label.AutomationId = "ChecklistItemTitle" + subtask.Id;
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8, MinimumHeightRequest = 56 };
            row.Add(check);
            row.Add(label, 1);
            var card = Ui.Card(row, radius: 16, padding: new Thickness(6, 6, 16, 6));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => { if (_canCheck) await Toggle(subtask.Id, !check.IsChecked); };
            card.GestureRecognizers.Add(tap);
            _items.Add(subtask.Id, (check, label, card));
            _rows.Add(card);
        }
        var heading = Ui.Text(title, 24, bold: true);
        heading.AutomationId = "ChecklistTitle";
        var location = _recurring
            ? _date.HasValue ? _date.Value.ToString("dddd, d MMM yyyy") : "Routine template"
            : task!.ScheduledDate is DateOnly date ? date.ToString("dddd, d MMM yyyy")
            : _store.Data.Lists.First(list => list.Id == task.ListId).Name;
        var header = Ui.Stack(Ui.Between(Ui.Eyebrow("To-do list"), Ui.Icon("close", "Close", Close)), heading, Ui.Text(location, 13, Ui.Muted));
        header.Spacing = 6;
        _root.Add(header);
        var summary = Ui.Card(Ui.Stack(_progress, _progressBar, _hint), Ui.Pale, 18, new Thickness(16));
        var content = Ui.Stack(summary, _rows);
        if (!_canCheck) content.Insert(1, Ui.Text("This is your routine's item list. Check items on a scheduled date in Home or the calendar.", 13, Ui.Muted));
        _root.Add(new ScrollView { Content = content, AutomationId = "ChecklistScroll" }, 0, 1);
        _root.Add(Ui.Between(Ui.Quiet("Edit " + (_recurring ? "routine" : "to-do list"), Edit, "EditChecklist"), Ui.Button("Close", Close, id: "CloseChecklist")), 0, 2);
        RefreshProgress();
    }

    private void RefreshProgress()
    {
        var subtasks = _recurring
            ? _store.Data.RecurringTasks.First(task => task.Id == _id).Subtasks
            : _store.Data.Tasks.First(task => task.Id == _id).Subtasks;
        var completion = _recurring && _date.HasValue
            ? _store.Data.Completions.FirstOrDefault(item => item.RecurringTaskId == _id && item.Date == _date) : null;
        var done = 0;
        _refreshing = true;
        foreach (var subtask in subtasks)
        {
            var completed = _recurring ? completion?.CompletedSubtaskIds.Contains(subtask.Id) == true : subtask.IsCompleted;
            if (completed) done++;
            var row = _items[subtask.Id];
            row.Check.IsChecked = completed;
            SemanticProperties.SetDescription(row.Check, (completed ? "Reopen " : "Complete ") + subtask.Title);
            row.Title.TextColor = completed ? Ui.Muted : Ui.Ink;
            row.Title.TextDecorations = completed ? TextDecorations.Strikethrough : TextDecorations.None;
            row.Card.BackgroundColor = completed ? Ui.Pale : Colors.White;
        }
        _refreshing = false;
        _progress.Text = $"{done} of {subtasks.Length} done";
        _progressBar.Progress = (double)done / subtasks.Length;
        _hint.Text = !_canCheck ? "Each occurrence starts with a fresh list."
            : done == subtasks.Length ? "All done. A little more off your mind."
            : "One item at a time. Your progress is saved as you go.";
    }

    private async Task Toggle(string subtaskId, bool value)
    {
        if (_busy || _refreshing) return;
        _busy = true;
        _rows.IsEnabled = false;
        try
        {
            if (_recurring) await _store.SetOccurrenceSubtaskCompletedAsync(_id, _date!.Value, subtaskId, value);
            else await _store.SetSubtaskCompletedAsync(_id, subtaskId, value);
        }
        catch (Exception ex) { await Ui.Error(this, ex); }
        finally { RefreshProgress(); _busy = false; _rows.IsEnabled = true; }
    }

    private Task Close() => _busy ? Task.CompletedTask : ModalNavigation.CloseAsync(this);

    private Task Edit() => _busy ? Task.CompletedTask : _recurring
        ? ModalNavigation.PushAsync(this, () => new RecurringEditorPage(_store, _store.Data.RecurringTasks.First(task => task.Id == _id)))
        : ModalNavigation.PushAsync(this, () => new TaskEditorPage(_store, _store.Data.Tasks.First(task => task.Id == _id)));

    protected override bool OnBackButtonPressed() => _busy || ModalNavigation.HandleBack(this);
}
