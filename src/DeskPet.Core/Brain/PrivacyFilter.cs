using DeskPet.Core.Config;
using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>Decides whether a window's process name and title may leave the machine.</summary>
public static class PrivacyFilter
{
    public static bool IsSendable(Settings settings, WindowInfo window) =>
        IsSendable(settings, window.ProcessName, window.Title);

    public static bool IsSendable(Settings settings, string processName, string title) =>
        !settings.IgnoreProcesses.Any(p => TextMatch.SameProcess(processName, p))
        && !settings.IgnoreTitleKeywords.Any(k => IsNonBlankSubstring(title, k));

    // Substring rather than whole-word: erring towards privacy, "OnlineBanking" must still be withheld.
    private static bool IsNonBlankSubstring(string title, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) && title.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
