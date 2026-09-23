using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Brain;

/// <summary>
/// TEMPORARY (M2 only, replaced by BrainRouter in M4): judges only windows whose title carries a
/// test marker, so drag testing can never touch anyone's real windows. Everything else is Neutral,
/// which the planner never hauls or pulls.
/// </summary>
internal sealed class TestMarkerBrain : IBrain
{
    public Task<IReadOnlyList<Verdict>> JudgeAsync(Goal goal, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
    {
        var verdicts = new Verdict[windows.Count];
        for (int i = 0; i < windows.Count; i++)
            verdicts[i] = Judge(windows[i].Title);
        return Task.FromResult<IReadOnlyList<Verdict>>(verdicts);
    }

    private static Verdict Judge(string title) =>
        title.Contains("deskpet-distraction", StringComparison.Ordinal) ? Verdict.Distraction
        : title.Contains("deskpet-ontask", StringComparison.Ordinal) ? Verdict.OnTask
        : Verdict.Neutral;
}
