using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Core.Focus;

/// <summary>
/// Rolling focus score in 0..1 from judged foreground samples.
/// <para>
/// Each recorded verdict (OnTask = 1, Neutral = 0.5, Distraction = 0) accrues weight for as long
/// as it is held, so a minute of distraction outweighs a two-second glance. Holding is capped at
/// <c>maxHold</c> because a verdict with no follow-up sample is stale evidence. All evidence decays
/// exponentially with the given half-life and is averaged against a fixed neutral prior, which
/// makes the score 0.5 with no evidence, stops one brief sample from swinging it to an extreme, and
/// lets it drift back towards 0.5 when samples stop arriving.
/// </para>
/// State is a handful of doubles, so every call is O(1) and allocation-free.
/// </summary>
public sealed class FocusTracker
{
    private const double Neutral = 0.5;

    private readonly IClock _clock;
    private readonly double _decayPerSecond;
    private readonly double _maxHoldSeconds;
    private readonly double _priorWeight;

    // Decayed evidence as of _lastUpdate, excluding the still-held verdict.
    private double _weightedSum;
    private double _weight;
    private double _heldValue;
    private bool _hasHeld;
    private DateTimeOffset _lastUpdate;

    public FocusTracker(IClock clock, TimeSpan? halfLife = null, TimeSpan? maxHold = null, TimeSpan? priorWeight = null)
    {
        _clock = clock;
        _decayPerSecond = Math.Log(2) / (halfLife ?? TimeSpan.FromMinutes(5)).TotalSeconds;
        _maxHoldSeconds = (maxHold ?? TimeSpan.FromSeconds(10)).TotalSeconds;
        _priorWeight = (priorWeight ?? TimeSpan.FromSeconds(30)).TotalSeconds;
        _lastUpdate = clock.UtcNow;
    }

    /// <summary>Band as of the last <see cref="UpdateBand"/> call.</summary>
    public FocusBand Band { get; private set; } = FocusBand.Neutral;

    public double Score
    {
        get
        {
            Project(_clock.UtcNow, out double sum, out double weight);
            double score = (sum + Neutral * _priorWeight) / (weight + _priorWeight);
            return Math.Clamp(score, 0.0, 1.0);
        }
    }

    public void Record(Verdict verdict)
    {
        DateTimeOffset now = _clock.UtcNow;
        Project(now, out double sum, out double weight);
        _weightedSum = sum;
        _weight = weight;
        _lastUpdate = now;
        _heldValue = ValueOf(verdict);
        _hasHeld = true;
    }

    /// <summary>Re-evaluates the band against the current score; true if it changed.</summary>
    public bool UpdateBand()
    {
        FocusBand next = FocusBandRules.Next(Band, Score);
        if (next == Band)
            return false;
        Band = next;
        return true;
    }

    // Decays stored evidence to `now` and folds in the held verdict's accrual. The held verdict
    // accrues for its first `hold` seconds, and that accrual itself decays for the remainder.
    private void Project(DateTimeOffset now, out double sum, out double weight)
    {
        double elapsed = Math.Max(0, (now - _lastUpdate).TotalSeconds);
        double decay = Math.Exp(-_decayPerSecond * elapsed);
        sum = _weightedSum * decay;
        weight = _weight * decay;
        if (!_hasHeld)
            return;

        double hold = Math.Min(elapsed, _maxHoldSeconds);
        double accrued = Math.Exp(-_decayPerSecond * (elapsed - hold)) * (1 - Math.Exp(-_decayPerSecond * hold)) / _decayPerSecond;
        sum += _heldValue * accrued;
        weight += accrued;
    }

    private static double ValueOf(Verdict verdict) => verdict switch
    {
        Verdict.OnTask => 1.0,
        Verdict.Distraction => 0.0,
        _ => Neutral,
    };
}
