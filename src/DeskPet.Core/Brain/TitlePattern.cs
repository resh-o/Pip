using System.Text.RegularExpressions;

namespace DeskPet.Core.Brain;

/// <summary>
/// Normalises a window into a cache key so titles that differ only in volatile parts
/// (unread counters, numbers, spacing) share one verdict.
/// </summary>
public static partial class TitlePattern
{
    public const int MaxTitleLength = 80;

    // The app/site suffix after the last " - " identifies what the window is, so truncation eats the head instead.
    private const string SuffixSeparator = " - ";
    private const int MaxSuffixLength = 40;

    public static string Create(string processName, string title)
    {
        var process = TextMatch.StripExe(processName).ToLowerInvariant();
        return process + "|" + Truncate(Normalise(title));
    }

    private static string Normalise(string title)
    {
        var text = title.ToLowerInvariant();
        text = BracketedCounter().Replace(text, " ");
        text = Digits().Replace(text, "#");
        return Whitespace().Replace(text, " ").Trim();
    }

    private static string Truncate(string title)
    {
        if (title.Length <= MaxTitleLength)
        {
            return title;
        }

        var split = title.LastIndexOf(SuffixSeparator, StringComparison.Ordinal);
        var suffixLength = split < 0 ? 0 : title.Length - split;
        if (suffixLength == 0 || suffixLength > MaxSuffixLength)
        {
            return title[..MaxTitleLength];
        }

        return title[..(MaxTitleLength - suffixLength)] + title[split..];
    }

    [GeneratedRegex(@"[\(\[]\s*\d+\+?\s*[\)\]]")]
    private static partial Regex BracketedCounter();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
