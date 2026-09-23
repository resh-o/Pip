namespace DeskPet.Core.Brain;

/// <summary>Case-insensitive matching helpers shared by the privacy filter and the offline rules.</summary>
internal static class TextMatch
{
    public static string StripExe(string processName)
    {
        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    public static bool SameProcess(string processName, string configured) =>
        string.Equals(StripExe(processName), StripExe(configured), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="word"/> occurs in <paramref name="text"/> not glued to other letters or digits,
    /// so "Code" matches "VS Code" but not "Barcode".
    /// </summary>
    public static bool ContainsWord(string text, string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        var start = 0;
        while ((start = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (!IsWordChar(text, start - 1) && !IsWordChar(text, start + word.Length))
            {
                return true;
            }

            start++;
        }

        return false;
    }

    private static bool IsWordChar(string text, int index) =>
        index >= 0 && index < text.Length && char.IsLetterOrDigit(text[index]);
}
