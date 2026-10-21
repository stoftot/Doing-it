using Android.Graphics;
using Android.Text;
using Android.Util;

namespace DoingIt;

internal static class TaskTitleLayout
{
    public const double FontSize = 15;
    private static readonly Lazy<Typeface> Font = new(() =>
        Typeface.CreateFromAsset(Android.App.Application.Context.Assets, "OpenSans-Regular.ttf")!);

    // Match the narrowest task row: page (44), card (40), check (44),
    // edit (48), two gaps (8). Use the portrait width even in landscape.
    public static double Width => Math.Max(40,
        Math.Min(Resources.Configuration!.ScreenWidthDp, Resources.Configuration.SmallestScreenWidthDp) - 184);

    private static Android.Content.Res.Resources Resources => Android.App.Application.Context.Resources!;

    public static bool Fits(string title)
    {
        using var paint = new TextPaint(PaintFlags.AntiAlias);
        paint.SetTypeface(Font.Value);
        paint.TextSize = TypedValue.ApplyDimension(ComplexUnitType.Sp, (float)FontSize, Resources.DisplayMetrics);
        using var text = new Java.Lang.String(title);
        using var builder = StaticLayout.Builder.Obtain(text, 0, text.Length(), paint,
            Math.Max(1, (int)Math.Floor(Width * Resources.DisplayMetrics!.Density)))!;
        using var layout = builder.SetBreakStrategy(BreakStrategy.Simple)!
            .SetHyphenationFrequency(HyphenationFrequency.None)!.Build()!;
        return layout.LineCount <= 2;
    }
}
