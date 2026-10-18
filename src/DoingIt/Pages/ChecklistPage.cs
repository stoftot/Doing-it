using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class ChecklistPage : ContentPage
{
    private readonly PlannerStore _store;
    private readonly string _id;
    private readonly bool _recurring;
    private readonly DateOnly? _date;
    private readonly Grid _root = new() { Padding = new Thickness(24), BackgroundColor = Color.FromArgb("#88203D47") };
    private bool _busy;

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
        var done = subtasks.Count(Done);
        var canCheck = !_recurring || _date.HasValue && PlannerQueries.ForDate(_store.Data, _date.Value).Any(item => item.Id == _id && item.IsRecurring);
        var rows = Ui.Stack();
        foreach (var subtask in subtasks)
        {
            var check = new CheckBox { IsChecked = Done(subtask), Color = Ui.Teal, WidthRequest = 48, HeightRequest = 48, IsEnabled = canCheck };
            SemanticProperties.SetDescription(check, (Done(subtask) ? "Reopen " : "Complete ") + subtask.Title);
            check.AutomationId = "CheckSubtask" + subtask.Id;
            check.CheckedChanged += async (_, args) => await Toggle(subtask.Id, args.Value);
            var label = Ui.Text(subtask.Title, 16, Done(subtask) ? Ui.Muted : Ui.Ink);
            if (Done(subtask)) label.TextDecorations = TextDecorations.Strikethrough;
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8 };
            row.Add(check);
            row.Add(label, 1);
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => { if (canCheck) await Toggle(subtask.Id, !Done(subtask)); };
            label.GestureRecognizers.Add(tap);
            rows.Add(row);
        }
        var progress = Ui.Text($"{done} of {subtasks.Length} done", 15, Ui.Teal, true);
        progress.AutomationId = "ChecklistProgress";
        var header = Ui.Stack(Ui.ModalHeader(title, Close), progress);
        if (_recurring) header.Add(Ui.Text(_date.HasValue ? _date.Value.ToString("dddd, d MMM yyyy") : "Routine template · Each occurrence starts fresh", 12, Ui.Muted));
        if (!canCheck) header.Add(Ui.Text("Edit the routine to change its items. Check items on a scheduled date in Home or the calendar.", 12, Ui.Muted));
        var body = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)],
            RowSpacing = 16,
            MaximumHeightRequest = 600
        };
        body.Add(header);
        body.Add(new ScrollView { Content = rows, MaximumHeightRequest = 360 }, 0, 1);
        body.Add(Ui.Between(Ui.Quiet("Edit " + (_recurring ? "routine" : "to-do list"), Edit, "EditChecklist"), Ui.Button("Done", Close, id: "CloseChecklist")), 0, 2);
        var card = Ui.Card(body);
        card.VerticalOptions = LayoutOptions.Center;
        _root.Add(card);
    }

    private async Task Toggle(string subtaskId, bool value)
    {
        if (_busy) return;
        _busy = true;
        _root.IsEnabled = false;
        try
        {
            if (_recurring) await _store.SetOccurrenceSubtaskCompletedAsync(_id, _date!.Value, subtaskId, value);
            else await _store.SetSubtaskCompletedAsync(_id, subtaskId, value);
        }
        catch (Exception ex) { await Ui.Error(this, ex); }
        finally { _busy = false; _root.IsEnabled = true; Render(); }
    }

    private Task Close() => _busy ? Task.CompletedTask : Navigation.PopModalAsync();

    private Task Edit() => _recurring
        ? Navigation.PushModalAsync(new RecurringEditorPage(_store, _store.Data.RecurringTasks.First(task => task.Id == _id)))
        : Navigation.PushModalAsync(new TaskEditorPage(_store, _store.Data.Tasks.First(task => task.Id == _id)));

    protected override bool OnBackButtonPressed() => _busy || base.OnBackButtonPressed();
}
