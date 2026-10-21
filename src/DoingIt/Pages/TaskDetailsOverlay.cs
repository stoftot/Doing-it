using DoingIt.Core;

namespace DoingIt.Pages;

// An in-page overlay keeps the real page visible behind the read-only card.
internal sealed class TaskDetailsOverlay : Grid
{
    public TaskDetailsOverlay(PlannerData data, string id, bool recurring, Action close)
    {
        AutomationId = "TaskDetailsOverlay";
        var routine = recurring ? data.RecurringTasks.First(task => task.Id == id) : null;
        var task = recurring ? null : data.Tasks.First(task => task.Id == id);
        var title = routine?.Title ?? task!.Title;
        var description = routine?.Description ?? task?.Description;
        var location = routine is not null
            ? $"{PlannerQueries.ScheduleLabel(routine)} · From {routine.StartDate:d MMM yyyy}" + (routine.IsEnabled ? "" : " · Paused")
            : task!.ScheduledDate is DateOnly date ? date.ToString("dddd, d MMM yyyy")
            : "List: " + data.Lists.First(list => list.Id == task.ListId).Name;

        var backdrop = new BoxView { Color = Color.FromArgb("#88203D47"), AutomationId = "TaskDetailsBackdrop" };
        var dismiss = new TapGestureRecognizer();
        dismiss.Tapped += (_, _) => close();
        backdrop.GestureRecognizers.Add(dismiss);
        Add(backdrop);

        var heading = Ui.Text(title, TaskTitleLayout.FontSize);
        heading.MaxLines = 2;
        heading.LineBreakMode = LineBreakMode.TailTruncation;
        heading.AutomationId = "TaskDetailsTitle";
        var closeButton = Ui.Icon("close", "Close task details", () => { close(); return Task.CompletedTask; });
        var context = Ui.Text(location, 13, Ui.Teal);
        context.AutomationId = "TaskDetailsLocation";
        var copy = Ui.Stack(context);
        if (!string.IsNullOrWhiteSpace(description))
        {
            var text = Ui.Text(description, 16);
            text.AutomationId = "TaskDetailsDescription";
            copy.Add(Ui.Rule());
            copy.Add(text);
        }
        var scroll = new ScrollView { Content = copy, MaximumHeightRequest = 300 };
        var card = Ui.Card(Ui.Stack(Ui.Between(heading, closeButton), scroll));
        card.AutomationId = "TaskDetailsCard";
        card.Margin = 24;
        card.MaximumWidthRequest = 560;
        card.HorizontalOptions = LayoutOptions.Fill;
        card.VerticalOptions = LayoutOptions.Center;
        // A tap on blank card space is consumed without dismissing the popup.
        card.GestureRecognizers.Add(new TapGestureRecognizer());
        Add(card);
        SizeChanged += (_, _) =>
        {
            if (Height > 0) scroll.MaximumHeightRequest = Math.Max(40, Height * .75 - 120);
        };
    }
}
