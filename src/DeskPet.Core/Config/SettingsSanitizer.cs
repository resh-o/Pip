namespace DeskPet.Core.Config;

/// <summary>Repairs hand-edited settings: nulls become defaults, out-of-range numbers are clamped.</summary>
internal static class SettingsSanitizer
{
    private static readonly Settings Defaults = new();

    public static Settings Sanitize(Settings s) => s with
    {
        Goal = s.Goal ?? string.Empty,
        Model = string.IsNullOrWhiteSpace(s.Model) ? Defaults.Model : s.Model.Trim(),

        ForegroundPollMs = Math.Clamp(s.ForegroundPollMs, 50, 5_000),
        DwellSeconds = Math.Max(s.DwellSeconds, 0),
        EnumerateSeconds = Math.Max(s.EnumerateSeconds, 1),

        HaulAfterSeconds = Math.Max(s.HaulAfterSeconds, 0),
        PullAfterSeconds = Math.Max(s.PullAfterSeconds, 0),
        WindowCooldownSeconds = Math.Max(s.WindowCooldownSeconds, 0),
        MinActionGapSeconds = Math.Max(s.MinActionGapSeconds, 0),
        MaxActionsPerTenMinutes = Math.Max(s.MaxActionsPerTenMinutes, 0),

        // Zero API calls is a legitimate "offline only" choice; the timeout upper bound keeps TimeSpan math safe.
        MaxApiCallsPerMinute = Math.Max(s.MaxApiCallsPerMinute, 0),
        ApiTimeoutSeconds = Math.Clamp(s.ApiTimeoutSeconds, 1, 120),

        AllowList = CleanList(s.AllowList, Defaults.AllowList),
        // A nulled-out privacy list falls back to the defaults rather than to "send everything".
        IgnoreProcesses = CleanList(s.IgnoreProcesses, Defaults.IgnoreProcesses),
        IgnoreTitleKeywords = CleanList(s.IgnoreTitleKeywords, Defaults.IgnoreTitleKeywords),
        OnTaskKeywords = CleanList(s.OnTaskKeywords, Defaults.OnTaskKeywords),
        DistractionKeywords = CleanList(s.DistractionKeywords, Defaults.DistractionKeywords),
    };

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string>? list, IReadOnlyList<string> fallback) =>
        list is null
            ? fallback
            : list.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray();
}
