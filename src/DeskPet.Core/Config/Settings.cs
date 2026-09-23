namespace DeskPet.Core.Config;

/// <summary>User-editable settings persisted as JSON. Defaults here are the shipped defaults.</summary>
public sealed record Settings
{
    public string Goal { get; init; } = string.Empty;
    public string Model { get; init; } = "claude-haiku-4-5";
    public bool PrivacyAcknowledged { get; init; }

    public int ForegroundPollMs { get; init; } = 250;
    public double DwellSeconds { get; init; } = 1.5;
    public double EnumerateSeconds { get; init; } = 30;

    public double HaulAfterSeconds { get; init; } = 20;
    public double PullAfterSeconds { get; init; } = 60;
    public double WindowCooldownSeconds { get; init; } = 300;
    public double MinActionGapSeconds { get; init; } = 45;
    public int MaxActionsPerTenMinutes { get; init; } = 6;

    public int MaxApiCallsPerMinute { get; init; } = 6;
    public double ApiTimeoutSeconds { get; init; } = 8;

    /// <summary>Processes the pet never moves (matched case-insensitively, without ".exe").</summary>
    public IReadOnlyList<string> AllowList { get; init; } = [];

    /// <summary>Processes whose windows are never sent to the API and never moved.</summary>
    public IReadOnlyList<string> IgnoreProcesses { get; init; } =
        ["1Password", "Bitwarden", "KeePass", "KeePassXC", "LastPass", "Dashlane", "Enpass", "Keeper", "KeeperPasswordManager"];

    /// <summary>Title keywords that keep a window's title from ever being sent to the API.</summary>
    public IReadOnlyList<string> IgnoreTitleKeywords { get; init; } =
        ["bank", "banking", "password", "vault", "InPrivate", "Incognito", "Private Browsing"];

    /// <summary>Offline RuleBrain hints: process names or title keywords.</summary>
    public IReadOnlyList<string> OnTaskKeywords { get; init; } =
        ["Code", "devenv", "rider64", "Godot", "WindowsTerminal", "notepad++", "Obsidian", "WINWORD", "EXCEL", "POWERPNT"];

    public IReadOnlyList<string> DistractionKeywords { get; init; } =
        ["YouTube", "Netflix", "Twitch", "Reddit", "TikTok", "Instagram", "Facebook", "X.com", "Twitter", "Steam", "EpicGamesLauncher"];
}
