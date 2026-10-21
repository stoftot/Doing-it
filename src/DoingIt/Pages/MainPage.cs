using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class MainPage : ContentPage
{
    private readonly PlannerStore _store;
    private readonly Grid _root = new() { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
    private readonly ScrollView _scroll = new();
    private readonly ContentView _navigation = new();
    private readonly IDispatcherTimer _dayTimer;
    private Grid? _popup;
    private TaskDetailsOverlay? _details;
    private int _tab;
    private bool _loaded;
    private bool _loading;
    private bool _busy;
    private bool? _widgetNavigation;
    private bool _openingWidget;
    private TaskEditorPage? _widgetEditor;
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public MainPage(PlannerStore store)
    {
        _store = store;
        Ui.ConfigurePage(this);
        _root.Add(_scroll);
        _root.Add(_navigation, 0, 1);
        Content = _root;
        _dayTimer = Dispatcher.CreateTimer();
        _dayTimer.Interval = TimeSpan.FromSeconds(30);
        _dayTimer.Tick += (_, _) => RefreshCurrentDay();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading) return;
        if (!_loaded) await Load();
        else Render();
        _dayTimer.Start();
    }

    protected override void OnDisappearing()
    {
        _dayTimer.Stop();
        base.OnDisappearing();
    }

    private async Task Load()
    {
        _loading = true;
        _scroll.Content = Ui.Stack(Ui.Text("Doing it", 32, bold: true), Ui.Text("Getting your day ready…"));
        try
        {
            await _store.LoadAsync();
            _loaded = true;
            Render();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            _navigation.Content = null;
            var content = Ui.Stack(Ui.Text("Your tasks are safe", 26, bold: true),
                Ui.Text("We couldn't open the saved planner. Nothing has been overwritten. Try again, or keep the app data for recovery.", 15),
                Ui.Button("Try again", Load));
            content.Padding = 24;
            _scroll.Content = content;
        }
        finally { _loading = false; }
        await ProcessWidgetNavigation();
    }

    public void OpenFromWidget(bool createTask)
    {
        _widgetNavigation = createTask;
        _ = ProcessWidgetNavigation();
    }

    private async Task ProcessWidgetNavigation()
    {
        // Queue through startup and failed load/retry. An unreadable planner
        // must never open an editor against an empty snapshot.
        if (!_loaded || _loading || _openingWidget) return;
        _openingWidget = true;
        try
        {
            while (_widgetNavigation is bool createTask)
            {
                _widgetNavigation = null;
                if (createTask && _widgetEditor is not null && Navigation.ModalStack.LastOrDefault() == _widgetEditor)
                    continue;
                while (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync(false);
                _tab = 0;
                ClosePopup();
                Render();
                await _scroll.ScrollToAsync(0, 0, false);
                if (createTask)
                {
                    _widgetEditor = new TaskEditorPage(_store, date: DateOnly.FromDateTime(DateTime.Today));
                    await Navigation.PushModalAsync(_widgetEditor);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlertAsync("Couldn't open the shortcut", "Please try the widget again.", "OK");
        }
        finally { _openingWidget = false; }
    }

    public void RefreshCurrentDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today == _today || !_loaded) return;
        _today = today;
        ClosePopup();
        Render();
        AndroidNotifications.DailyNotifications.Refresh();
    }

    private void Render()
    {
        if (!_loaded) return;
        _today = DateOnly.FromDateTime(DateTime.Today);
        var content = new VerticalStackLayout { Spacing = 22, Padding = new Thickness(22, 20, 22, 24) };
        if (_tab == 0) RenderHome(content);
        else if (_tab == 1) RenderRecurring(content);
        else RenderLists(content);
        _scroll.Content = content;
        RenderNavigation();
    }

    private void RenderHome(VerticalStackLayout content)
    {
        var shortcuts = new HorizontalStackLayout { Spacing = 4 };
        shortcuts.Add(Ui.Icon("bell", "Notification settings", () => Navigation.PushModalAsync(new NotificationSettingsPage()), true));
        shortcuts.Add(Ui.Icon("calendar", "Open calendar", OpenCalendar, true));
        content.Add(Ui.Between(Ui.Stack(Ui.Eyebrow(_today.ToString("dddd, d MMMM")), Ui.Text("Doing it.", 34, bold: true)), shortcuts));
        var due = PlannerQueries.ForDate(_store.Data, _today);
        var remaining = due.Count(t => !t.IsCompleted);
        var hero = Ui.Stack(Ui.Eyebrow("ONE THING AT A TIME"), Ui.Text("A little progress,\nevery day.", 26, Ui.Teal, true),
            Ui.Between(Ui.Text(remaining == 0 ? "Make space for what matters." : $"{remaining} task{(remaining == 1 ? "" : "s")} on your mind today.", 12, Ui.Teal),
                Ui.Icon("plus", "Add today's task", () => OpenTask(date: _today))));
        hero.Spacing = 8;
        content.Add(Ui.Card(hero, Ui.Blush, 28));

        var todayCard = Ui.Stack(SectionHeader("Today's tasks", () => PlannerQueries.ForDate(_store.Data, _today), _today));
        var incomplete = due.Where(t => !t.IsCompleted).ToArray();
        if (incomplete.Length == 0)
            todayCard.Add(Ui.Empty(due.Count == 0 ? "A fresh start" : "All done for today", due.Count == 0 ? "Add a task or set a little routine." : "Take a breath. You've earned it."));
        foreach (var task in incomplete) todayCard.Add(TaskRow(task, _today));
        todayCard.Add(Ui.Quiet("+  Add a task for today", () => OpenTask(date: _today), "AddTodayTask"));
        content.Add(Ui.Card(todayCard));
        content.Add(Ui.Between(Ui.Eyebrow("YOUR LISTS"), Ui.Quiet("+ New list", CreateList, "CreateListHome")));
        foreach (var list in _store.Data.Lists) content.Add(ListCard(list, false));
    }

    private void RenderRecurring(VerticalStackLayout content)
    {
        content.Add(Ui.PageHeading("Little routines.", "A rhythm for the things you do again."));
        var active = _store.Data.RecurringTasks.Count(t => t.IsEnabled);
        content.Add(Ui.Card(Ui.Between(Ui.Stack(Ui.Eyebrow("RECURRING TASKS"), Ui.Text($"{active} active routine{(active == 1 ? "" : "s")}", 21, Ui.Teal, true)),
            new Image { Source = "repeat.png", HeightRequest = 36, WidthRequest = 36 }), Ui.Blush));
        content.Add(Ui.Button("+  Create recurring task", () => OpenRecurring(), id: "CreateRecurringTask"));
        if (_store.Data.RecurringTasks.Length == 0)
            content.Add(Ui.Card(Ui.Empty("Build your first routine", "Daily, weekly or on selected dates each month.", "repeat")));
        foreach (var task in _store.Data.RecurringTasks)
        {
            var enabled = new Switch { IsToggled = task.IsEnabled, OnColor = Ui.Teal, ThumbColor = task.IsEnabled ? Ui.Teal : Ui.Muted };
            SemanticProperties.SetDescription(enabled, $"Enable {task.Title}");
            enabled.Toggled += async (_, args) => await Change(() => _store.SetRecurringEnabledAsync(task.Id, args.Value));
            var details = Ui.Stack(Ui.TaskTitle(task.Title, () => ShowDetails(task.Id, true)), Ui.Text(PlannerQueries.ScheduleLabel(task), 12, Ui.Muted));
            details.Spacing = 5;
            if (task.IsChecklist)
            {
                var occurrence = PlannerQueries.ForDate(_store.Data, _today).FirstOrDefault(item => item.IsRecurring && item.Id == task.Id);
                details.Add(Ui.Quiet(occurrence is null ? $"To-do list · {task.Subtasks.Length} subtasks" : occurrence.ProgressLabel + " today",
                    () => Navigation.PushModalAsync(new ChecklistPage(_store, task.Id, true, occurrence is null ? null : _today))));
            }
            var card = Ui.Stack(Ui.Between(details, enabled), Ui.Rule(),
                Ui.Between(Ui.Text(task.IsEnabled ? "●  Active" : "○  Paused", 12, task.IsEnabled ? Ui.Teal : Ui.Muted),
                    Ui.Quiet("Edit routine", () => OpenRecurring(task), "EditRecurring" + task.Id)));
            content.Add(Ui.Card(card));
        }
        content.Add(Ui.Text("Pause a routine whenever you need a break. It will stay here until you're ready.", 12, Ui.Muted));
    }

    private void RenderLists(VerticalStackLayout content)
    {
        content.Add(Ui.PageHeading("Room for it all.", "Keep the everyday things together.", Ui.Icon("calendar", "Open calendar", OpenCalendar, true)));
        var actions = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 10 };
        actions.Add(Ui.Button("+  New task", () => OpenTask(), id: "CreateTask"));
        actions.Add(Ui.Button("+  New list", CreateList, false, "CreateList"), 1);
        content.Add(actions);
        content.Add(Ui.Eyebrow($"{_store.Data.Lists.Length} LIST{(_store.Data.Lists.Length == 1 ? "" : "S")} · ALWAYS HERE FOR YOU"));
        foreach (var list in _store.Data.Lists) content.Add(ListCard(list, true));
        content.Add(Ui.Button("View upcoming tasks", OpenCalendar, false, "UpcomingTasks"));
    }

    private View ListCard(TaskList list, bool manage)
    {
        IReadOnlyList<DueTask> Items() => _store.Data.Tasks.Where(t => t.ListId == list.Id)
            .Select(PlannerQueries.FromTask).ToArray();
        var items = Items();
        var header = SectionHeader(list.Name, Items);
        var stack = Ui.Stack(header);
        var incomplete = items.Where(t => !t.IsCompleted).ToArray();
        if (incomplete.Length == 0)
            stack.Add(Ui.Empty("A little room to plan", "Tasks in this list stay until you finish them.", "list"));
        foreach (var task in incomplete) stack.Add(TaskRow(task));
        var add = Ui.Quiet("+  Add task", () => OpenTask(listId: list.Id), "AddTask" + list.Id);
        stack.Add(manage && list.Id != PlannerStore.GeneralListId
            ? Ui.Between(add, Ui.Quiet("Manage", () => ManageList(list))) : add);
        return Ui.Card(stack);
    }

    private View SectionHeader(string name, Func<IReadOnlyList<DueTask>> items, DateOnly? date = null)
    {
        var count = items().Count(t => t.IsCompleted);
        var done = Ui.Quiet($"✓  {count} done", () =>
        {
            ShowCompleted(name, items, date);
            return Task.CompletedTask;
        }, "Completed" + name.Replace(" ", ""));
        SemanticProperties.SetDescription(done, $"View completed tasks in {name}, {count} completed");
        return Ui.Between(Ui.Text(name, 19, bold: true), done);
    }

    private View TaskRow(DueTask task, DateOnly? date = null)
    {
        if (task.IsChecklist) return ChecklistRow(task, () => OpenChecklist(task, date), () => ShowDetails(task.Id, task.IsRecurring));
        var check = new CheckBox { Color = Ui.Teal, IsChecked = false, WidthRequest = 44, HeightRequest = 48 };
        SemanticProperties.SetDescription(check, $"Complete {task.Title}");
        check.CheckedChanged += async (_, args) =>
        {
            if (args.Value) await Change(() => Complete(task, date, true));
        };
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 3 };
        grid.Add(check);
        var text = Ui.Stack(Ui.TaskTitle(task.Title, () => ShowDetails(task.Id, task.IsRecurring)));
        text.VerticalOptions = LayoutOptions.Center;
        text.Spacing = 2;
        if (task.IsRecurring) text.Add(Ui.Text("Recurring", 10, Ui.Muted));
        grid.Add(text, 1);
        grid.Add(Ui.Icon("edit", "Edit " + task.Title, () => Edit(task)), 2);
        return grid;
    }

    internal static View ChecklistRow(DueTask task, Func<Task> open, Func<Task> details)
    {
        var progress = Ui.Quiet(task.ProgressLabel + "  ›", open);
        progress.HorizontalOptions = LayoutOptions.Start;
        progress.BackgroundColor = Ui.Pale;
        SemanticProperties.SetDescription(progress, $"Open {task.Title}, {task.ProgressLabel}");
        var text = Ui.Stack(Ui.TaskTitle(task.Title, details), progress);
        text.Spacing = 3;
        if (task.IsRecurring) text.Add(Ui.Text("Recurring", 10, Ui.Muted));
        var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8, Padding = new Thickness(0, 8) };
        row.Add(Ui.Icon("list", "Open " + task.Title, open));
        row.Add(text, 1);
        return row;
    }

    private Task ShowDetails(string id, bool recurring)
    {
        if (_details is not null) return Task.CompletedTask;
        _scroll.IsEnabled = false;
        _navigation.IsEnabled = false;
        if (_popup is not null) _popup.IsEnabled = false;
        _details = new TaskDetailsOverlay(_store.Data, id, recurring, CloseDetails);
        _root.Add(_details);
        Grid.SetRowSpan(_details, 2);
        return Task.CompletedTask;
    }

    private void CloseDetails()
    {
        if (_details is null) return;
        _root.Remove(_details);
        _details = null;
        _scroll.IsEnabled = _popup is null;
        _navigation.IsEnabled = _popup is null;
        if (_popup is not null) _popup.IsEnabled = true;
    }

    private Task OpenChecklist(DueTask task, DateOnly? date)
    {
        ClosePopup();
        return Navigation.PushModalAsync(new ChecklistPage(_store, task.Id, task.IsRecurring, task.IsRecurring ? date ?? _today : null));
    }

    private Task Complete(DueTask task, DateOnly? date, bool value) => task.IsRecurring
        ? _store.SetOccurrenceCompletedAsync(task.Id, date ?? _today, value)
        : _store.SetTaskCompletedAsync(task.Id, value);

    private Task Edit(DueTask task) => task.IsRecurring
        ? OpenRecurring(_store.Data.RecurringTasks.First(t => t.Id == task.Id))
        : OpenTask(_store.Data.Tasks.First(t => t.Id == task.Id));

    private async Task Change(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try { await action(); }
        catch (Exception ex) { await Ui.Error(this, ex); }
        finally { _busy = false; Render(); }
    }

    private void ShowCompleted(string name, Func<IReadOnlyList<DueTask>> items, DateOnly? date)
    {
        ClosePopup();
        _scroll.IsEnabled = false;
        _navigation.IsEnabled = false;
        _popup = new Grid { BackgroundColor = Color.FromArgb("#88203D47"), Padding = 24 };
        var rows = Ui.Stack();
        var completed = items().Where(t => t.IsCompleted).ToArray();
        foreach (var task in completed)
            rows.Add(task.IsChecklist ? ChecklistRow(task, () => OpenChecklist(task, date), () => ShowDetails(task.Id, task.IsRecurring)) : Ui.Between(Ui.TaskTitle(task.Title, () => ShowDetails(task.Id, task.IsRecurring), Ui.Muted), Ui.Quiet("Undo", async () =>
            {
                await Change(() => Complete(task, date, false));
                ShowCompleted(name, items, date);
            })));
        if (completed.Length == 0) rows.Add(Ui.Empty("Nothing completed yet", "The things you finish will appear here."));
        var body = Ui.Stack(Ui.Between(Ui.Text("Completed", 24, bold: true), Ui.Icon("close", "Close completed tasks", () =>
        {
            ClosePopup();
            return Task.CompletedTask;
        })), Ui.Text(name, 13, Ui.Muted), Ui.Rule(), new ScrollView { Content = rows, MaximumHeightRequest = 360 });
        var card = Ui.Card(body);
        card.VerticalOptions = LayoutOptions.Center;
        _popup.Add(card);
        _root.Add(_popup);
        Grid.SetRowSpan(_popup, 2);
    }

    private void ClosePopup()
    {
        CloseDetails();
        if (_popup is null) return;
        _root.Remove(_popup);
        _popup = null;
        _scroll.IsEnabled = true;
        _navigation.IsEnabled = true;
    }

    private void RenderNavigation()
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 6 };
        var names = new[] { "Home", "Recurring", "Lists" };
        var icons = new[] { "home", "repeat", "list" };
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var selected = i == _tab;
            var label = Ui.Text(names[i], 11, selected ? Colors.White : Ui.Muted, true);
            label.HorizontalOptions = LayoutOptions.Center;
            var stack = Ui.Stack(new Image { Source = icons[i] + (selected ? "_light" : "") + ".png", HeightRequest = 21, WidthRequest = 21 }, label);
            stack.Spacing = 4;
            var item = Ui.Card(stack, selected ? Ui.Teal : Colors.Transparent, 18, new Thickness(8, 10));
            item.AutomationId = "Tab" + names[i];
            SemanticProperties.SetDescription(item, names[i] + (selected ? ", selected" : " tab"));
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { _tab = index; ClosePopup(); Render(); _ = _scroll.ScrollToAsync(0, 0, false); };
            item.GestureRecognizers.Add(tap);
            grid.Add(item, i);
        }
        var nav = Ui.Card(grid, Colors.White, 24, new Thickness(8));
        nav.Margin = new Thickness(16, 4, 16, 12);
        _navigation.Content = nav;
    }

    private Task OpenTask(TaskItem? item = null, string? listId = null, DateOnly? date = null) => Navigation.PushModalAsync(new TaskEditorPage(_store, item, listId, date));
    private Task OpenRecurring(RecurringTask? item = null) => Navigation.PushModalAsync(new RecurringEditorPage(_store, item));
    private Task OpenCalendar() => Navigation.PushModalAsync(new CalendarPage(_store));

    private async Task CreateList()
    {
        var name = await DisplayPromptAsync("A new little list", "Give your list a name.", "Create", "Cancel", "e.g. Around the house", 40);
        if (name is not null) await Change(async () => { await _store.AddListAsync(name); });
    }

    private async Task ManageList(TaskList list)
    {
        var action = await DisplayActionSheetAsync(list.Name, "Cancel", "Delete list", "Rename list");
        if (action == "Rename list")
        {
            var name = await DisplayPromptAsync("Rename list", "Choose a name.", "Save", "Cancel", maxLength: 40, initialValue: list.Name);
            if (name is not null) await Change(() => _store.RenameListAsync(list.Id, name));
        }
        else if (action == "Delete list" && await DisplayAlertAsync("Delete this list?", "Its tasks will move to General, including completed tasks.", "Delete list", "Keep list"))
            await Change(() => _store.DeleteListAsync(list.Id));
    }

    protected override bool OnBackButtonPressed()
    {
        if (_details is not null) { CloseDetails(); return true; }
        if (_popup is not null) { ClosePopup(); return true; }
        if (_tab != 0) { _tab = 0; Render(); return true; }
        return base.OnBackButtonPressed();
    }
}
