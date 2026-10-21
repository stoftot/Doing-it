using System.Globalization;
using System.Text.RegularExpressions;

namespace DoingIt.Core;

public static class TaskText
{
    public static string CleanTitle(string? value)
    {
        var title = value?.Trim().Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Task title is required.");
        if (title.Count(c => c == '\n') > 1)
            throw new ArgumentException("Task title must fit on two lines. Put additional details in the description.");
        return title;
    }

    public static string? CleanDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // The presentation supplies the actual font/width measurement. Headless callers
    // can use the structural two-line rule without depending on a graphics runtime.
    public static bool FitsLines(string value) => value.Count(c => c == '\n') <= 1;

    public static (string Title, string? Description) FromLegacy(string value, Func<string, bool> fits)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 250 || value != value.Trim())
            throw new ArgumentException("The saved task text is invalid.");
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        if (FitsLines(normalized) && fits(normalized)) return (normalized, null);

        // Keep the full original text, including paragraph breaks, in the description.
        // Only the compact title collapses whitespace between words.
        var words = Regex.Split(value, @"\s+");
        var title = "";
        foreach (var word in words)
        {
            var candidate = title.Length == 0 ? word : title + " " + word;
            if (!fits(candidate)) break;
            title = candidate;
        }
        if (title.Length == 0)
        {
            // A single oversized word has no fitting whole-word prefix. Keep the
            // longest complete text-element prefix instead; never split an emoji.
            var elements = StringInfo.GetTextElementEnumerator(words[0]);
            while (elements.MoveNext())
            {
                var candidate = title + elements.GetTextElement();
                if (!fits(candidate)) break;
                title = candidate;
            }
        }
        if (title.Length == 0) throw new ArgumentException("There is no room to display a task title.");
        return (title, value);
    }
}
