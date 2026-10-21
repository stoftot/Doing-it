namespace DoingIt.Pages;

public abstract class EditorPage : ContentPage
{
    protected readonly VerticalStackLayout Body = new() { Spacing = 24, Padding = new Thickness(24, 8, 24, 28) };
    private readonly Button _save;
    private bool _saving;

    protected EditorPage(string title, string action)
    {
        Ui.ConfigurePage(this);
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        var header = Ui.ModalHeader(title, () => _saving ? Task.CompletedTask : ModalNavigation.CloseAsync(this));
        header.Margin = new Thickness(24, 12, 16, 12);
        root.Add(header);
        root.Add(new ScrollView { Content = Body }, 0, 1);
        _save = Ui.Button(action, Save, id: "SaveTask");
        _save.Margin = new Thickness(24, 12, 24, 18);
        root.Add(_save, 0, 2);
        Content = root;
    }

    protected abstract Task Persist();

    private Task Save() => CommitAndClose(async () => { await Persist(); return true; });

    protected Task Delete(Func<Task> delete, string explanation) => CommitAndClose(async () =>
    {
        if (!await DisplayAlertAsync("Delete this task?", explanation, "Delete", "Keep task")) return false;
        await delete();
        return true;
    });

    private async Task CommitAndClose(Func<Task<bool>> action)
    {
        if (_saving || ModalNavigation.IsBusy(this)) return;
        _saving = true;
        _save.IsEnabled = false;
        try
        {
            await ModalNavigation.CloseAsync(this, async () =>
            {
                try { return await action(); }
                catch (Exception ex) { await Ui.Error(this, ex); return false; }
            });
        }
        finally { _saving = false; _save.IsEnabled = true; }
    }

    protected override bool OnBackButtonPressed() => _saving || ModalNavigation.HandleBack(this);
}
