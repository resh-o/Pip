using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>The wire names of verdicts in the classify_windows tool schema.</summary>
internal static class VerdictNames
{
    public const string OnTask = "on_task";
    public const string Neutral = "neutral";
    public const string Distraction = "distraction";

    public static readonly string[] All = [OnTask, Neutral, Distraction];

    // Ordinal on purpose: the strict tool schema guarantees exact enum values, so anything else is garbage.
    public static bool TryParse(string? name, out Verdict verdict)
    {
        (var known, verdict) = name switch
        {
            OnTask => (true, Verdict.OnTask),
            Neutral => (true, Verdict.Neutral),
            Distraction => (true, Verdict.Distraction),
            _ => (false, Verdict.Neutral),
        };
        return known;
    }
}
