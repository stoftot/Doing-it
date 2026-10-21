using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class TaskEditorPage : EditorPage
{
    private readonly PlannerStore _store;
    private readonly TaskItem? _existing;
    private readonly Editor _title;
    private readonly Editor _description;
    private readonly SubtaskEditor _subtasks;
    private readonly Picker _lists;
    private readonly DatePicker _date;
    private readonly View _listDestination;
    private readonly View _dateDestination;
    private readonly Button _listMode;
    private readonly Button _dateMode;
    private bool _scheduled;

    public TaskEditorPage(PlannerStore store, TaskItem? existing = null, string? listId = null, DateOnly? date = null)
        : base(existing is null ? "A new task" : "Edit task", existing is null ? "Add task" : "Save changes")
    {
        _store = store;
        _existing = existing;
        _scheduled = existing is not null ? existing.ScheduledDate.HasValue : date.HasValue;
        _title = Ui.TitleEditor(existing?.Title ?? "");
        _description = Ui.Description(existing?.Description ?? "");
        _lists = new Picker
        {
            Title = "Choose a list",
            TitleColor = Ui.Muted,
            TextColor = Ui.Ink,
            ItemsSource = store.Data.Lists.ToList(),
            ItemDisplayBinding = new Binding(nameof(TaskList.Name)),
            SelectedItem = store.Data.Lists.FirstOrDefault(l => l.Id == (existing?.ListId ?? listId ?? PlannerStore.GeneralListId)),
            FontFamily = "OpenSansRegular",
            FontSize = 16,
            AutomationId = "TaskListPicker"
        };
        _date = new DatePicker
        {
            Date = (existing?.ScheduledDate ?? date ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue),
            MinimumDate = new DateTime(2000, 1, 1),
            MaximumDate = new DateTime(2100, 12, 31),
            Format = "ddd, d MMM yyyy",
            TextColor = Ui.Ink,
            FontSize = 16,
            AutomationId = "TaskDatePicker"
        };
        Body.Add(Ui.Text("One small thing, written down.", 14, Ui.Muted));
        Body.Add(Ui.TitleField(_title));
        _subtasks = new SubtaskEditor(existing?.IsChecklist ?? false, existing?.Subtasks ?? [], existing is not null);
        Body.Add(_subtasks);
        Body.Add(Ui.Field("DESCRIPTION (OPTIONAL)", Ui.Card(_description)));
        _listMode = Ui.Button("In a list", () => SetMode(false), false, "ChooseListMode");
        _dateMode = Ui.Button("On a date", () => SetMode(true), false, "ChooseDateMode");
        var modes = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        modes.Add(_listMode);
        modes.Add(_dateMode, 1);
        Body.Add(Ui.Field("WHERE DOES IT BELONG?", modes));
        _listDestination = Ui.Field("CHOOSE A LIST", Ui.Card(_lists), "This task stays in its list until you complete it. You can also find it on Home.");
        _dateDestination = Ui.Field("CHOOSE A DATE", Ui.Card(_date), "This task appears only on this date in Home and the calendar. It won't belong to a list.");
        Body.Add(_listDestination);
        Body.Add(_dateDestination);
        RenderDestination();
        if (existing is not null)
        {
            var delete = Ui.Quiet("Delete task", () => Delete(() => _store.DeleteTaskAsync(existing.Id), "This task will be permanently removed."));
            delete.TextColor = Ui.Danger;
            Body.Add(delete);
        }
    }

    private Task SetMode(bool scheduled)
    {
        _scheduled = scheduled;
        RenderDestination();
        return Task.CompletedTask;
    }

    private void RenderDestination()
    {
        _listMode.BackgroundColor = !_scheduled ? Ui.Teal : Ui.Pale;
        _listMode.TextColor = !_scheduled ? Colors.White : Ui.Teal;
        _dateMode.BackgroundColor = _scheduled ? Ui.Teal : Ui.Pale;
        _dateMode.TextColor = _scheduled ? Colors.White : Ui.Teal;
        _listDestination.IsVisible = !_scheduled;
        _dateDestination.IsVisible = _scheduled;
    }

    protected override Task Persist() => _store.SaveTaskAsync(new TaskItem
    {
        Id = _existing?.Id ?? Guid.NewGuid().ToString("N"),
        Title = _title.Text ?? "",
        Description = _description.Text,
        IsChecklist = _subtasks.IsChecklist,
        Subtasks = _subtasks.Subtasks,
        ListId = _scheduled ? null : (_lists.SelectedItem as TaskList)?.Id,
        ScheduledDate = _scheduled ? DateOnly.FromDateTime(_date.Date ?? DateTime.Today) : null,
        IsCompleted = _existing?.IsCompleted ?? false
    });
}
