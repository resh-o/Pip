using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class ActionPlannerPullTests : ActionPlannerFixture
{
    [Fact]
    public void Pulls_on_task_window_forward_right_after_a_haul()
    {
        Planner.RecordExecuted(ReachHaul(YouTube));
        Clock.Advance(TimeSpan.FromSeconds(2));

        var action = Judge(Notes, Verdict.Neutral, OnTaskWindows);

        Assert.Equal(new PetAction.PullForward(Editor), action);
    }

    [Fact]
    public void Pull_after_haul_happens_even_with_no_foreground()
    {
        Planner.RecordExecuted(ReachHaul(YouTube));

        Assert.Equal(new PetAction.PullForward(Editor), Planner.OnTick(null, OnTaskWindows, paused: false));
    }

    [Fact]
    public void No_pull_after_haul_when_user_is_already_on_task()
    {
        Planner.RecordExecuted(ReachHaul(YouTube));

        Assert.Null(Judge(Editor, Verdict.OnTask, OnTaskWindows));
        Assert.Null(Planner.OnTick(null, OnTaskWindows, paused: false));
    }

    [Fact]
    public void Pull_prefers_the_most_recently_judged_on_task_window()
    {
        Judge(Terminal, Verdict.OnTask);
        Clock.Advance(TimeSpan.FromSeconds(5));
        Planner.RecordExecuted(ReachHaul(YouTube));

        Assert.Equal(new PetAction.PullForward(Terminal), Judge(Notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Pull_never_targets_the_foreground_or_non_on_task_windows()
    {
        (WindowInfo, Verdict)[] known = [(Notes, Verdict.Neutral), (Reddit, Verdict.Distraction), (Editor, Verdict.OnTask)];
        Planner.RecordExecuted(ReachHaul(YouTube));

        Assert.Null(Judge(Editor, Verdict.Neutral, [(Notes, Verdict.Neutral), (Editor, Verdict.OnTask)]));
        Assert.Equal(new PetAction.PullForward(Editor), Judge(Notes, Verdict.Neutral, known));
    }

    [Fact]
    public void Pulls_after_long_neutral_stretch()
    {
        Judge(Notes, Verdict.Neutral, OnTaskWindows);
        Clock.Advance(TimeSpan.FromSeconds(59.75));
        Assert.Null(Tick(Notes, OnTaskWindows));

        Clock.Advance(TimeSpan.FromSeconds(0.25));
        Assert.Equal(new PetAction.PullForward(Editor), Tick(Notes, OnTaskWindows));
    }

    [Fact]
    public void NeutralClock_resets_when_user_goes_on_task()
    {
        Judge(Notes, Verdict.Neutral, OnTaskWindows);
        Clock.Advance(TimeSpan.FromSeconds(40));
        Judge(Editor, Verdict.OnTask, OnTaskWindows);
        Judge(Notes, Verdict.Neutral, OnTaskWindows);
        Clock.Advance(TimeSpan.FromSeconds(40));

        Assert.Null(Tick(Notes, OnTaskWindows));
    }

    [Fact]
    public void Long_neutral_earns_one_pull_then_waits_again()
    {
        Judge(Notes, Verdict.Neutral, OnTaskWindows);
        Clock.Advance(TimeSpan.FromSeconds(60));
        Planner.RecordExecuted(Tick(Notes, OnTaskWindows)!);
        Clock.Advance(TimeSpan.FromSeconds(59));

        Assert.Null(Tick(Notes, OnTaskWindows));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new PetAction.PullForward(Terminal), Tick(Notes, OnTaskWindows)); // editor cooling down
    }
}
