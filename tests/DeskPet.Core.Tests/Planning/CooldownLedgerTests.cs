using DeskPet.Core.Config;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class CooldownLedgerTests
{
    private readonly FakeClock _clock = new();
    private readonly TestSettings _settings = new(new Settings { WindowCooldownSeconds = 300 });
    private readonly CooldownLedger _ledger;

    public CooldownLedgerTests() => _ledger = new CooldownLedger(_clock, _settings);

    [Fact]
    public void Unknown_window_is_not_cooling_down()
    {
        Assert.False(_ledger.IsCoolingDown(1));
    }

    [Fact]
    public void Recorded_window_cools_down_for_the_configured_time()
    {
        _ledger.Record(1);
        _clock.Advance(TimeSpan.FromSeconds(299));
        Assert.True(_ledger.IsCoolingDown(1));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_ledger.IsCoolingDown(1));
    }

    [Fact]
    public void Cooldown_is_per_window()
    {
        _ledger.Record(1);

        Assert.False(_ledger.IsCoolingDown(2));
    }

    [Fact]
    public void Recording_again_restarts_the_cooldown()
    {
        _ledger.Record(1);
        _clock.Advance(TimeSpan.FromSeconds(200));
        _ledger.Record(1);
        _clock.Advance(TimeSpan.FromSeconds(200));

        Assert.True(_ledger.IsCoolingDown(1));
    }

    [Fact]
    public void Follows_settings_changes()
    {
        _ledger.Record(1);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _settings.Current = _settings.Current with { WindowCooldownSeconds = 30 };

        Assert.False(_ledger.IsCoolingDown(1));
    }

    [Fact]
    public void Expired_entries_are_pruned()
    {
        for (nint handle = 1; handle <= 50; handle++)
        {
            _ledger.Record(handle);
        }

        _clock.Advance(TimeSpan.FromSeconds(300));
        _ledger.Record(99);

        Assert.Equal(1, _ledger.TrackedCount);
    }
}
