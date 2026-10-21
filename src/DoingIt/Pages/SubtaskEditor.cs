using DoingIt.Core;

namespace DoingIt.Pages;

internal sealed class SubtaskEditor : VerticalStackLayout
{
    private readonly List<(Subtask Item, Entry Input, View Row, Label Number)> _items = [];
    private readonly HashSet<string> _savedIds;
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    private readonly View _fields;
    private readonly Button _normal;
    private readonly Button _checklist;

    public bool IsChecklist { get; private set; }
    public Subtask[] Subtasks => IsChecklist
        ? _items.Where(item => _savedIds.Contains(item.Item.Id) || !string.IsNullOrWhiteSpace(item.Input.Text))
            .Select(item => item.Item with { Title = item.Input.Text ?? "" }).ToArray() : [];

    public SubtaskEditor(bool isChecklist, Subtask[] subtasks, bool existing)
    {
        Spacing = 16;
        _savedIds = subtasks.Select(item => item.Id).ToHashSet();
        IsChecklist = isChecklist;
        _normal = Ui.Button("Normal task", () => SetKind(false), false, "NormalTaskType");
        _checklist = Ui.Button("To-do list", () => SetKind(true), false, "ChecklistTaskType");
        var types = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        types.Add(_normal);
        types.Add(_checklist, 1);
        if (!existing) Add(Ui.Field("TASK TYPE", types));
        foreach (var subtask in subtasks) AddRow(subtask);
        var add = Ui.Button("+  Add item", AddItem, false, "AddSubtask");
        _fields = Ui.Field("ITEMS", Ui.Stack(_rows, add), "Use Next on the keyboard to keep adding. Check off every item to finish the task.");
        Add(_fields);
        Render();
    }

    private Task AddItem()
    {
        var empty = _items.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Input.Text)).Input;
        Focus(empty ?? AddRow(new Subtask()));
        return Task.CompletedTask;
    }

    private void Focus(Entry input) => Dispatcher.Dispatch(() => input.Focus());

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
            Placeholder = "Add an item",
            MaxLength = 250,
            TextColor = Ui.Ink,
            PlaceholderColor = Ui.Muted,
            FontSize = 15,
            FontFamily = "OpenSansRegular",
            MinimumHeightRequest = 48,
            ReturnType = ReturnType.Next,
            AutomationId = "SubtaskTitle" + item.Id
        };
        input.Completed += async (_, _) =>
        {
            var index = _items.FindIndex(value => value.Input == input);
            if (index < _items.Count - 1) Focus(_items[index + 1].Input);
            else await AddItem();
        };
        var number = Ui.Text("", 12, Ui.Muted, true);
        View? row = null;
        var remove = Ui.Icon("close", "Remove subtask " + item.Id, () =>
        {
            _items.RemoveAll(value => value.Row == row);
            _rows.Remove(row!);
            if (_items.Count == 0) AddRow(new Subtask());
            RefreshNumbers();
            return Task.CompletedTask;
        });
        void UpdateDescription() => SemanticProperties.SetDescription(remove, "Remove item " + (string.IsNullOrWhiteSpace(input.Text) ? number.Text : input.Text));
        input.TextChanged += (_, _) => UpdateDescription();
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        grid.Add(number);
        grid.Add(input, 1);
        grid.Add(remove, 2);
        row = Ui.Card(grid, radius: 16, padding: new Thickness(14, 4, 4, 4));
        _items.Add((item, input, row, number));
        _rows.Add(row);
        RefreshNumbers();
        UpdateDescription();
        return input;
    }

    private void RefreshNumbers()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index].Number.Text = (index + 1).ToString("00");
            SemanticProperties.SetDescription(_items[index].Input, $"Item {index + 1}");
        }
    }
}
