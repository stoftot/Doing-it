using Microsoft.Maui.Controls.Shapes;

namespace DoingIt;

internal static class Ui
{
    public static readonly Color Teal = Color.FromArgb("#1D4C5E");
    public static readonly Color Blush = Color.FromArgb("#D5BBB1");
    public static readonly Color Paper = Color.FromArgb("#F7F5F1");
    public static readonly Color Ink = Color.FromArgb("#203D47");
    public static readonly Color Muted = Color.FromArgb("#6B7B80");
    public static readonly Color Line = Color.FromArgb("#E6E8E5");
    public static readonly Color Pale = Color.FromArgb("#EAF0EF");
    public static readonly Color Danger = Color.FromArgb("#9A4141");

    public static Label Text(string text, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color ?? Ink,
        FontFamily = bold ? "OpenSansSemibold" : "OpenSansRegular",
        VerticalOptions = LayoutOptions.Center
    };

    public static Label Eyebrow(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        CharacterSpacing = 2,
        FontFamily = "OpenSansSemibold",
        TextColor = Muted
    };

    public static Border Card(View content, Color? color = null, int radius = 24, Thickness? padding = null) => new()
    {
        Content = content,
        BackgroundColor = color ?? Colors.White,
        Stroke = Colors.Transparent,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = radius },
        Padding = padding ?? new Thickness(20)
    };

    public static Button Button(string text, Func<Task> action, bool primary = true, string? id = null)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = primary ? Teal : Pale,
            TextColor = primary ? Colors.White : Teal,
            CornerRadius = 16,
            FontSize = 14,
            FontFamily = "OpenSansSemibold",
            MinimumHeightRequest = 48,
            Padding = new Thickness(16, 10),
            BorderWidth = 0,
            AutomationId = id
        };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    public static Button Quiet(string text, Func<Task> action, string? id = null)
    {
        var button = Button(text, action, false, id);
        button.BackgroundColor = Colors.Transparent;
        button.Padding = new Thickness(10, 6);
        button.FontSize = 12;
        button.MinimumWidthRequest = 44;
        button.MinimumHeightRequest = 44;
        return button;
    }

    public static ImageButton Icon(string image, string description, Func<Task> action, bool filled = false)
    {
        var button = new ImageButton
        {
            Source = image + ".png",
            BackgroundColor = filled ? Pale : Colors.Transparent,
            HeightRequest = 48,
            WidthRequest = 48,
            CornerRadius = 16,
            Padding = 13,
            AutomationId = description.Replace(" ", "")
        };
        SemanticProperties.SetDescription(button, description);
        button.Clicked += async (_, _) => await action();
        return button;
    }

    public static View Rule() => new BoxView { HeightRequest = 1, Color = Line, Margin = new Thickness(0, 4) };

    public static Grid Between(View left, View right)
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        grid.Add(left);
        grid.Add(right, 1);
        return grid;
    }

    public static VerticalStackLayout Stack(params View[] views)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        foreach (var view in views) stack.Add(view);
        return stack;
    }

    public static View Empty(string title, string subtitle, string icon = "check")
    {
        var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 14, Padding = new Thickness(0, 12) };
        row.Add(Card(new Image { Source = icon + ".png", WidthRequest = 22, HeightRequest = 22 }, Pale, 16, new Thickness(12)));
        var copy = Stack(Text(title, 15, bold: true), Text(subtitle, 12, Muted));
        copy.Spacing = 4;
        row.Add(copy, 1);
        return row;
    }

    public static void ConfigurePage(ContentPage page)
    {
        page.BackgroundColor = Paper;
        page.SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(page, false);
    }

    public static View PageHeading(string title, string subtitle, View? trailing = null)
    {
        var titles = Stack(Text(title, 30, bold: true), Text(subtitle, 13, Muted));
        titles.Spacing = 4;
        return trailing is null ? titles : Between(titles, trailing);
    }

    public static View ModalHeader(string title, Func<Task> close) => Between(Text(title, 23, bold: true), Icon("close", "Close", close));

    public static View Field(string label, View input, string? hint = null)
    {
        var stack = Stack(Eyebrow(label), input);
        if (hint is not null) stack.Add(Text(hint, 12, Muted));
        return stack;
    }

    public static Editor Description(string text = "") => new()
    {
        Text = text,
        Placeholder = "Add details (optional)",
        PlaceholderColor = Muted,
        TextColor = Ink,
        BackgroundColor = Colors.Transparent,
        FontFamily = "OpenSansRegular",
        FontSize = 17,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 106,
        AutomationId = "TaskDescription"
    };

    public static Editor TitleEditor(string text = "") => new()
    {
        Text = text,
        Placeholder = "What would you like to do?",
        PlaceholderColor = Muted,
        TextColor = Ink,
        BackgroundColor = Colors.Transparent,
        FontFamily = "OpenSansRegular",
        FontSize = TaskTitleLayout.FontSize,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 64,
        AutomationId = "TaskTitle"
    };

    public static View TitleField(Editor editor)
    {
        var hint = Text("Required · Up to two lines in a task row", 12, Muted);
        void RefreshHint()
        {
            var fits = TaskTitleLayout.Fits(editor.Text?.Trim() ?? "");
            hint.Text = fits ? "Required · Up to two lines in a task row" : "Title is too long. Move extra details to the description.";
            hint.TextColor = fits ? Muted : Danger;
        }
        editor.TextChanged += (_, _) => RefreshHint();
        RefreshHint();
        return Stack(Eyebrow("TASK TITLE"), Card(editor), hint);
    }

    public static Label TaskTitle(string title, Func<Task> open, Color? color = null)
    {
        var label = Text(title, TaskTitleLayout.FontSize, color);
        label.MaxLines = 2;
        label.LineBreakMode = LineBreakMode.TailTruncation;
        label.MinimumHeightRequest = 44;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await open();
        label.GestureRecognizers.Add(tap);
        SemanticProperties.SetHint(label, "Open task details");
        return label;
    }

    public static async Task Error(Page page, Exception exception)
    {
        var message = exception is ArgumentException or InvalidOperationException
            ? exception.Message : "Your changes couldn't be saved. Your existing tasks have been kept. Please try again.";
        System.Diagnostics.Debug.WriteLine(exception);
        await page.DisplayAlertAsync("Couldn't save", message, "OK");
    }
}
