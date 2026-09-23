using DeskPet.Core.Config;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class ActionPlannerTests
{
    private static readonly (WindowInfo Window, Verdict Verdict)[] NoWindows = [];

    private readonly FakeClock _clock = new();
    private readonly TestSettings _settings = new();
    private readonly ActionPlanner _planner;

    private readonly WindowInfo _youtube = Windows.Make(1, "chrome", "YouTube - Chrome");
    private readonly WindowInfo _reddit = Windows.Make(2, "firefox", "Reddit - Firefox");
    private readonly WindowInfo _editor = Windows.Make(3, "Code", "main.cs - VS Code");
    private readonly WindowInfo _terminal = Windows.Make(4, "WindowsTerminal", "pwsh");
    private readonly WindowInfo _notes = Windows.Make(5, "notes", "Untitled - Notes");

    public ActionPlannerTests() => _planner = new ActionPlanner(_clock, _settings, "DeskPet");

    private (WindowInfo Window, Verdict Verdict)[] OnTaskWindows => [(_editor, Verdict.OnTask), (_terminal, Verdict.OnTask)];

    [Fact]
    public void First_distraction_judgement_alerts()
    {
        var action = Judge(_youtube, Verdict.Distraction);

        Assert.Equal(new PetAction.Alert(_youtube), action);
    }

    [Fact]
    public void Alerts_only_once_per_episode()
    {
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(Judge(_youtube, Verdict.Distraction));
        Assert.Null(Tick(_youtube));
        Assert.Null(Judge(_reddit, Verdict.Distraction));
    }

    [Fact]
    public void Hauls_after_distraction_stays_in_front_long_enough()
    {
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(19.75));
        Assert.Null(Tick(_youtube));

        _clock.Advance(TimeSpan.FromSeconds(0.25));
        Assert.Equal(new PetAction.Haul(_youtube, ScreenEdge.Top), Tick(_youtube));
    }

    [Fact]
    public void Haul_clock_restarts_when_a_different_distraction_comes_forward()
    {
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(15));
        Judge(_reddit, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Null(Tick(_reddit));

        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.IsType<PetAction.Haul>(Tick(_reddit));
    }

    [Fact]
    public void Title_change_in_same_window_keeps_haul_clock_once_rejudged()
    {
        var nextVideo = _youtube with { Title = "Another video - YouTube - Chrome" };
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(20));

        Assert.Null(Tick(nextVideo)); // not judged yet: never act on a stale verdict
        Assert.IsType<PetAction.Haul>(Judge(nextVideo, Verdict.Distraction));
    }

    [Fact]
    public void Episode_resets_when_foreground_becomes_non_distraction()
    {
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(15));
        Judge(_editor, Verdict.OnTask);
        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(new PetAction.Alert(_youtube), Judge(_youtube, Verdict.Distraction));
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Null(Tick(_youtube));
    }

    [Fact]
    public void Brief_unjudged_switch_does_not_reset_haul_clock_but_blocks_acting_meanwhile()
    {
        Judge(_youtube, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(19));
        Assert.Null(Tick(_editor));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(Tick(_editor));

        Assert.IsType<PetAction.Haul>(Tick(_youtube));
    }

    [Fact]
    public void Nothing_physical_is_planned_while_an_action_is_in_flight()
    {
        ReachHaul(_youtube);
        _clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Null(Tick(_youtube));
        Assert.Null(Judge(_notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Aborted_action_is_not_charged_and_can_be_retried()
    {
        var haul = ReachHaul(_youtube);
        _planner.RecordAborted(haul);

        Assert.Equal(haul, Tick(_youtube));
    }

    [Fact]
    public void Pulls_on_task_window_forward_right_after_a_haul()
    {
        _planner.RecordExecuted(ReachHaul(_youtube));
        _clock.Advance(TimeSpan.FromSeconds(2));

        var action = Judge(_notes, Verdict.Neutral, OnTaskWindows);

        Assert.Equal(new PetAction.PullForward(_editor), action);
    }

    [Fact]
    public void Pull_after_haul_happens_even_with_no_foreground()
    {
        _planner.RecordExecuted(ReachHaul(_youtube));

        Assert.Equal(new PetAction.PullForward(_editor), _planner.OnTick(null, OnTaskWindows, paused: false));
    }

    [Fact]
    public void No_pull_after_haul_when_user_is_already_on_task()
    {
        _planner.RecordExecuted(ReachHaul(_youtube));

        Assert.Null(Judge(_editor, Verdict.OnTask, OnTaskWindows));
        Assert.Null(_planner.OnTick(null, OnTaskWindows, paused: false));
    }

    [Fact]
    public void Pull_prefers_the_most_recently_judged_on_task_window()
    {
        Judge(_terminal, Verdict.OnTask);
        _clock.Advance(TimeSpan.FromSeconds(5));
        _planner.RecordExecuted(ReachHaul(_youtube));

        Assert.Equal(new PetAction.PullForward(_terminal), Judge(_notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Pull_never_targets_the_foreground_or_non_on_task_windows()
    {
        (WindowInfo, Verdict)[] known = [(_notes, Verdict.Neutral), (_reddit, Verdict.Distraction), (_editor, Verdict.OnTask)];
        _planner.RecordExecuted(ReachHaul(_youtube));

        Assert.Null(Judge(_editor, Verdict.Neutral, [(_notes, Verdict.Neutral), (_editor, Verdict.OnTask)]));
        Assert.Equal(new PetAction.PullForward(_editor), Judge(_notes, Verdict.Neutral, known));
    }

    [Fact]
    public void Pulls_after_long_neutral_stretch()
    {
        Judge(_notes, Verdict.Neutral, OnTaskWindows);
        _clock.Advance(TimeSpan.FromSeconds(59.75));
        Assert.Null(Tick(_notes, OnTaskWindows));

        _clock.Advance(TimeSpan.FromSeconds(0.25));
        Assert.Equal(new PetAction.PullForward(_editor), Tick(_notes, OnTaskWindows));
    }

    [Fact]
    public void Neutral_clock_resets_when_user_goes_on_task()
    {
        Judge(_notes, Verdict.Neutral, OnTaskWindows);
        _clock.Advance(TimeSpan.FromSeconds(40));
        Judge(_editor, Verdict.OnTask, OnTaskWindows);
        Judge(_notes, Verdict.Neutral, OnTaskWindows);
        _clock.Advance(TimeSpan.FromSeconds(40));

        Assert.Null(Tick(_notes, OnTaskWindows));
    }

    [Fact]
    public void Long_neutral_earns_one_pull_then_waits_again()
    {
        Judge(_notes, Verdict.Neutral, OnTaskWindows);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _planner.RecordExecuted(Tick(_notes, OnTaskWindows)!);
        _clock.Advance(TimeSpan.FromSeconds(59));

        Assert.Null(Tick(_notes, OnTaskWindows));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new PetAction.PullForward(_terminal), Tick(_notes, OnTaskWindows)); // editor cooling down
    }

    [Fact]
    public void Cooldown_blocks_hauling_the_same_window_again()
    {
        _planner.RecordExecuted(ReachHaul(_youtube));
        Judge(_notes, Verdict.Neutral);
        _clock.Advance(TimeSpan.FromSeconds(60));

        Assert.IsType<PetAction.Alert>(Judge(_youtube, Verdict.Distraction));
        _clock.Advance(TimeSpan.FromSeconds(200));
        Assert.Null(Tick(_youtube));

        _clock.Advance(TimeSpan.FromSeconds(40)); // 300 s since the haul
        Assert.IsType<PetAction.Haul>(Tick(_youtube));
    }

    [Fact]
    public void Min_gap_blocks_a_second_haul_of_another_window()
    {
        _planner.RecordExecuted(ReachHaul(_youtube)); // at t=20
        Judge(_reddit, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(44));
        Assert.Null(Tick(_reddit));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<PetAction.Haul>(Tick(_reddit));
    }

    [Fact]
    public void Ten_minute_budget_caps_all_physical_actions()
    {
        _settings.Current = new Settings { MaxActionsPerTenMinutes = 1, MinActionGapSeconds = 0 };
        _planner.RecordExecuted(ReachHaul(_youtube));

        Assert.Null(Judge(_notes, Verdict.Neutral, OnTaskWindows)); // post-haul pull is still budgeted
        Judge(_reddit, Verdict.Distraction);
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Null(Tick(_reddit));

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.IsType<PetAction.Haul>(Tick(_reddit));
    }

    [Fact]
    public void Paused_blocks_everything()
    {
        Assert.Null(_planner.OnJudged(_youtube, Verdict.Distraction, NoWindows, paused: true));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(_planner.OnTick(_youtube, NoWindows, paused: true));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<PetAction.Alert>(Tick(_youtube)); // resumes where it left off
    }

    [Fact]
    public void Fullscreen_foreground_blocks_everything()
    {
        var game = Windows.Fullscreen(9, "steam", "Some game");
        Assert.Null(Judge(game, Verdict.Distraction));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(Tick(game));

        _planner.RecordExecuted(ReachHaul(_youtube));
        var movie = Windows.Fullscreen(10, "vlc", "Movie");
        Assert.Null(Judge(movie, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Never_alerts_or_hauls_untouchable_windows()
    {
        _settings.Current = new Settings { AllowList = ["chrome"] };

        Assert.Null(Judge(_youtube, Verdict.Distraction));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(Tick(_youtube));
        Assert.Null(Judge(Windows.Make(8, "explorer", "Downloads"), Verdict.Distraction));
        Assert.Null(Judge(Windows.Make(9, "DeskPet", "Pet"), Verdict.Distraction));
    }

    [Fact]
    public void Never_pulls_untouchable_windows()
    {
        _settings.Current = new Settings { IgnoreTitleKeywords = ["main.cs"] };
        _planner.RecordExecuted(ReachHaul(_youtube));
        _planner.MarkUntouchable(_terminal.Handle);

        Assert.Null(Judge(_notes, Verdict.Neutral, OnTaskWindows));
    }

    [Fact]
    public void Marked_untouchable_window_is_not_hauled()
    {
        Judge(_youtube, Verdict.Distraction);
        _planner.MarkUntouchable(_youtube.Handle);
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(Tick(_youtube));
    }

    [Fact]
    public void Settings_change_applies_to_pending_escalation()
    {
        Judge(_youtube, Verdict.Distraction);
        _settings.Current = _settings.Current with { AllowList = ["chrome"] };
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(Tick(_youtube));
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

    private PetAction.Haul ReachHaul(WindowInfo window)
    {
        Assert.IsType<PetAction.Alert>(Judge(window, Verdict.Distraction));
        _clock.Advance(TimeSpan.FromSeconds(_settings.Current.HaulAfterSeconds));
        return Assert.IsType<PetAction.Haul>(Tick(window));
    }

    private PetAction? Judge(WindowInfo foreground, Verdict verdict, (WindowInfo Window, Verdict Verdict)[]? known = null) =>
        _planner.OnJudged(foreground, verdict, known ?? NoWindows, paused: false);

    private PetAction? Tick(WindowInfo? foreground, (WindowInfo Window, Verdict Verdict)[]? known = null) =>
        _planner.OnTick(foreground, known ?? NoWindows, paused: false);
}
