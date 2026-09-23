using DeskPet.Core.Config;
using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>
/// The single authority on which windows the pet may lay hands on. Built from an immutable
/// settings snapshot so the per-tick checks are plain set lookups.
/// </summary>
public sealed class SafetyPolicy
{
    private static readonly string[] ShellClasses =
        ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "#32770"];

    private static readonly string[] SystemProcesses =
        ["consent", "LockApp", "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost"];

    private readonly HashSet<string> _shellClasses = new(ShellClasses, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _titleKeywords;
    private readonly string _ownProcessName;
    private readonly HashSet<nint> _untouchable;

    public SafetyPolicy(Settings settings, string ownProcessName)
        : this(settings, ownProcessName, [])
    {
    }

    private SafetyPolicy(Settings settings, string ownProcessName, HashSet<nint> untouchable)
    {
        Settings = settings;
        _ownProcessName = ownProcessName;
        _untouchable = untouchable;
        AddProcesses(SystemProcesses);
        AddProcesses(settings.AllowList);
        AddProcesses(settings.IgnoreProcesses);
        AddProcesses([ownProcessName]);
        _titleKeywords = NonBlank(settings.IgnoreTitleKeywords);
    }

    /// <summary>The snapshot this policy was built from, so owners can detect a settings change by reference.</summary>
    public Settings Settings { get; }

    /// <summary>Rebuilds for new settings while keeping handles already found to be unmovable.</summary>
    public SafetyPolicy WithSettings(Settings settings) => new(settings, _ownProcessName, _untouchable);

    /// <summary>Called when a move failed (e.g. elevated window under UIPI) so we stop retrying it.</summary>
    public void MarkUntouchable(nint handle) => _untouchable.Add(handle);

    public bool ActionsAllowed(WindowInfo? foreground, bool paused) =>
        !paused && foreground is not { IsFullscreen: true };

    public bool CanTouch(WindowInfo window) =>
        window.Handle != 0
        && !string.IsNullOrWhiteSpace(window.Title)
        && !window.IsFullscreen
        && !_untouchable.Contains(window.Handle)
        && !_shellClasses.Contains(window.ClassName)
        && !_blockedProcesses.Contains(window.ProcessName)
        && !HasIgnoredKeyword(window.Title);

    private bool HasIgnoredKeyword(string title)
    {
        foreach (var keyword in _titleKeywords)
        {
            if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void AddProcesses(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _blockedProcesses.Add(WithoutExe(name.Trim()));
            }
        }
    }

    // Users often type "foo.exe" into the lists even though the platform reports bare names.
    private static string WithoutExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    // A blank keyword would match every title and silently disable the pet.
    private static string[] NonBlank(IReadOnlyList<string> keywords)
    {
        var result = new List<string>(keywords.Count);
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                result.Add(keyword);
            }
        }

        return [.. result];
    }
}
