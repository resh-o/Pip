using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class ActionPlannerEscalationTests : ActionPlannerFixture
{
    [Fact]
    public void First_distraction_judgement_alerts()
    {
        var action = Judge(YouTube, Verdict.Distraction);

        Assert.Equal(new PetAction.Alert(YouTube), action);
    }

    [Fact]
    public void Alerts_only_once_per_episode()
    {
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(Judge(YouTube, Verdict.Distraction));
        Assert.Null(Tick(YouTube));
        Assert.Null(Judge(Reddit, Verdict.Distraction));
    }

    [Fact]
    public void Hauls_after_distraction_stays_in_front_long_enough()
    {
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(19.75));
        Assert.Null(Tick(YouTube));

        Clock.Advance(TimeSpan.FromSeconds(0.25));
        Assert.Equal(new PetAction.Haul(YouTube, ScreenEdge.Top), Tick(YouTube));
    }

    [Fact]
    public void HaulClock_restarts_when_a_different_distraction_comes_forward()
    {
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(15));
        Judge(Reddit, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Null(Tick(Reddit));

        Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.IsType<PetAction.Haul>(Tick(Reddit));
    }

    [Fact]
    public void Title_change_in_same_window_keeps_haulClock_once_rejudged()
    {
        var nextVideo = YouTube with { Title = "Another video - YouTube - Chrome" };
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(20));

        Assert.Null(Tick(nextVideo)); // not judged yet: never act on a stale verdict
        Assert.IsType<PetAction.Haul>(Judge(nextVideo, Verdict.Distraction));
    }

    [Fact]
    public void Episode_resets_when_foreground_becomes_non_distraction()
    {
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(15));
        Judge(Editor, Verdict.OnTask);
        Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(new PetAction.Alert(YouTube), Judge(YouTube, Verdict.Distraction));
        Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Null(Tick(YouTube));
    }

    [Fact]
    public void Brief_unjudged_switch_does_not_reset_haulClock_but_blocks_acting_meanwhile()
    {
        Judge(YouTube, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(19));
        Assert.Null(Tick(Editor));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(Tick(Editor));

        Assert.IsType<PetAction.Haul>(Tick(YouTube));
    }

    [Fact]
    public void Nothing_physical_is_planned_while_an_action_is_in_flight()
    {
        ReachHaul(YouTube);
        Clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Null(Tick(YouTube));
        Assert.Null(Judge(Notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Aborted_action_is_not_charged_and_can_be_retried()
    {
        var haul = ReachHaul(YouTube);
        Planner.RecordAborted(haul);

        Assert.Equal(haul, Tick(YouTube));
    }

    [Fact]
    public void Maximised_distraction_is_still_hauled()
    {
        var maximised = Windows.Make(11, "chrome", "Twitch", bounds: new ScreenRect(0, 0, 1920, 1040), maximized: true);

        Assert.IsType<PetAction.Haul>(ReachHaul(maximised));
    }

    [Theory]
    [InlineData(-2500, 100, -2000, 600, ScreenEdge.Left)]
    [InlineData(-600, 100, -100, 600, ScreenEdge.Right)]
    [InlineData(-1500, -350, -1000, -150, ScreenEdge.Top)]
    [InlineData(-1500, 900, -1000, 1080, ScreenEdge.Bottom)]
    public void Hauls_off_the_nearest_edge_of_the_windows_own_monitor(int left, int top, int right, int bottom, ScreenEdge edge)
    {
        var secondary = new ScreenRect(-2560, -360, 0, 1080);
        var window = Windows.Make(12, "chrome", "Reddit", bounds: new ScreenRect(left, top, right, bottom), monitor: secondary);

        Assert.Equal(edge, ReachHaul(window).Edge);
    }
}
