namespace Fanote.Core;

public static class NoteTitleHelper
{
    public const string PlaceholderTitle = "Nueva nota";

    public static string GetTitle(string text)
    {
        var firstLine = text.Split('\n')[0].TrimEnd('\r').Trim();
        return string.IsNullOrEmpty(firstLine) ? PlaceholderTitle : firstLine;
    }
}
