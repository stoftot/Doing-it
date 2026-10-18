using DoingIt.Core;

namespace DoingIt.Pages;

internal sealed class SubtaskEditor : VerticalStackLayout
{
    private readonly List<(Subtask Item, Entry Input, View Row)> _items = [];
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    private readonly View _fields;
    private readonly Button _normal;
    private readonly Button _checklist;

    public bool IsChecklist { get; private set; }
    public Subtask[] Subtasks => IsChecklist
        ? _items.Select(item => item.Item with { Title = item.Input.Text ?? "" }).ToArray() : [];

    public SubtaskEditor(bool isChecklist, Subtask[] subtasks, bool existing)
    {
        Spacing = 16;
        IsChecklist = isChecklist;
        _normal = Ui.Button("Normal task", () => SetKind(false), false, "NormalTaskType");
        _checklist = Ui.Button("To-do list", () => SetKind(true), false, "ChecklistTaskType");
        var types = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        types.Add(_normal);
        types.Add(_checklist, 1);
        if (!existing) Add(Ui.Field("TASK TYPE", types));
        foreach (var subtask in subtasks) AddRow(subtask);
        _fields = Ui.Field("SUBTASKS", Ui.Stack(_rows, Ui.Quiet("+  Add subtask", () =>
        {
            var entry = AddRow(new Subtask());
            entry.Focus();
            return Task.CompletedTask;
        }, "AddSubtask")), "Add at least one item. The task is done when every item is checked.");
        Add(_fields);
        Render();
    }

    private Task SetKind(bool value)
    {
        IsChecklist = value;
        if (value && _items.Count == 0) AddRow(new Subtask());
        Render();
        return Task.CompletedTask;
    }

    private void Render()
    {
        _fields.IsVisible = IsChecklist;
        _normal.BackgroundColor = !IsChecklist ? Ui.Teal : Ui.Pale;
        _normal.TextColor = !IsChecklist ? Colors.White : Ui.Teal;
        _checklist.BackgroundColor = IsChecklist ? Ui.Teal : Ui.Pale;
        _checklist.TextColor = IsChecklist ? Colors.White : Ui.Teal;
    }

    private Entry AddRow(Subtask item)
    {
        var input = new Entry
        {
            Text = item.Title,
            Placeholder = "What needs doing?",
            MaxLength = 250,
            TextColor = Ui.Ink,
            PlaceholderColor = Ui.Muted,
            FontSize = 15,
            FontFamily = "OpenSansRegular",
            AutomationId = "SubtaskTitle" + item.Id
        };
        SemanticProperties.SetDescription(input, "Subtask title");
        View? row = null;
        var remove = Ui.Icon("close", "Remove subtask " + item.Id, () =>
        {
            _items.RemoveAll(value => value.Row == row);
            _rows.Remove(row!);
            return Task.CompletedTask;
        });
        SemanticProperties.SetDescription(remove, "Remove subtask");
        row = Ui.Between(input, remove);
        _items.Add((item, input, row));
        _rows.Add(row);
        return input;
    }
}
