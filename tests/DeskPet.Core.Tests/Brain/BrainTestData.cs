using DeskPet.Core.Abstractions;
using DeskPet.Core.Config;
using DeskPet.Core.Models;

namespace DeskPet.Core.Tests.Brain;

internal static class BrainTestData
{
    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    public static WindowInfo Window(string process, string title) =>
        new(1, process, title, "Class", new ScreenRect(10, 10, 800, 600), Monitor, false, false);
}

internal sealed class StaticSettings(Settings current) : ISettings
{
    public Settings Current { get; set; } = current;
}

/// <summary>Primary brain double: records every batch and answers through a configurable delegate.</summary>
internal sealed class RecordingBrain : IBrain
{
    public List<IReadOnlyList<WindowInfo>> Calls { get; } = [];

    public Func<IReadOnlyList<WindowInfo>, CancellationToken, Task<IReadOnlyList<Verdict>>> Behaviour { get; set; } =
        (windows, _) => Task.FromResult<IReadOnlyList<Verdict>>(windows.Select(_ => Verdict.OnTask).ToArray());

    public Task<IReadOnlyList<Verdict>> JudgeAsync(Goal goal, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
    {
        Calls.Add(windows);
        return Behaviour(windows, cancellationToken);
    }
}
