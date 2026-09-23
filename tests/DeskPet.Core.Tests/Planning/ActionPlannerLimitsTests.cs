using DeskPet.Core.Config;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class ActionPlannerLimitsTests : ActionPlannerFixture
{
    [Fact]
    public void Cooldown_blocks_hauling_the_same_window_again()
    {
        Planner.RecordExecuted(ReachHaul(YouTube));
        Judge(Notes, Verdict.Neutral);
        Clock.Advance(TimeSpan.FromSeconds(60));

        Assert.IsType<PetAction.Alert>(Judge(YouTube, Verdict.Distraction));
        Clock.Advance(TimeSpan.FromSeconds(200));
        Assert.Null(Tick(YouTube));

        Clock.Advance(TimeSpan.FromSeconds(40)); // 300 s since the haul
        Assert.IsType<PetAction.Haul>(Tick(YouTube));
    }

    [Fact]
    public void Min_gap_blocks_a_second_haul_of_another_window()
    {
        Planner.RecordExecuted(ReachHaul(YouTube)); // at t=20
        Judge(Reddit, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(44));
        Assert.Null(Tick(Reddit));

        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<PetAction.Haul>(Tick(Reddit));
    }

    [Fact]
    public void Ten_minute_budget_caps_all_physical_actions()
    {
        Config.Current = new Settings { MaxActionsPerTenMinutes = 1, MinActionGapSeconds = 0 };
        Planner.RecordExecuted(ReachHaul(YouTube));

        Assert.Null(Judge(Notes, Verdict.Neutral, OnTaskWindows)); // post-haul pull is still budgeted
        Judge(Reddit, Verdict.Distraction);
        Clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Null(Tick(Reddit));

        Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.IsType<PetAction.Haul>(Tick(Reddit));
    }

    [Fact]
    public void Paused_blocks_everything()
    {
        Assert.Null(Planner.OnJudged(YouTube, Verdict.Distraction, NoWindows, paused: true));
        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(Planner.OnTick(YouTube, NoWindows, paused: true));

        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<PetAction.Alert>(Tick(YouTube)); // resumes where it left off
    }

    [Fact]
    public void Fullscreen_foreground_blocks_everything()
    {
        var game = Windows.Fullscreen(9, "steam", "Some game");
        Assert.Null(Judge(game, Verdict.Distraction));
        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(Tick(game));

        Planner.RecordExecuted(ReachHaul(YouTube));
        var movie = Windows.Fullscreen(10, "vlc", "Movie");
        Assert.Null(Judge(movie, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Never_alerts_or_hauls_untouchable_windows()
    {
        Config.Current = new Settings { AllowList = ["chrome"] };

        Assert.Null(Judge(YouTube, Verdict.Distraction));
        Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(Tick(YouTube));
        Assert.Null(Judge(Windows.Make(8, "explorer", "Downloads"), Verdict.Distraction));
        Assert.Null(Judge(Windows.Make(9, "DeskPet", "Pet"), Verdict.Distraction));
    }

    [Fact]
    public void Never_pulls_untouchable_windows()
    {
        Config.Current = new Settings { IgnoreTitleKeywords = ["main.cs"] };
        Planner.RecordExecuted(ReachHaul(YouTube));
        Planner.MarkUntouchable(Terminal.Handle);

        Assert.Null(Judge(Notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Marked_untouchable_window_is_not_hauled()
    {
        Judge(YouTube, Verdict.Distraction);
        Planner.MarkUntouchable(YouTube.Handle);
        Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(Tick(YouTube));
    }

    [Fact]
    public void Settings_change_applies_to_pending_escalation()
    {
        Judge(YouTube, Verdict.Distraction);
        Config.Current = Config.Current with { AllowList = ["chrome"] };
        Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(Tick(YouTube));
    }
}
