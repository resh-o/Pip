using DeskPet.Core.Focus;
using DeskPet.Core.Models;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Focus;

public sealed class FocusTrackerTests
{
    private readonly FakeClock _clock = new();
    private readonly FocusTracker _tracker;

    public FocusTrackerTests() => _tracker = new FocusTracker(_clock);

    [Fact]
    public void Score_is_neutral_with_no_evidence()
    {
        Assert.Equal(0.5, _tracker.Score, 9);
        _clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0.5, _tracker.Score, 9);
        Assert.Equal(FocusBand.Neutral, _tracker.Band);
    }

    [Fact]
    public void A_fresh_sample_has_no_weight_until_time_passes()
    {
        _tracker.Record(Verdict.OnTask);
        Assert.Equal(0.5, _tracker.Score, 9);
    }

    [Fact]
    public void Sustained_on_task_converges_up()
    {
        Hold(Verdict.OnTask, seconds: 600);
        Assert.True(_tracker.Score > 0.9, $"score {_tracker.Score}");
    }

    [Fact]
    public void Sustained_distraction_converges_down()
    {
        Hold(Verdict.Distraction, seconds: 600);
        Assert.True(_tracker.Score < 0.1, $"score {_tracker.Score}");
    }

    [Fact]
    public void Sustained_neutral_stays_neutral()
    {
        Hold(Verdict.Neutral, seconds: 600);
        Assert.Equal(0.5, _tracker.Score, 9);
    }

    [Fact]
    public void Score_moves_monotonically_towards_the_held_verdict()
    {
        double previous = _tracker.Score;
        for (int i = 0; i < 60; i++)
        {
            Hold(Verdict.OnTask, seconds: 1);
            Assert.True(_tracker.Score > previous);
            previous = _tracker.Score;
        }
    }

    [Fact]
    public void Long_distraction_outweighs_brief_on_task()
    {
        Hold(Verdict.Distraction, seconds: 60);
        Hold(Verdict.OnTask, seconds: 2);
        Assert.True(_tracker.Score < 0.3, $"score {_tracker.Score}");
    }

    [Fact]
    public void Brief_distraction_barely_dents_long_on_task()
    {
        Hold(Verdict.OnTask, seconds: 60);
        double before = _tracker.Score;
        Hold(Verdict.Distraction, seconds: 2);
        Assert.True(_tracker.Score > 0.7, $"score {_tracker.Score}");
        Assert.True(before - _tracker.Score < 0.05);
    }

    [Fact]
    public void Held_verdict_accrues_between_samples_up_to_the_hold_cap()
    {
        _tracker.Record(Verdict.OnTask);
        _clock.Advance(TimeSpan.FromSeconds(5));
        double afterFive = _tracker.Score;
        _clock.Advance(TimeSpan.FromSeconds(5));
        double afterTen = _tracker.Score;

        Assert.True(afterTen > afterFive && afterFive > 0.5);
    }

    [Fact]
    public void Without_samples_the_score_decays_towards_neutral()
    {
        Hold(Verdict.OnTask, seconds: 300);
        double start = _tracker.Score;

        _clock.Advance(TimeSpan.FromMinutes(5));
        double oneHalfLife = _tracker.Score;
        _clock.Advance(TimeSpan.FromMinutes(55));

        Assert.True(oneHalfLife < start && oneHalfLife > 0.5);
        Assert.Equal(0.5, _tracker.Score, 2);
    }

    [Fact]
    public void Old_evidence_halves_every_half_life()
    {
        var clock = new FakeClock();
        var tracker = new FocusTracker(clock, halfLife: TimeSpan.FromMinutes(1), maxHold: TimeSpan.FromSeconds(1), priorWeight: TimeSpan.FromSeconds(1));
        tracker.Record(Verdict.OnTask);
        clock.Advance(TimeSpan.FromSeconds(1));
        double evidence = Deviation(tracker.Score);

        // Deviation is w/(w+prior) with prior = 1, so recover the evidence weight w on each side.
        // The held sample stopped accruing at the 1 s cap, so only decay acts from here.
        clock.Advance(TimeSpan.FromMinutes(1));
        double weightBefore = evidence / (1 - evidence);
        double decayed = Deviation(tracker.Score);
        double weightAfter = decayed / (1 - decayed);

        Assert.Equal(0.5, weightAfter / weightBefore, 3);
    }

    [Fact]
    public void Score_stays_within_zero_and_one()
    {
        Hold(Verdict.OnTask, seconds: 7200);
        Assert.InRange(_tracker.Score, 0.0, 1.0);
        Hold(Verdict.Distraction, seconds: 7200);
        Assert.InRange(_tracker.Score, 0.0, 1.0);
    }

    [Fact]
    public void Clock_going_backwards_does_not_corrupt_the_score()
    {
        Hold(Verdict.OnTask, seconds: 60);
        _clock.Advance(TimeSpan.FromMinutes(-10));
        _tracker.Record(Verdict.Distraction);
        Assert.InRange(_tracker.Score, 0.5, 1.0);
    }

    [Fact]
    public void Band_changes_are_reported_once()
    {
        Hold(Verdict.OnTask, seconds: 120);

        Assert.True(_tracker.UpdateBand());
        Assert.Equal(FocusBand.High, _tracker.Band);
        Assert.False(_tracker.UpdateBand());
    }

    [Fact]
    public void Band_is_only_updated_on_request()
    {
        Hold(Verdict.Distraction, seconds: 120);
        Assert.Equal(FocusBand.Neutral, _tracker.Band);
        Assert.True(_tracker.UpdateBand());
        Assert.Equal(FocusBand.Low, _tracker.Band);
    }

    [Fact]
    public void High_band_has_hysteresis()
    {
        Hold(Verdict.OnTask, seconds: 120);
        _tracker.UpdateBand();

        HoldUntil(Verdict.Distraction, () => _tracker.Score < FocusBandRules.EnterHigh);
        Assert.True(_tracker.Score >= FocusBandRules.LeaveHigh);
        Assert.False(_tracker.UpdateBand());
        Assert.Equal(FocusBand.High, _tracker.Band);

        HoldUntil(Verdict.Distraction, () => _tracker.Score < FocusBandRules.LeaveHigh);
        Assert.True(_tracker.UpdateBand());
        Assert.Equal(FocusBand.Neutral, _tracker.Band);
    }

    [Fact]
    public void Low_band_has_hysteresis()
    {
        Hold(Verdict.Distraction, seconds: 120);
        _tracker.UpdateBand();

        HoldUntil(Verdict.OnTask, () => _tracker.Score > FocusBandRules.EnterLow);
        Assert.True(_tracker.Score <= FocusBandRules.LeaveLow);
        Assert.False(_tracker.UpdateBand());
        Assert.Equal(FocusBand.Low, _tracker.Band);

        HoldUntil(Verdict.OnTask, () => _tracker.Score > FocusBandRules.LeaveLow);
        Assert.True(_tracker.UpdateBand());
        Assert.Equal(FocusBand.Neutral, _tracker.Band);
    }

    private static double Deviation(double score) => Math.Abs(score - 0.5) * 2;

    // Mimics the controller: one judged sample per second.
    private void Hold(Verdict verdict, int seconds)
    {
        for (int i = 0; i < seconds; i++)
        {
            _tracker.Record(verdict);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }
    }

    private void HoldUntil(Verdict verdict, Func<bool> done)
    {
        for (int i = 0; i < 3600 && !done(); i++)
            Hold(verdict, seconds: 1);
        Assert.True(done(), "condition never reached");
    }
}
