using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

/// <summary>Shared windows, clock and helpers for the ActionPlanner test classes.</summary>
public abstract class ActionPlannerFixture
{
    protected static readonly (WindowInfo Window, Verdict Verdict)[] NoWindows = [];

    protected readonly FakeClock Clock = new();
    protected readonly TestSettings Config = new();
    protected readonly ActionPlanner Planner;

    protected readonly WindowInfo YouTube = Windows.Make(1, "chrome", "YouTube - Chrome");
    protected readonly WindowInfo Reddit = Windows.Make(2, "firefox", "Reddit - Firefox");
    protected readonly WindowInfo Editor = Windows.Make(3, "Code", "main.cs - VS Code");
    protected readonly WindowInfo Terminal = Windows.Make(4, "WindowsTerminal", "pwsh");
    protected readonly WindowInfo Notes = Windows.Make(5, "notes", "Untitled - Notes");

    protected ActionPlannerFixture() => Planner = new ActionPlanner(Clock, Config, "DeskPet");

    protected (WindowInfo Window, Verdict Verdict)[] OnTaskWindows => [(Editor, Verdict.OnTask), (Terminal, Verdict.OnTask)];

    protected PetAction.Haul ReachHaul(WindowInfo window)
    {
        Assert.IsType<PetAction.Alert>(Judge(window, Verdict.Distraction));
        Clock.Advance(TimeSpan.FromSeconds(Config.Current.HaulAfterSeconds));
        return Assert.IsType<PetAction.Haul>(Tick(window));
    }

    protected PetAction? Judge(WindowInfo foreground, Verdict verdict, (WindowInfo Window, Verdict Verdict)[]? known = null) =>
        Planner.OnJudged(foreground, verdict, known ?? NoWindows, paused: false);

    protected PetAction? Tick(WindowInfo? foreground, (WindowInfo Window, Verdict Verdict)[]? known = null) =>
        Planner.OnTick(foreground, known ?? NoWindows, paused: false);
}
