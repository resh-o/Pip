using DeskPet.Core.Config;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class RateLimiterTests
{
    private readonly FakeClock _clock = new();
    private readonly TestSettings _settings = new(new Settings { MinActionGapSeconds = 45, MaxActionsPerTenMinutes = 6 });
    private readonly RateLimiter _limiter;

    public RateLimiterTests() => _limiter = new RateLimiter(_clock, _settings);

    [Fact]
    public void Allows_first_action()
    {
        Assert.True(_limiter.CanAct());
    }

    [Fact]
    public void Enforces_minimum_gap()
    {
        _limiter.Record();
        _clock.Advance(TimeSpan.FromSeconds(44));
        Assert.False(_limiter.CanAct());
        Assert.True(_limiter.HasBudget());

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(_limiter.CanAct());
    }

    [Fact]
    public void Caps_actions_per_ten_minutes()
    {
        RecordEvery(TimeSpan.FromSeconds(60), 6);

        Assert.False(_limiter.CanAct());
        Assert.False(_limiter.HasBudget());
    }

    [Fact]
    public void Budget_slides_as_old_actions_expire()
    {
        RecordEvery(TimeSpan.FromSeconds(60), 6); // actions at t=0..300, now t=360

        _clock.Advance(TimeSpan.FromSeconds(239)); // t=599: first action still inside the window
        Assert.False(_limiter.CanAct());

        _clock.Advance(TimeSpan.FromSeconds(1)); // t=600
        Assert.True(_limiter.CanAct());
        _limiter.Record();
        Assert.False(_limiter.HasBudget());
    }

    [Fact]
    public void Follows_settings_changes()
    {
        _limiter.Record();
        _settings.Current = _settings.Current with { MaxActionsPerTenMinutes = 1 };
        _clock.Advance(TimeSpan.FromSeconds(60));

        Assert.False(_limiter.CanAct());
    }

    private void RecordEvery(TimeSpan gap, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.True(_limiter.CanAct());
            _limiter.Record();
            _clock.Advance(gap);
        }
    }
}
