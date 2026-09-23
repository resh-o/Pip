using DeskPet.Core.Brain;
using DeskPet.Core.Models;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Brain;

public sealed class VerdictCacheTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void MissThenHit()
    {
        var cache = new VerdictCache(_clock);

        Assert.False(cache.TryGet("k", out _));
        cache.Set("k", Verdict.Distraction);
        Assert.True(cache.TryGet("k", out var verdict));
        Assert.Equal(Verdict.Distraction, verdict);
    }

    [Fact]
    public void SetOverwrites()
    {
        var cache = new VerdictCache(_clock);
        cache.Set("k", Verdict.Distraction);
        cache.Set("k", Verdict.OnTask);

        Assert.True(cache.TryGet("k", out var verdict));
        Assert.Equal(Verdict.OnTask, verdict);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void EvictsLeastRecentlyUsedAtCapacity()
    {
        var cache = new VerdictCache(_clock, capacity: 2, timeToLive: null);
        cache.Set("a", Verdict.OnTask);
        cache.Set("b", Verdict.OnTask);
        cache.TryGet("a", out _);
        cache.Set("c", Verdict.OnTask);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void EntriesExpireAfterTimeToLive()
    {
        var cache = new VerdictCache(_clock);
        cache.Set("k", Verdict.OnTask);

        _clock.Advance(VerdictCache.DefaultTimeToLive - TimeSpan.FromSeconds(1));
        Assert.True(cache.TryGet("k", out _));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet("k", out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void NullTimeToLiveNeverExpires()
    {
        var cache = new VerdictCache(_clock, 10, null);
        cache.Set("k", Verdict.OnTask);
        _clock.Advance(TimeSpan.FromDays(365));

        Assert.True(cache.TryGet("k", out _));
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var cache = new VerdictCache(_clock);
        cache.Set("a", Verdict.OnTask);
        cache.Set("b", Verdict.Neutral);
        cache.Reset();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("a", out _));
    }

    [Fact]
    public void RejectsNonPositiveCapacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new VerdictCache(_clock, 0, null));
}
